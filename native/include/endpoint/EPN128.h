// EPN128 (Nuvoton MA35D0, Chili Pro) peripheral initializers. Mirrors
// .NET's GHIElectronics.Endpoint.Core.EPN128 and Python's EPN128 class so
// sample code reads the same shape in every language. MA35D0 and MA35D1
// share the same pinctrl layout; EPN128 is the subset bonded on Chili Pro
// (I2C4/I2C5, four CAN buses, five user UARTs, two ADC channels).
//
// Pay-for-use rule: the kernel drivers (i2c-ma35d1, m_can_platform) are
// built as modules; nothing probes at boot. The device tree also strips
// pinctrl-0 from every peripheral node, so the kernel never muxes a pin.
// Initialize modprobes the driver and selects the peripheral's pins
// through /usr/sbin/endpoint-pinmux; UnInitialize restores the pins to
// whatever they were and unloads the driver (ref-counted across
// controllers). SerialPort uses the kernel's standard UART binding, so it
// only needs the pin selection.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   I2cController::Initialize(EPN128::I2c::I2c4);
//   int fd = open("/dev/i2c-4", O_RDWR);
//   ...
//   close(fd);
//   I2cController::UnInitialize(EPN128::I2c::I2c4);

#pragma once

#include <algorithm>
#include <array>
#include <cctype>
#include <chrono>
#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <dirent.h>
#include <fcntl.h>
#include <fstream>
#include <map>
#include <mutex>
#include <stdexcept>
#include <string>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <thread>
#include <unistd.h>

// Reuse the helpers (RunChecked, TryRun, WaitPath, ReadTrim, ToLower)
// from EPN256.h - the MA35D0 and MA35D1 share the same Linux plumbing, so
// duplicating the shell + sysfs helpers would only drift.
#include "EPN256.h"

namespace GHIElectronics::Endpoint::Core::EPN128 {

// GPIO pins, for Devices::Gpio::GpioController
struct Gpio {
    // The EPN128's pins: port * 16 + line, ports A to N
    struct Pin {
        static constexpr int PA0 = 0, PA1 = 1, PA2 = 2, PA3 = 3, PA4 = 4, PA5 = 5, PA6 = 6, PA7 = 7;
        static constexpr int PA8 = 8, PA9 = 9, PA10 = 10, PA11 = 11, PA12 = 12, PA13 = 13, PA14 = 14, PA15 = 15;
        static constexpr int PB0 = 16, PB1 = 17, PB2 = 18, PB3 = 19, PB4 = 20, PB5 = 21, PB6 = 22, PB7 = 23;
        static constexpr int PB8 = 24, PB9 = 25, PB10 = 26, PB11 = 27, PB12 = 28, PB13 = 29, PB14 = 30, PB15 = 31;
        static constexpr int PC0 = 32, PC1 = 33, PC2 = 34, PC3 = 35, PC4 = 36, PC5 = 37, PC6 = 38, PC7 = 39;
        static constexpr int PC8 = 40, PC9 = 41, PC10 = 42, PC11 = 43, PC12 = 44, PC13 = 45, PC14 = 46, PC15 = 47;
        static constexpr int PD0 = 48, PD1 = 49, PD2 = 50, PD3 = 51, PD4 = 52, PD5 = 53, PD6 = 54, PD7 = 55;
        static constexpr int PD8 = 56, PD9 = 57, PD10 = 58, PD11 = 59, PD12 = 60, PD13 = 61, PD14 = 62, PD15 = 63;
        static constexpr int PE0 = 64, PE1 = 65, PE2 = 66, PE3 = 67, PE4 = 68, PE5 = 69, PE6 = 70, PE7 = 71;
        static constexpr int PE8 = 72, PE9 = 73, PE10 = 74, PE11 = 75, PE12 = 76, PE13 = 77, PE14 = 78, PE15 = 79;
        static constexpr int PF0 = 80, PF1 = 81, PF2 = 82, PF3 = 83, PF4 = 84, PF5 = 85, PF6 = 86, PF7 = 87;
        static constexpr int PF8 = 88, PF9 = 89, PF10 = 90, PF11 = 91, PF12 = 92, PF13 = 93, PF14 = 94, PF15 = 95;
        static constexpr int PG0 = 96, PG1 = 97, PG2 = 98, PG3 = 99, PG4 = 100, PG5 = 101, PG6 = 102, PG7 = 103;
        static constexpr int PG8 = 104, PG9 = 105, PG10 = 106, PG11 = 107, PG12 = 108, PG13 = 109, PG14 = 110, PG15 = 111;
        static constexpr int PH0 = 112, PH1 = 113, PH2 = 114, PH3 = 115, PH4 = 116, PH5 = 117, PH6 = 118, PH7 = 119;
        static constexpr int PH8 = 120, PH9 = 121, PH10 = 122, PH11 = 123, PH12 = 124, PH13 = 125, PH14 = 126, PH15 = 127;
        static constexpr int PI0 = 128, PI1 = 129, PI2 = 130, PI3 = 131, PI4 = 132, PI5 = 133, PI6 = 134, PI7 = 135;
        static constexpr int PI8 = 136, PI9 = 137, PI10 = 138, PI11 = 139, PI12 = 140, PI13 = 141, PI14 = 142, PI15 = 143;
        static constexpr int PJ0 = 144, PJ1 = 145, PJ2 = 146, PJ3 = 147, PJ4 = 148, PJ5 = 149, PJ6 = 150, PJ7 = 151;
        static constexpr int PJ8 = 152, PJ9 = 153, PJ10 = 154, PJ11 = 155, PJ12 = 156, PJ13 = 157, PJ14 = 158, PJ15 = 159;
        static constexpr int PK0 = 160, PK1 = 161, PK2 = 162, PK3 = 163, PK4 = 164, PK5 = 165, PK6 = 166, PK7 = 167;
        static constexpr int PK8 = 168, PK9 = 169, PK10 = 170, PK11 = 171, PK12 = 172, PK13 = 173, PK14 = 174, PK15 = 175;
        static constexpr int PL0 = 176, PL1 = 177, PL2 = 178, PL3 = 179, PL4 = 180, PL5 = 181, PL6 = 182, PL7 = 183;
        static constexpr int PL8 = 184, PL9 = 185, PL10 = 186, PL11 = 187, PL12 = 188, PL13 = 189, PL14 = 190, PL15 = 191;
        static constexpr int PM0 = 192, PM1 = 193, PM2 = 194, PM3 = 195, PM4 = 196, PM5 = 197, PM6 = 198, PM7 = 199;
        static constexpr int PM8 = 200, PM9 = 201, PM10 = 202, PM11 = 203, PM12 = 204, PM13 = 205, PM14 = 206, PM15 = 207;
        static constexpr int PN0 = 208, PN1 = 209, PN2 = 210, PN3 = 211, PN4 = 212, PN5 = 213, PN6 = 214, PN7 = 215;
        static constexpr int PN8 = 216, PN9 = 217, PN10 = 218, PN11 = 219, PN12 = 220, PN13 = 221, PN14 = 222, PN15 = 223;
        static constexpr int NONE = -1;  // no pin
    };

