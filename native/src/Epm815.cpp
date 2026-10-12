#include "Epm815.h"

#include <filesystem>
#include <fstream>
#include <map>
#include <mutex>
#include <vector>

#include "EndpointDevices.h"
#include "Errors.h"
#include "KernelModules.h"
#include "PeripheralPins.h"
#include "Pins.h"
#include "SystemCommands.h"

namespace GHIElectronics::Endpoint::Internal::Epm815 {

namespace {

using Stm32Mp1Pins::OutputType;
using Stm32Mp1Pins::Pull;
using Stm32Mp1Pins::Speed;

// EPM815 pin numbers (port * 16 + line)
constexpr int P(char port, int line) { return (port == 'Z' ? Pins::PortZ : port - 'A') * 16 + line; }

std::string ReadText(const std::string& path) {
    std::ifstream file(path);
    std::string text;
    std::getline(file, text);
    return text;
}

std::string Hex(uint64_t value) {
    char text[20];
    std::snprintf(text, sizeof text, "%llx", static_cast<unsigned long long>(value));
    return text;
}

}  // namespace

// ---------------------------------------------------------------- I2C

namespace I2c {

namespace {

struct Bus {
    const char* name;
    uint64_t address;
    int scl, sda, sclAlternate, sdaAlternate;
};

// Indexed by the constants
const Bus Buses[] = {
    {"I2C1", 0x40012000, P('D', 12), P('D', 13), 5, 5},
    {"I2C5", 0x40015000, P('Z', 4), P('Z', 5), 4, 4},
    {"I2C4", 0x5C002000, P('B', 6), P('F', 15), 6, 4},
    {"I2C6", 0x5C009000, P('D', 1), P('D', 0), 2, 2},
};

const Bus& Get(int bus) {
    if (bus < 0 || bus >= static_cast<int>(std::size(Buses))) {
        ThrowArgumentOutOfRange("The I2C buses are I2c1, I2c4, I2c5 and I2c6");
    }
    return Buses[bus];
}

// Added to the kernel's I2C driver by the Endpoint firmware: the kernel
// computes the timings for the speed, as for the device tree's
std::string SpeedFile(const Bus& b) { return "/sys/bus/platform/devices/" + Hex(b.address) + ".i2c/clock_frequency"; }

void SetFrequency(const Bus& b, int frequency) {
    auto path = SpeedFile(b);
    if (std::filesystem::exists(path)) {
        {
            std::ofstream file(path);
            file << frequency;
            file.flush();
            if (!file) {
                ThrowIO("Cannot set the speed of " + std::string(b.name));
            }
        }
        // The kernel lowers a speed the board's bus cannot reach: the rise and
        // fall times in its device tree (pull-ups, wiring) are too slow for it
        auto actual = std::stoi(ReadText(path));
        if (actual != frequency) {
            ThrowArgumentOutOfRange(std::string(b.name) + " cannot run at " + std::to_string(frequency) +
                                    " Hz on this board (its bus rise and fall times are too slow): it runs at " +
                                    std::to_string(actual) + " Hz");
        }
    }
    else if (frequency != DefaultFrequency) {
        ThrowNotSupported("This firmware cannot change the I2C speed: update the board's firmware");
    }
}

}  // namespace

void Initialize(int bus, int frequency) {
    const auto& b = Get(bus);
    if (frequency < MinFrequency || frequency > MaxFrequency) {
        ThrowArgumentOutOfRange("I2C speeds are " + std::to_string(MinFrequency) + " to " + std::to_string(MaxFrequency) + " Hz");
    }
    auto device = "i2c-" + std::to_string(bus);
    if (!EndpointDevices::IsAt("i2c-dev", device, b.address)) {
        ThrowInvalidOperation("/dev/" + device + " is not " + b.name + " in this firmware: update the board's firmware");
    }

    auto connected = PeripheralPins::Connect(b.name, {{b.scl, b.sclAlternate, Pull::Up, OutputType::OpenDrain},
                                                      {b.sda, b.sdaAlternate, Pull::Up, OutputType::OpenDrain}});
    try {
        SetFrequency(b, frequency);
    }
    catch (...) {
        // A bus that was already initialized stays as it was
        if (connected) {
            PeripheralPins::Disconnect(b.name);
        }
        throw;
    }
}

void UnInitialize(int bus) { PeripheralPins::Disconnect(Get(bus).name); }

int GetFrequency(int bus) {
    auto path = SpeedFile(Get(bus));
    return std::filesystem::exists(path) ? std::stoi(ReadText(path)) : DefaultFrequency;
}

}  // namespace I2c

// ---------------------------------------------------------------- SPI

namespace Spi {

namespace {

struct Bus {
    const char* name;
    uint64_t address;
    int sck, miso, mosi, alternate;
};

const Bus Buses[] = {
    {"SPI1", 0x44004000, P('Z', 0), P('Z', 1), P('Z', 2), 5},
    {"SPI4", 0x44005000, P('E', 12), P('E', 13), P('E', 14), 5},
    {"SPI5", 0x44009000, P('F', 7), P('F', 8), P('F', 9), 5},
};

constexpr const char* BufferSizeFile = "/sys/module/spidev/parameters/bufsiz";
std::mutex gate;

const Bus& Get(int bus) {
    if (bus < 0 || bus >= static_cast<int>(std::size(Buses))) {
        ThrowArgumentOutOfRange("The SPI buses are Spi1, Spi4 and Spi5");
    }
    return Buses[bus];
}

void LoadDriver(int bufferSize) {
    std::lock_guard lock(gate);
    if (!std::filesystem::exists(BufferSizeFile)) {
        KernelModules::Load("spidev", bufferSize == DefaultBufferSize ? "" : "bufsiz=" + std::to_string(bufferSize));
    }
    auto loaded = std::stoi(ReadText(BufferSizeFile));
    if (loaded < bufferSize) {
        ThrowInvalidOperation("The SPI driver is already loaded for transfers up to " + std::to_string(loaded) +
                              " bytes. For " + std::to_string(bufferSize) +
                              ", give this size to the first Initialize after boot.");
    }
}

}  // namespace

void Initialize(int bus, int bufferSize) {
    const auto& b = Get(bus);
    if (bufferSize < 1) {
        ThrowArgumentOutOfRange("The buffer size must be at least 1 byte");
    }

    LoadDriver(bufferSize);
    auto device = "spidev" + std::to_string(bus) + ".0";
    if (!EndpointDevices::IsAt("spidev", device, b.address)) {
        ThrowInvalidOperation("/dev/" + device + " is not " + b.name + " in this firmware: update the board's firmware");
    }
    KernelModules::WaitFor("/dev/" + device);

    // Clock and data out switch fast: medium speed edges, as the ST device trees use for SPI
    PeripheralPins::Connect(b.name, {{b.sck, b.alternate, Pull::None, OutputType::PushPull, Speed::Medium},
                                     {b.miso, b.alternate},
                                     {b.mosi, b.alternate, Pull::None, OutputType::PushPull, Speed::Medium}});
}

void UnInitialize(int bus) { PeripheralPins::Disconnect(Get(bus).name); }

}  // namespace Spi

// ---------------------------------------------------------------- PWM

namespace Pwm {

namespace {

// Channel: 0 = the timer's channel 1 (the system counts from 0)
struct Output {
    int pin, timer, channel, alternate;
};

const Output Outputs[] = {
    {P('A', 8), 1, 0, 1},  {P('E', 11), 1, 1, 1}, {P('A', 10), 1, 2, 1}, {P('A', 11), 1, 3, 1},
    {P('A', 15), 2, 0, 1}, {P('B', 3), 2, 1, 1},  {P('A', 3), 2, 3, 1},
    {P('C', 6), 3, 0, 2},  {P('B', 5), 3, 1, 2},  {P('B', 0), 3, 2, 2},
    {P('D', 12), 4, 0, 2}, {P('B', 7), 4, 1, 2},  {P('D', 14), 4, 2, 2}, {P('D', 15), 4, 3, 2},
    {P('A', 0), 5, 0, 2},  {P('H', 11), 5, 1, 2}, {P('H', 12), 5, 2, 2}, {P('I', 0), 5, 3, 2},
    {P('I', 5), 8, 0, 3},  {P('I', 6), 8, 1, 3},  {P('I', 7), 8, 2, 3},  {P('I', 2), 8, 3, 3},
    {P('H', 6), 12, 0, 2}, {P('H', 9), 12, 1, 2},
    {P('A', 6), 13, 0, 9},
    {P('F', 9), 14, 0, 9},
    {P('E', 5), 15, 0, 4}, {P('E', 6), 15, 1, 4},
    {P('B', 8), 16, 0, 1},
    {P('B', 9), 17, 0, 1},
};

const std::map<int, uint64_t> TimerAddress = {
    {1, 0x44000000},  {2, 0x40000000},  {3, 0x40001000},  {4, 0x40002000},  {5, 0x40003000},  {8, 0x44001000},
    {12, 0x40006000}, {13, 0x40007000}, {14, 0x40008000}, {15, 0x44006000}, {16, 0x44007000}, {17, 0x44008000},
};

const Output& Get(int pin) {
    for (const auto& o : Outputs) {
        if (o.pin == pin) {
            return o;
        }
    }
    ThrowArgument("Pin " + Pins::Name(pin) + " has no PWM");
}

std::string Owner(int pin) { return "PWM " + Pins::Name(pin); }

}  // namespace

void Initialize(int pin) {
    const auto& o = Get(pin);
    PeripheralPins::Connect(Owner(pin), {{pin, o.alternate}});
}

void UnInitialize(int pin) {
    Get(pin);
    PeripheralPins::Disconnect(Owner(pin));
}

int GetChipId(int pin) {
    auto timer = Get(pin).timer;
    auto chip = EndpointDevices::Find("pwm", "pwmchip", TimerAddress.at(timer));
    if (chip < 0) {
        ThrowInvalidOperation("TIM" + std::to_string(timer) + "'s PWM is not enabled in this firmware");
    }
    return chip;
}

int GetChannelId(int pin) { return Get(pin).channel; }

}  // namespace Pwm

// ---------------------------------------------------------------- Serial ports

namespace SerialPort {

namespace {

constexpr int None = -1;

struct Port {
    const char* device;
    const char* name;
    uint64_t address;
    int alternate, tx, rx, rts = None, cts = None;

