#include "Analog.h"

#include <fcntl.h>
#include <unistd.h>

#include <charconv>
#include <chrono>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <map>
#include <thread>

#include "EndpointDevices.h"
#include "Errors.h"
#include "KernelModules.h"
#include "PeripheralPins.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal {

namespace {

constexpr int P(char port, int line) { return (port - 'A') * 16 + line; }

// An analog input: its converter (1 or 2) and channel there
struct Input {
    int pin, converter, channel;

    bool IsGpio() const { return pin <= Pins::Last; }

    std::string Name() const {
        if (pin == AdcPins::ANA0) return "ANA0";
        if (pin == AdcPins::ANA1) return "ANA1";
        return Pins::Name(pin);
    }
};

const Input Inputs[] = {
    {AdcPins::ANA0, 1, 0}, {AdcPins::ANA1, 1, 1}, {P('F', 11), 1, 2}, {P('A', 6), 1, 3},
    {P('F', 12), 1, 6},    {P('B', 0), 1, 9},     {P('C', 0), 1, 10}, {P('C', 3), 1, 13},
    {P('A', 3), 1, 15},    {P('A', 0), 1, 16},    {P('A', 4), 1, 18}, {P('A', 5), 1, 19},
    {P('F', 13), 2, 2},    {P('F', 14), 2, 6},
};

const Input* FindInput(int pin) {
    for (const auto& input : Inputs) {
        if (input.pin == pin) {
            return &input;
        }
    }
    return nullptr;
}

std::string ReadText(const std::string& path) {
    std::ifstream file(path);
    std::string text;
    std::getline(file, text);
    return text;
}

// The sample format, for example "le:u16/16>>0": 16 bits
int ReadBits(const std::string& folder) {
    std::error_code error;
    for (const auto& entry : std::filesystem::directory_iterator(folder + "/scan_elements", error)) {
        auto name = entry.path().filename().string();
        if (name.rfind("in_voltage", 0) != 0 || name.size() < 5 || name.substr(name.size() - 5) != "_type") {
            continue;
        }
        auto format = ReadText(entry.path().string());
        auto slash = format.find('/');
        auto shift = format.find(">>");
        if (slash != std::string::npos && shift != std::string::npos && shift > slash) {
            return std::stoi(format.substr(slash + 1, shift - slash - 1));
        }
    }
    return 16;  // the STM32MP1 converters' resolution unless the device tree lowers it
}

uint32_t ParseNumber(const char* text, size_t length, const std::string& what) {
    uint32_t value = 0;
    auto [end, error] = std::from_chars(text, text + length, value);
    if (error != std::errc()) {
        ThrowIO("Unexpected reading from " + what);
    }
    return value;
}

}  // namespace

// ---------------------------------------------------------------- ADC

const Converter& Converter::Get(int number) {
    static std::mutex gate;
    static std::map<int, Converter> cache;
    std::lock_guard lock(gate);
    auto found = cache.find(number);
    if (found != cache.end()) {
        return found->second;
    }

    auto folder = EndpointDevices::FindIio(number == 1 ? "48003000.adc:adc@0" : "48003000.adc:adc@100");
    if (!folder) {
        ThrowInvalidOperation("ADC" + std::to_string(number) + " is not enabled in this firmware: update the board's firmware");
    }
    Converter c;
    c.folder = *folder;
    c.scaleMillivolts = std::stod(ReadText(c.folder + "/in_voltage_scale"));
    if (std::filesystem::exists(c.folder + "/in_voltage_offset")) {
        c.offset = std::stoi(ReadText(c.folder + "/in_voltage_offset"));
    }
    c.bits = ReadBits(c.folder);
    return cache.emplace(number, c).first->second;
}

namespace AdcController {

int PinCount() { return static_cast<int>(std::size(Inputs)); }

bool SupportsAnalogInput(int pin) { return FindInput(pin) != nullptr; }

int ConvertPinNumberToAnalogChannelNumber(int pin) {
    auto input = FindInput(pin);
    if (!input) {
        ThrowArgument("Pin " + std::to_string(pin) + " has no analog input: use the EPM815.Adc.Pin constants");
    }
    return input->channel;
}

double VoltageReferenceMillivolts() { return Converter::Get(1).ReferenceMillivolts(); }

}  // namespace AdcController

AnalogInputPinImpl::AnalogInputPinImpl(int pin)
    : name_(FindInput(pin) ? FindInput(pin)->Name() : ""),
      gpio_(FindInput(pin) && FindInput(pin)->IsGpio()),
      converter_([pin]() -> const Converter& {
          auto input = FindInput(pin);
          if (!input) {
              // The same check and message as .NET's AnalogController.OpenPin
              ThrowNotSupported("Pin " + std::to_string(pin) + " is not supporting analog input.");
          }
          return Converter::Get(input->converter);
      }()) {
    auto input = FindInput(pin);
    auto path = converter_.folder + "/in_voltage" + std::to_string(input->channel) + "_raw";
    if (!std::filesystem::exists(path)) {
        ThrowInvalidOperation("The analog input on " + name_ + " (ADC" + std::to_string(input->converter) + " channel " +
                              std::to_string(input->channel) + ") is not enabled in this firmware: update the board's firmware");
    }
    if (gpio_ && !PeripheralPins::Connect("ADC " + name_, {PinFunction::Analog(pin)})) {
        ThrowInvalidOperation("The analog input on " + name_ + " is already open");
    }
    fd_ = open(path.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd_ < 0) {
        auto error = errno;
        if (gpio_) {
            PeripheralPins::Disconnect("ADC " + name_);
        }
        errno = error;
        ThrowSystemError("Cannot open " + path);
    }
}