    // User LED on PB8
    static constexpr int Led = Pin::PB8;

    // The pin's name, for example "PC0"
    static std::string Name(int pin) {
        if (pin < 0 || pin > Pin::PN15) {
            return "#" + std::to_string(pin);
        }
        int port = pin / 16;
        return std::string("P") + static_cast<char>('A' + port) + std::to_string(pin % 16);
    }

    // Linux gpiochip number for the pin. On EPN128 each port is its own
    // gpiochip (A..N = 0..13).
    static int GetChipId(int pin) { return pin / 16; }

    // Line number within the gpiochip (0 to 15 on EPN128).
    static int GetLineId(int pin) { return pin % 16; }
};

}  // namespace GHIElectronics::Endpoint::Core::EPN128

namespace GHIElectronics::Endpoint::Core::EPN128::detail {

// Alias EPN256's helpers so the rest of this header reads like EPN256.h.
using GHIElectronics::Endpoint::Core::EPN256::detail::RunChecked;
using GHIElectronics::Endpoint::Core::EPN256::detail::TryRun;
using GHIElectronics::Endpoint::Core::EPN256::detail::WaitPath;
using GHIElectronics::Endpoint::Core::EPN256::detail::ReadTrim;
using GHIElectronics::Endpoint::Core::EPN256::detail::ToLower;
using GHIElectronics::Endpoint::Core::EPN256::detail::MuxPin;
using GHIElectronics::Endpoint::Core::EPN256::detail::UnmuxPin;
using GHIElectronics::Endpoint::Core::EPN256::detail::MuxPins;
using GHIElectronics::Endpoint::Core::EPN256::detail::UnmuxPins;
using GHIElectronics::Endpoint::Core::EPN256::detail::PullNone;
using GHIElectronics::Endpoint::Core::EPN256::detail::PullUp;
using GHIElectronics::Endpoint::Core::EPN256::detail::PullDown;

}  // namespace GHIElectronics::Endpoint::Core::EPN128::detail

