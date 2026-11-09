// EPN128 (Nuvoton MA35D0, Chili Pro) peripheral initializers. Mirrors
// .NET's GHIElectronics.Endpoint.Core.EPN128 and Python's EPN128 class so
// sample code reads the same shape in every language. MA35D0 and MA35D1
// share the same pinctrl layout; EPN128 is the subset bonded on Chili Pro
// (I2C4/I2C5, four CAN buses, five user UARTs, two ADC channels).
//
// Pay-for-use rule: the kernel drivers (i2c-ma35d1, m_can_platform) are
// built as modules; nothing probes at boot. Initialize modprobes the
// driver, which triggers the kernel's pinctrl framework to claim the
// peripheral's pins. UnInitialize unloads the driver (ref-counted across
// controllers) so pins return to free GPIO. SerialPort uses the kernel's
// standard UART binding - no modprobe/bind needed, pinctrl claims the
// TX/RX pins at Initialize.
//
//   using namespace GHIElectronics::Endpoint::Core::EPN128;
//   I2c::Initialize(I2c::I2c4);
//   int fd = open("/dev/i2c-4", O_RDWR);
//   ...
//   close(fd);
//   I2c::UnInitialize(I2c::I2c4);

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

namespace GHIElectronics::Endpoint::Core::EPN128::detail {

// Alias EPN256's helpers so the rest of this header reads like EPN256.h.
using GHIElectronics::Endpoint::Core::EPN256::detail::RunChecked;
using GHIElectronics::Endpoint::Core::EPN256::detail::TryRun;
using GHIElectronics::Endpoint::Core::EPN256::detail::WaitPath;
using GHIElectronics::Endpoint::Core::EPN256::detail::ReadTrim;
using GHIElectronics::Endpoint::Core::EPN256::detail::ToLower;

}  // namespace GHIElectronics::Endpoint::Core::EPN128::detail

// GPIO pin constants + GetChipId / GetLineId live in Core.h as
// EPN128::Gpio (a struct), the same pattern as EPN256. Include Core.h
// to use them.

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
        OpenCount()[bus]++;
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
    if (--it->second == 0) counts.erase(it);
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

inline void Initialize(const std::string& iface, int bitrate = 500'000) {
    Validate(iface);
    if (bitrate < MinBitrate || bitrate > MaxBitrate)
        throw std::out_of_range("CAN bitrate must be 10 kbit/s to 1 Mbit/s");
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"m_can_platform"});
            ModuleLoaded() = true;
        }
        OpenCount()[iface]++;
    }
    if (!detail::WaitPath("/sys/class/net/" + iface, true)) {
        throw std::runtime_error("m_can_platform loaded but " + iface +
                                 " did not appear within 1 s");
    }
    char rate[16];
    std::snprintf(rate, sizeof rate, "%d", bitrate);
    detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "down"});
    detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "type", "can",
                                     "bitrate", rate});
    detail::RunChecked("/sbin/ip", {"link", "set", iface.c_str(), "up"});
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

inline void Initialize(const std::string& device, bool /*enableHardwareFlowControl*/ = false) {
    if (!IsValidPort(device))
        throw std::out_of_range(
            "Must be one of EPN128::SerialPort::Uart4 / Uart5 / Uart7 / Uart9 / Uart13");
    if (access(device.c_str(), F_OK) != 0) {
        throw std::runtime_error(device + " does not exist: this firmware is missing the uart.");
    }
}

inline void UnInitialize(const std::string& /*device*/) {
    // No-op: EPN128 UARTs use the kernel's standard UART binding, so there
    // is nothing to unbind on close.
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
