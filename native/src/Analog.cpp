#include "Analog.h"

#include <fcntl.h>
#include <unistd.h>

#include <charconv>
#include <chrono>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <map>
#include <optional>
#include <thread>

#include "endpoint/BoardSupport.h"

#include "EndpointDevices.h"
#include "Errors.h"
#include "KernelModules.h"
#include "PeripheralPins.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal {

// The registered board's analog inputs. Core has no chip knowledge: the
// per-chip tables live in BoardSupportInit.cpp and dispatch through
// BoardSupport, the same split as .NET's AdcController and
// BoardSupport.Current.AdcInputs.
using Core::AnalogInput;

namespace {

constexpr int P(char port, int line) { return (port - 'A') * 16 + line; }

const AnalogInput* FindInput(int pin) { return Core::BoardSupport::Current().AdcFind(pin); }

const AnalogInput& GetInput(int pin) {
    auto* input = FindInput(pin);
    if (!input) {
        // The same check and message as .NET's AnalogController.OpenPin
        ThrowNotSupported("Pin " + std::to_string(pin) + " is not supporting analog input.");
    }
    return *input;
}

std::string ReadText(const std::string& path) {
    std::ifstream file(path);
    std::string text;
    std::getline(file, text);
    return text;
}

// The sample format is "<endian>:<sign><real bits>/<storage bits>>><shift>".
// The real bits are the resolution: the MA35D1 EADC reports "le:u12/16>>0",
// 12 meaningful bits padded into a 16-bit word.
int ReadBits(const std::string& folder) {
    std::error_code error;
    for (const auto& entry : std::filesystem::directory_iterator(folder + "/scan_elements", error)) {
        auto name = entry.path().filename().string();
        if (name.rfind("in_voltage", 0) != 0 || name.size() < 5 || name.substr(name.size() - 5) != "_type") {
            continue;
        }
        auto format = ReadText(entry.path().string());
        auto sign = format.find(':');
        auto slash = format.find('/');
        if (sign != std::string::npos && slash != std::string::npos && slash > sign + 2) {
            try {
                return std::stoi(format.substr(sign + 2, slash - sign - 2));
            } catch (...) {
            }
        }
    }
    return 16;  // only reached for a driver that publishes no sample format at all
}

// A number from a sysfs attribute, or nothing if it is missing or not a number.
std::optional<double> ReadDouble(const std::string& path) {
    auto text = ReadText(path);
    if (text.empty()) {
        return std::nullopt;
    }
    try {
        return std::stod(text);
    } catch (...) {
        return std::nullopt;
    }
}

// The first in_voltage*_<suffix> in the folder, for drivers that publish scale
// and offset per channel rather than per device.
std::optional<double> FirstPerChannelDouble(const std::string& folder, const std::string& suffix) {
    std::error_code error;
    auto tail = "_" + suffix;
    for (const auto& entry : std::filesystem::directory_iterator(folder, error)) {
        auto name = entry.path().filename().string();
        if (name.rfind("in_voltage", 0) != 0 || name.size() < tail.size() ||
            name.substr(name.size() - tail.size()) != tail) {
            continue;
        }
        if (auto value = ReadDouble(entry.path().string())) {
            return value;
        }
    }
    return std::nullopt;
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

const Converter& Converter::Get(const std::string& iioName) {
    static std::mutex gate;
    static std::map<std::string, Converter> cache;
    std::lock_guard lock(gate);
    auto found = cache.find(iioName);
    if (found != cache.end()) {
        return found->second;
    }

    auto folder = EndpointDevices::FindIio(iioName);
    if (!folder) {
        ThrowInvalidOperation("IIO device '" + iioName + "' is not enabled in this firmware: update the board's firmware");
    }
    Converter c;
    c.folder = *folder;
    c.bits = ReadBits(c.folder);

    // Scale and offset can be per-device (STM32) or per-channel (some MA35
    // kernels). Try the device-wide ones first, then the first per-channel
    // entry so either layout works. The MA35D1 EADC driver publishes neither,
    // so the last resort is the supply the converter actually references,
    // which is 3.3 V on every Endpoint board.
    auto scale = ReadDouble(c.folder + "/in_voltage_scale");
    if (!scale) scale = FirstPerChannelDouble(c.folder, "scale");
    c.scaleMillivolts = scale.value_or(3300.0 / (1LL << c.bits));

    auto offset = ReadDouble(c.folder + "/in_voltage_offset");
    if (!offset) offset = FirstPerChannelDouble(c.folder, "offset");
    c.offset = static_cast<int>(offset.value_or(0));

    return cache.emplace(iioName, c).first->second;
}

namespace AdcController {

int PinCount() {
    int count = 0;
    Core::BoardSupport::Current().AdcInputs(&count);
    return count;
}

bool SupportsAnalogInput(int pin) { return FindInput(pin) != nullptr; }

int ConvertPinNumberToAnalogChannelNumber(int pin) {
    auto input = FindInput(pin);
    if (!input) {
        ThrowArgument("Pin " + std::to_string(pin) + " has no analog input: use the chip's Adc.Pin constants");
    }
    return input->channel;
}

// The voltage reference comes from the chip's primary converter: ADC1 on
// EPM815, EADC0 on EPN256 / EPN128. Either chip reports 3.3 V in practice; it
// is read from the IIO driver so there is no hidden assumption.
double VoltageReferenceMillivolts() {
    int count = 0;
    auto* inputs = Core::BoardSupport::Current().AdcInputs(&count);
    if (count == 0) {
        ThrowNotSupported("This board has no analog inputs");
    }
    return Converter::Get(inputs[0].iioName).ReferenceMillivolts();
}

}  // namespace AdcController

AnalogInputPinImpl::AnalogInputPinImpl(int pin) : AnalogInputPinImpl(pin, GetInput(pin)) {}

AnalogInputPinImpl::AnalogInputPinImpl(int pin, const AnalogInput& input)
    : name_(input.name), hasOwnPin_(input.hasOwnPin), converter_(Converter::Get(input.iioName)) {
    auto path = converter_.folder + "/in_voltage" + std::to_string(input.channel) + "_raw";
    if (!std::filesystem::exists(path)) {
        ThrowInvalidOperation("The analog input on " + name_ + " (channel " + std::to_string(input.channel) +
                              ") is not enabled in this firmware: update the board's firmware");
    }
    // hasOwnPin is false for dedicated analog pads (STM32's ANA0 / ANA1),
    // which are not GPIO and so have nothing to mux or reserve. For the rest,
    // `alternate` is -1 on STM32 (the pad's analog mode) and the MFP value on
    // MA35, which has no analog mode and selects ADC inputs like any other
    // peripheral function; PeripheralPins::Apply handles both.
    if (hasOwnPin_ && !PeripheralPins::Connect("ADC " + name_, {PinFunction{pin, input.alternate}})) {
        ThrowInvalidOperation("The analog input on " + name_ + " is already open");
    }
    fd_ = open(path.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd_ < 0) {
        auto error = errno;
        if (hasOwnPin_) {
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
    if (hasOwnPin_) {
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