    bool HasFlowControl() const { return rts != None; }
};

const Port Ports[] = {
    {"/dev/ttySTM0", "UART1", 0x5C000000, 7, P('Z', 7), P('Z', 6), P('A', 12), P('Z', 3)},
    {"/dev/ttySTM1", "UART2", 0x4000E000, 7, P('F', 5), P('D', 6), P('D', 4), P('D', 3)},
    {"/dev/ttySTM2", "UART3", 0x4000F000, 7, P('D', 8), P('D', 9), P('D', 12), P('D', 11)},
    {"/dev/ttySTM3", "UART4", 0x40010000, 8, P('D', 1), P('D', 0)},
    {"/dev/ttySTM4", "UART5", 0x40011000, 14, P('B', 13), P('B', 12)},
    {"/dev/ttySTM6", "UART7", 0x40018000, 7, P('E', 8), P('F', 6), P('E', 9), P('E', 10)},
    {"/dev/ttySTM7", "UART8", 0x40019000, 8, P('E', 1), P('E', 0)},
};

std::mutex gate;
// Whether each initialized port has flow control
std::map<std::string, bool> flowControl;

const Port& Get(const std::string& portName) {
    for (const auto& p : Ports) {
        if (portName == p.device) {
            return p;
        }
    }
    ThrowArgument(portName + " is not an EPM815 serial port (Uart1 ... Uart8)");
}

}  // namespace

void Initialize(const std::string& portName, bool enableHardwareFlowControl) {
    const auto& port = Get(portName);
    if (enableHardwareFlowControl && !port.HasFlowControl()) {
        ThrowArgument(std::string(port.name) + " has no RTS and CTS pins");
    }

    std::lock_guard lock(gate);
    auto current = flowControl.find(port.device);
    if (current != flowControl.end()) {
        if (current->second != enableHardwareFlowControl) {
            ThrowInvalidOperation(std::string(port.name) + " is already initialized " + (current->second ? "with" : "without") +
                                  " flow control: call UnInitialize first");
        }
        return;
    }

    auto name = std::filesystem::path(port.device).filename().string();
    if (!EndpointDevices::IsAt("tty", name, port.address)) {
        ThrowInvalidOperation(std::string(port.device) + " is not " + port.name + " in this firmware: update the board's firmware");
    }

    std::vector<PinFunction> pins{{port.tx, port.alternate}, {port.rx, port.alternate}};
    if (enableHardwareFlowControl) {
        pins.push_back({port.rts, port.alternate});
        pins.push_back({port.cts, port.alternate});
    }
    PeripheralPins::Connect(port.name, pins);
    flowControl[port.device] = enableHardwareFlowControl;
}

void UnInitialize(const std::string& portName) {
    const auto& port = Get(portName);
    std::lock_guard lock(gate);
    PeripheralPins::Disconnect(port.name);
    flowControl.erase(port.device);
}

}  // namespace SerialPort

// ---------------------------------------------------------------- CAN

namespace Can {

namespace {

struct Port {
    const char* name;
    const char* interface;
    uint64_t address;
    int tx, rx;
};

const Port Ports[] = {
    {"CAN1", "can0", 0x4400E000, P('D', 1), P('D', 0)},
    {"CAN2", "can1", 0x4400F000, P('B', 13), P('B', 12)},
};

constexpr int Alternate = 9;

const Port& Get(const std::string& port) {
    for (const auto& p : Ports) {
        if (port == p.interface) {
            return p;
        }
    }
    ThrowArgument("There is no CAN port \"" + port + "\": use EPM815.Can.Can1 or Can2");
}

}  // namespace

void Initialize(const std::string& port, int bitrate) {
    const auto& p = Get(port);
    if (bitrate <= 0 || bitrate > MaxBitrate) {
        ThrowArgumentOutOfRange("The bitrate must be more than 0 and at most 1,000,000");
    }
    if (!EndpointDevices::IsAt("net", p.interface, p.address)) {
        ThrowInvalidOperation(std::string(p.interface) + " is not " + p.name + " in this firmware: update the board's firmware");
    }

    // TX switches fast: medium speed edges, as ST's device trees use for CAN
    auto connected = PeripheralPins::Connect(p.name, {{p.tx, Alternate, Pull::None, OutputType::PushPull, Speed::Medium},
                                                      {p.rx, Alternate}});
    try {
        // The bitrate can only change while the port is stopped. The modes are
        // named too: a mode another program switched on (loopback,
        // listen-only, FD) would otherwise stay on.
        auto what = std::string("Cannot start ") + p.name + " at " + std::to_string(bitrate) + " bit/s";
        SystemCommands::Run("ip", what, {"link", "set", p.interface, "down"});
        SystemCommands::Run("ip", what,
                            {"link", "set", p.interface, "type", "can", "bitrate", std::to_string(bitrate), "restart-ms", "100",
                             "loopback", "off", "listen-only", "off", "fd", "off"});
        SystemCommands::Run("ip", what, {"link", "set", p.interface, "up"});
    }
    catch (...) {
        if (connected) {
            PeripheralPins::Disconnect(p.name);
        }
        throw;
    }
}

void UnInitialize(const std::string& port) {
    const auto& p = Get(port);
    SystemCommands::Run("ip", std::string("Cannot stop ") + p.name, {"link", "set", p.interface, "down"});
    PeripheralPins::Disconnect(p.name);
}

}  // namespace Can

}  // namespace GHIElectronics::Endpoint::Internal::Epm815