namespace GHIElectronics::Endpoint::Core::EPN128::I2c {

// Only I2C4 and I2C5 are bonded out on Chili Pro.
inline constexpr int I2c4 = 4;  // PC0 (SDA), PC1 (SCL); /dev/i2c-4
inline constexpr int I2c5 = 5;  // PC4 (SDA), PC5 (SCL); /dev/i2c-5

inline constexpr int DefaultFrequency = 400'000;  // fast mode
inline constexpr int MinFrequency = 10'000;
inline constexpr int MaxFrequency = 1'000'000;    // fast mode plus

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(int bus) {
    if (bus != I2c4 && bus != I2c5)
        throw std::out_of_range("The I2C buses on EPN128 are I2c4 and I2c5");
}

// Multi-function pin value that selects I2C on these lines. Same table as
// .NET's EPN128.I2c.
inline constexpr int Mfp = 0x4;

struct BusPins { int scl; int sda; };

inline BusPins PinsOf(int bus) {
    return bus == I2c4 ? BusPins{Gpio::Pin::PC1, Gpio::Pin::PC0}
                       : BusPins{Gpio::Pin::PC5, Gpio::Pin::PC4};
}

// MMIO addresses from arch/arm64/.../ma35d0.dtsi.
inline std::string SpeedFile(int bus) {
    char path[96];
    unsigned long address = (bus == I2c4) ? 0x40840000UL : 0x40850000UL;
    std::snprintf(path, sizeof path,
                  "/sys/bus/platform/devices/%lx.i2c/clock_frequency", address);
    return path;
}

inline void SetFrequency(int bus, int frequency) {
    std::string path = SpeedFile(bus);
    std::ofstream f(path);
    if (f) f << frequency;
}

inline void Initialize(int bus, int frequency = DefaultFrequency) {
    Validate(bus);
    if (frequency < MinFrequency || frequency > MaxFrequency)
        throw std::out_of_range("I2C speeds are 10 kHz to 1 MHz");
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"i2c-ma35d1"});
            ModuleLoaded() = true;
        }
        if (OpenCount()[bus]++ == 0) {
            auto p = PinsOf(bus);
            detail::MuxPins({{p.scl, Mfp}, {p.sda, Mfp}}, detail::PullUp);
        }
    }
    char dev[32];
    std::snprintf(dev, sizeof dev, "/dev/i2c-%d", bus);
    if (!detail::WaitPath(dev, false)) {
        throw std::runtime_error(std::string("i2c-ma35d1 loaded but ") + dev +
                                 " did not appear within 1 s");
    }
    SetFrequency(bus, frequency);
}

inline void UnInitialize(int bus) {
    Validate(bus);
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(bus);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) {
        counts.erase(it);
        auto p = PinsOf(bus);
        detail::UnmuxPins({p.scl, p.sda});
    }
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"i2c-ma35d1"});
        ModuleLoaded() = false;
    }
}

inline int GetFrequency(int bus) {
    Validate(bus);
    std::string s = detail::ReadTrim(SpeedFile(bus));
    if (s.empty()) return DefaultFrequency;
    try { return std::stoi(s); } catch (...) { return DefaultFrequency; }
}

}  // namespace I2c

namespace GHIElectronics::Endpoint::Core::EPN128::Can {

// All four CAN controllers are bonded out on Chili Pro.
inline constexpr const char* Can0 = "can0";  // PC2 (RXD), PC3 (TXD)
inline constexpr const char* Can1 = "can1";  // PC6 (RXD), PC7 (TXD)
inline constexpr const char* Can2 = "can2";  // PA15 (RXD), PG1 (TXD)
inline constexpr const char* Can3 = "can3";  // PG8 (RXD), PG9 (TXD)

inline constexpr int MinBitrate = 10'000;
inline constexpr int MaxBitrate = 1'000'000;

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<std::string, int>& OpenCount() { static std::map<std::string, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(const std::string& iface) {
    if (iface != "can0" && iface != "can1" && iface != "can2" && iface != "can3")
        throw std::out_of_range("Must be EPN128::Can::Can0 / Can1 / Can2 / Can3");
}

// Multi-function pin values per bus. Same table as .NET's EPN128.Can.
struct PortPins { int rxd; int txd; int mfp; };

inline PortPins PinsOf(const std::string& iface) {
    if (iface == "can0") return {Gpio::Pin::PC2,  Gpio::Pin::PC3,  0x3};
    if (iface == "can1") return {Gpio::Pin::PC6,  Gpio::Pin::PC7,  0x3};
    if (iface == "can2") return {Gpio::Pin::PA15, Gpio::Pin::PG1,  0x5};
    return {Gpio::Pin::PG8, Gpio::Pin::PG9, 0x3};
}

inline void Initialize(const std::string& iface, int bitrate = 500'000) {
    Validate(iface);
    if (bitrate < MinBitrate || bitrate > MaxBitrate)
        throw std::out_of_range("CAN bitrate must be 10 kbit/s to 1 Mbit/s");
    bool first;
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"m_can_platform"});
            ModuleLoaded() = true;
        }
        first = OpenCount()[iface]++ == 0;
    }
    if (!detail::WaitPath("/sys/class/net/" + iface, true)) {
        throw std::runtime_error("m_can_platform loaded but " + iface +
                                 " did not appear within 1 s");
    }
    auto p = PinsOf(iface);
    if (first) detail::MuxPins({{p.rxd, p.mfp}, {p.txd, p.mfp}});
    char rate[16];
    std::snprintf(rate, sizeof rate, "%d", bitrate);
    try {
        detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "down"});
        detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "type", "can",
                                         "bitrate", rate});
        detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "up"});
    }
    catch (...) {
        if (first) detail::UnmuxPins({p.rxd, p.txd});
        throw;
    }
}