AnalogInputPinImpl::~AnalogInputPinImpl() {
    if (fd_ >= 0) {
        close(fd_);
    }
    if (gpio_) {
        PeripheralPins::Disconnect("ADC " + name_);
    }
}

uint32_t AnalogInputPinImpl::ReadRaw() {
    std::lock_guard lock(gate_);
    char buffer[16];
    // Every read from the start makes the driver take a new sample
    auto length = pread(fd_, buffer, sizeof buffer, 0);
    if (length <= 0) {
        ThrowSystemError("Cannot read the analog input on " + name_);
    }
    return ParseNumber(buffer, static_cast<size_t>(length), "the analog input on " + name_);
}

double AnalogInputPinImpl::ReadMillivolts() {
    return (static_cast<double>(ReadRaw()) + converter_.offset) * converter_.scaleMillivolts;
}

// ---------------------------------------------------------------- DAC

namespace {

struct Output {
    int channel, pin;
    const char* device;
    int deviceChannel;
};

const Output Outputs[] = {
    {0, P('A', 4), "40017000.dac:dac@1", 1},
    {1, P('A', 5), "40017000.dac:dac@2", 2},
};

// The DAC drivers are modules: they load the first time a program uses the DAC
std::string LoadDacDriver(const std::string& device) {
    static std::mutex gate;
    std::lock_guard lock(gate);
    auto folder = EndpointDevices::FindIio(device);
    if (!folder) {
        KernelModules::Load("stm32-dac-core");
        KernelModules::Load("stm32-dac");
        auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1);
        while (!(folder = EndpointDevices::FindIio(device))) {
            if (std::chrono::steady_clock::now() > deadline) {
                ThrowInvalidOperation("The DAC did not start: update the board's firmware");
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
    }
    return *folder;
}

void WriteFile(const std::string& path, const std::string& value) {
    std::ofstream file(path);
    file << value;
    file.flush();
    if (!file) {
        ThrowIO("Cannot write " + path);
    }
}

}  // namespace

DacChannelImpl::DacChannelImpl(int channel) : channel_(channel) {
    const Output* output = nullptr;
    for (const auto& o : Outputs) {
        if (o.channel == channel) {
            output = &o;
        }
    }
    if (!output) {
        ThrowArgumentOutOfRange(channel >= 0 && channel < ChannelCount
                                    ? "DAC channel " + std::to_string(channel) + " is not enabled in this firmware"
                                    : "Use EPM815.Dac.PA4 or EPM815.Dac.PA5");
    }
    pin_ = output->pin;
    owner_ = "DAC channel " + std::to_string(channel) + " (" + Pins::Name(pin_) + ")";

    if (!PeripheralPins::Connect(owner_, {PinFunction::Analog(pin_)})) {
        ThrowInvalidOperation("DAC channel " + std::to_string(channel) + " is already open");
    }
    try {
        auto folder = LoadDacDriver(output->device);
        auto prefix = folder + "/out_voltage" + std::to_string(output->deviceChannel);
        powerDown_ = prefix + "_powerdown";
        fd_ = open((prefix + "_raw").c_str(), O_WRONLY | O_CLOEXEC);
        if (fd_ < 0) {
            ThrowSystemError("Cannot open " + prefix + "_raw");
        }
        WriteValue(0);
        WriteFile(powerDown_, "0");  // output on, at 0 V
    }
    catch (...) {
        if (fd_ >= 0) {
            close(fd_);
        }
        PeripheralPins::Disconnect(owner_);
        throw;
    }
}

DacChannelImpl::~DacChannelImpl() {
    try {
        WriteFile(powerDown_, "1");  // output off (high impedance)
    }
    catch (...) {
    }
    close(fd_);
    PeripheralPins::Disconnect(owner_);
}

void DacChannelImpl::WriteValue(int value) {
    if (value < MinValue || value > MaxValue) {
        ThrowArgumentOutOfRange("The value must be " + std::to_string(MinValue) + " to " + std::to_string(MaxValue));
    }
    auto text = std::to_string(value);
    if (pwrite(fd_, text.data(), text.size(), 0) != static_cast<ssize_t>(text.size())) {
        ThrowSystemError("Cannot write DAC channel " + std::to_string(channel_));
    }
    last_ = value;
}

}  // namespace GHIElectronics::Endpoint::Internal