inline void UnInitialize(const std::string& iface) {
    if (iface != "can0" && iface != "can1" && iface != "can2" && iface != "can3") return;
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(iface);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) {
        counts.erase(it);
        detail::TryRun("/sbin/ip", {"link", "set", iface.c_str(), "down"});
        auto p = PinsOf(iface);
        detail::UnmuxPins({p.rxd, p.txd});
    }
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"m_can_platform"});
        ModuleLoaded() = false;
    }
}

}  // namespace Can

namespace GHIElectronics::Endpoint::Core::EPN128::SerialPort {

// User UARTs on Chili Pro. The console UART is reserved for the kernel
// and not exposed here. Unlike EPN256, EPN128 UARTs use the kernel's
// standard UART binding - no sysfs bind or modprobe needed.
inline constexpr const char* Uart4  = "/dev/ttyS4";   // PI10 (RXD), PI11 (TXD), PI8 (CTS), PI9 (RTS)
inline constexpr const char* Uart5  = "/dev/ttyS5";   // PG6 (RXD), PG7 (TXD); no flow-control pins bonded
inline constexpr const char* Uart7  = "/dev/ttyS7";   // PL14 (RXD), PG0 (TXD); no flow-control pins bonded
inline constexpr const char* Uart9  = "/dev/ttyS9";   // PG2 (RXD), PG3 (TXD); no flow-control pins bonded
inline constexpr const char* Uart13 = "/dev/ttyS13";  // PK4 (RXD), PG10 (TXD); no flow-control pins bonded

inline bool IsValidPort(const std::string& device) {
    return device == "/dev/ttyS4"  || device == "/dev/ttyS5" ||
           device == "/dev/ttyS7"  || device == "/dev/ttyS9" ||
           device == "/dev/ttyS13";
}

// RXD / TXD / RTS / CTS and the MFP they share (ma35d1-pinfunc.h). -1 means
// the signal is not bonded out on Chili Pro.
struct PortPins { int rxd, txd, rts, cts, mfp; };

inline PortPins PinsOf(const std::string& device) {
    using P = Gpio::Pin;
    if (device == "/dev/ttyS4") return {P::PI10, P::PI11, P::PI9, P::PI8, 0x2};
    if (device == "/dev/ttyS5") return {P::PG6,  P::PG7,  -1,     -1,     0x2};
    if (device == "/dev/ttyS7") return {P::PL14, P::PG0,  -1,     -1,     0x2};
    if (device == "/dev/ttyS9") return {P::PG2,  P::PG3,  -1,     -1,     0x2};
    return {P::PK4, P::PG10, -1, -1, 0x3};
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<std::string, int>& OpenCount() { static std::map<std::string, int> m; return m; }

// The ma35d1-uart driver auto-binds at boot, but the board DTS strips its
// pinctrl-0, so the pins stay free GPIO until Initialize selects them.
// enableHardwareFlowControl adds RTS and CTS where they are bonded out.
inline void Initialize(const std::string& device, bool enableHardwareFlowControl = false) {
    if (!IsValidPort(device))
        throw std::out_of_range(
            "Must be one of EPN128::SerialPort::Uart4 / Uart5 / Uart7 / Uart9 / Uart13");
    if (access(device.c_str(), F_OK) != 0) {
        throw std::runtime_error(device + " does not exist: this firmware is missing the uart.");
    }
    std::lock_guard lock(Gate());
    if (OpenCount()[device]++ != 0) return;
    auto p = PinsOf(device);
    try {
        detail::MuxPins({{p.rxd, p.mfp}, {p.txd, p.mfp}});
        if (enableHardwareFlowControl) {
            if (p.rts >= 0) detail::MuxPin(p.rts, p.mfp);
            if (p.cts >= 0) detail::MuxPin(p.cts, p.mfp);
        }
    }
    catch (...) {
        OpenCount().erase(device);
        throw;
    }
}

inline void UnInitialize(const std::string& device) {
    if (!IsValidPort(device)) return;
    std::lock_guard lock(Gate());
    auto cnt = OpenCount().find(device);
    if (cnt == OpenCount().end() || cnt->second <= 0) return;
    if (--cnt->second == 0) {
        OpenCount().erase(cnt);
        auto p = PinsOf(device);
        detail::UnmuxPins({p.rxd, p.txd});
        if (p.rts >= 0) detail::UnmuxPin(p.rts);
        if (p.cts >= 0) detail::UnmuxPin(p.cts);
    }
}

}  // namespace SerialPort

namespace GHIElectronics::Endpoint::Core::EPN128::Adc {

// MA35D0 ADC on Chili Pro via Linux IIO. 4-channel 10-bit ADC (compatible
// nuvoton,ma35d1-adc); only CH2 and CH3 are bonded out.
struct Pin {
    static constexpr int PB10 = 26;  // ADC0 channel 2
    static constexpr int PB11 = 27;  // ADC0 channel 3
};

// IIO name of the MA35D0 ADC. The ma35_adc driver sets
// indio_dev->name = dev_name(pdev), i.e. the DT node name.
inline constexpr const char* IioName = "40420000.adc";

inline int ChannelOf(int pin) {
    if (pin == Pin::PB10) return 2;
    if (pin == Pin::PB11) return 3;
    throw std::out_of_range("Must be EPN128::Adc::Pin::PB10 or PB11");
}

inline std::string ResolveIioDir() {
    DIR* dir = opendir("/sys/bus/iio/devices");
    if (!dir)
        throw std::runtime_error("IIO subsystem not available");
    while (auto* ent = readdir(dir)) {
        if (ent->d_name[0] == '.') continue;
        std::string dirName = ent->d_name;
        std::string name = detail::ReadTrim("/sys/bus/iio/devices/" + dirName + "/name");
        if (detail::ToLower(name) == detail::ToLower(std::string(IioName))) {
            closedir(dir);
            return "/sys/bus/iio/devices/" + dirName;
        }
    }
    closedir(dir);
    throw std::runtime_error(std::string("IIO device ") + IioName + " not found");
}

inline int ReadRaw(int pin) {
    int channel = ChannelOf(pin);
    std::string path = ResolveIioDir() + "/in_voltage" + std::to_string(channel) + "_raw";
    std::string s = detail::ReadTrim(path);
    if (s.empty())
        throw std::runtime_error("ADC channel not available: " + path);
    return std::stoi(s);
}

inline int ReadMillivolts(int pin) {
    int channel = ChannelOf(pin);
    std::string dir = ResolveIioDir();
    int raw = ReadRaw(pin);
    double scale = 3300.0 / 1024.0;  // 10-bit ADC fallback
    double offset = 0.0;
    auto try_read = [](const std::string& p, double& out) {
        std::string s = detail::ReadTrim(p);
        if (s.empty()) return false;
        try { out = std::stod(s); return true; } catch (...) { return false; }
    };
    double v;
    if (try_read(dir + "/in_voltage_scale", v)) scale = v;
    else if (try_read(dir + "/in_voltage" + std::to_string(channel) + "_scale", v)) scale = v;
    try_read(dir + "/in_voltage_offset", offset);
    return static_cast<int>((raw + offset) * scale + 0.5);
}

}  // namespace Adc

namespace GHIElectronics::Endpoint::Core::EPN128::Watchdog {

// Watchdog name string, for Devices::Watchdog::WatchdogController::FromName.
// Mirrors .NET's EPN128.Watchdog class.
inline constexpr const char* Id = "EPN128.Watchdog";

}  // namespace Watchdog

namespace GHIElectronics::Endpoint::Core::EPN128::StorageController {

// Storage controller name strings, for
// GHIElectronics::Endpoint::Devices::Storage::StorageController::FromName.
inline constexpr const char* SdCard             = "EPN128.StorageController.SdCard";
inline constexpr const char* UsbHostMassStorage = "EPN128.StorageController.UsbHostMassStorage";

}  // namespace StorageController
