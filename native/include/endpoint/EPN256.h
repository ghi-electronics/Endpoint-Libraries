// EPN256 (Nuvoton MA35D1) peripheral initializers. Mirrors .NET's
// GHIElectronics.Endpoint.Core.EPN256 and Python's EPN256 class so sample
// code reads the same shape in every language.
//
// Pay-for-use rule: the kernel drivers (spi-ma35d1-spi, pwm-ma35d1,
// m_can_platform, i2c-ma35d1) are built as modules; nothing probes at
// boot. Initialize modprobes the driver, which triggers the kernel's
// pinctrl framework to claim the peripheral's pins. UnInitialize unloads
// the driver (ref-counted across controllers) so pins return to free
// GPIO. Call Initialize before using a Linux API (open /dev/spidev0.0,
// read /sys/class/pwm/...), call UnInitialize when done.
//
//   using namespace GHIElectronics::Endpoint::Core::EPN256;
//   Spi::Initialize(Spi::Spi0);
//   int fd = open("/dev/spidev0.0", O_RDWR);
//   ...
//   close(fd);
//   Spi::UnInitialize(Spi::Spi0);

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

namespace GHIElectronics::Endpoint::Core::EPN256::detail {

inline void RunChecked(const char* path, std::initializer_list<const char*> args) {
    pid_t child = fork();
    if (child == 0) {
        std::vector<char*> argv;
        argv.push_back(const_cast<char*>(path));
        for (const char* a : args) argv.push_back(const_cast<char*>(a));
        argv.push_back(nullptr);
        execv(path, argv.data());
        _exit(127);
    }
    int status = 0;
    while (waitpid(child, &status, 0) < 0 && errno == EINTR) {}
    if (!WIFEXITED(status) || WEXITSTATUS(status) != 0) {
        std::string message = std::string(path);
        for (const char* a : args) { message += ' '; message += a; }
        throw std::runtime_error(message + " failed");
    }
}

inline void TryRun(const char* path, std::initializer_list<const char*> args) {
    pid_t child = fork();
    if (child == 0) {
        std::vector<char*> argv;
        argv.push_back(const_cast<char*>(path));
        for (const char* a : args) argv.push_back(const_cast<char*>(a));
        argv.push_back(nullptr);
        execv(path, argv.data());
        _exit(127);
    }
    while (waitpid(child, nullptr, 0) < 0 && errno == EINTR) {}
}

inline bool WaitPath(const std::string& path, bool is_dir, int timeout_ms = 1000) {
    auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_ms);
    while (true) {
        struct stat st{};
        if (stat(path.c_str(), &st) == 0) {
            if (is_dir ? S_ISDIR(st.st_mode) : (S_ISREG(st.st_mode) || S_ISCHR(st.st_mode) || S_ISBLK(st.st_mode))) {
                return true;
            }
        }
        if (std::chrono::steady_clock::now() >= deadline) return false;
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
}

inline std::string ReadTrim(const std::string& path) {
    std::ifstream f(path);
    std::string s((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
    while (!s.empty() && (s.back() == '\n' || s.back() == '\r' || s.back() == ' ' ||
                          s.back() == '\0' || s.back() == '\t')) s.pop_back();
    return s;
}

inline std::string ToLower(std::string s) {
    for (auto& c : s) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return s;
}

inline bool EndsWithDotName(const std::string& platform, const std::string& ofName) {
    auto expected = "." + ToLower(ofName);
    auto haystack = ToLower(platform);
    return haystack.size() >= expected.size() &&
        haystack.compare(haystack.size() - expected.size(), expected.size(), expected) == 0;
}

}  // namespace GHIElectronics::Endpoint::Core::EPN256::detail

namespace GHIElectronics::Endpoint::Core::EPN256::Spi {

inline constexpr int Spi0 = 0;  // PB8  (SS1), PB9  (CLK), PB10 (MOSI), PB11 (MISO)
inline constexpr int Spi1 = 1;  // PC8  (SS0), PC9  (CLK), PC10 (MOSI), PC11 (MISO)
inline constexpr int Spi2 = 2;  // PG8  (SS0), PG9  (CLK), PG10 (MOSI), PK4  (MISO)
inline constexpr int Spi3 = 3;  // PL8  (SS0), PL9  (CLK), PL4  (MOSI), PL5  (MISO)

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(int bus) {
    if (bus < 0 || bus > 3)
        throw std::out_of_range("Must be EPN256::Spi::Spi0/Spi1/Spi2/Spi3");
}

inline void Initialize(int bus) {
    Validate(bus);
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"spi-ma35d1-spi"});
            detail::RunChecked("/sbin/modprobe", {"spidev"});
            ModuleLoaded() = true;
        }
        OpenCount()[bus]++;
    }
    char dev[32];
    std::snprintf(dev, sizeof dev, "/dev/spidev%d.0", bus);
    if (!detail::WaitPath(dev, false)) {
        throw std::runtime_error(std::string("spi-ma35d1 loaded but ") + dev +
                                 " did not appear within 1 s");
    }
}

inline void UnInitialize(int bus) {
    Validate(bus);
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(bus);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) counts.erase(it);
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"spidev"});
        detail::TryRun("/sbin/rmmod", {"spi-ma35d1-spi"});
        ModuleLoaded() = false;
    }
}

}  // namespace Spi

namespace GHIElectronics::Endpoint::Core::EPN256::I2c {

inline constexpr int I2c1 = 1;
inline constexpr int I2c2 = 2;
inline constexpr int I2c3 = 3;
inline constexpr int I2c4 = 4;
inline constexpr int I2c5 = 5;

inline constexpr int DefaultFrequency = 400'000;
inline constexpr int MinFrequency = 10'000;
inline constexpr int MaxFrequency = 1'000'000;

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(int bus) {
    if (bus < I2c1 || bus > I2c5)
        throw std::out_of_range("Must be EPN256::I2c::I2c1..I2c5");
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

}  // namespace I2c

namespace GHIElectronics::Endpoint::Core::EPN256::Pwm {

inline constexpr int Epwm0 = 0;
inline constexpr int Epwm1 = 1;  // backlight on EPN256 reference panel
inline constexpr int Epwm2 = 2;

inline std::array<const char*, 3> PlatformDevices() {
    return {"40580000.epwm0", "40590000.epwm1", "405a0000.epwm2"};
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(int c) {
    if (c < 0 || c > 2)
        throw std::out_of_range("Must be EPN256::Pwm::Epwm0/Epwm1/Epwm2");
}

inline bool TryGetChipId(int controller, int& chipId) {
    DIR* dir = opendir("/sys/class/pwm");
    if (!dir) return false;
    std::string expected = PlatformDevices()[controller];
    while (auto* ent = readdir(dir)) {
        if (ent->d_name[0] == '.') continue;
        std::string chipName = ent->d_name;
        std::string ofName = detail::ReadTrim(std::string("/sys/class/pwm/") +
                                              chipName + "/device/of_node/name");
        if (!ofName.empty() && detail::EndsWithDotName(expected, ofName) &&
            chipName.rfind("pwmchip", 0) == 0) {
            try {
                chipId = std::stoi(chipName.substr(std::strlen("pwmchip")));
                closedir(dir);
                return true;
            } catch (...) {}
        }
    }
    closedir(dir);
    return false;
}

inline void Initialize(int controller) {
    Validate(controller);
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"pwm-ma35d1"});
            ModuleLoaded() = true;
        }
        OpenCount()[controller]++;
    }
    for (int i = 0; i < 50; i++) {
        int chip;
        if (TryGetChipId(controller, chip)) return;
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
    throw std::runtime_error(std::string("pwm-ma35d1 loaded but no pwmchip matched ") +
                             PlatformDevices()[controller] + " within 1 s");
}

inline void UnInitialize(int controller) {
    Validate(controller);
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(controller);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) counts.erase(it);
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"pwm-ma35d1"});
        ModuleLoaded() = false;
    }
}

inline int GetChipId(int controller) {
    Validate(controller);
    int chip;
    if (!TryGetChipId(controller, chip))
        throw std::runtime_error("EPN256::Pwm::Initialize must be called first");
    return chip;
}

}  // namespace Pwm

namespace GHIElectronics::Endpoint::Core::EPN256::Can {

inline constexpr const char* Can0 = "can0";
inline constexpr const char* Can1 = "can1";

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<std::string, int>& OpenCount() { static std::map<std::string, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

inline void Validate(const std::string& iface) {
    if (iface != "can0" && iface != "can1")
        throw std::out_of_range("Must be EPN256::Can::Can0 or Can1");
}

inline void Initialize(const std::string& iface, int bitrate = 500'000) {
    Validate(iface);
    if (bitrate < 10'000 || bitrate > 1'000'000)
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
    if (iface != "can0" && iface != "can1") return;
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

namespace GHIElectronics::Endpoint::Core::EPN256::SerialPort {

inline constexpr const char* Uart11 = "/dev/ttyS11";
inline constexpr const char* Uart12 = "/dev/ttyS12";
inline constexpr const char* Uart14 = "/dev/ttyS14";

inline const std::map<std::string, std::string>& DeviceName() {
    static const std::map<std::string, std::string> m = {
        {"/dev/ttyS11", "407b0000.serial"},
        {"/dev/ttyS12", "407c0000.serial"},
        {"/dev/ttyS14", "407e0000.serial"},
    };
    return m;
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<std::string, int>& OpenCount() { static std::map<std::string, int> m; return m; }

inline void WriteSysfs(const std::string& path, const std::string& value) {
    std::string cmd = "echo " + value + " > " + path;
    detail::RunChecked("/bin/sh", {"-c", cmd.c_str()});
}

inline void Initialize(const std::string& device) {
    auto it = DeviceName().find(device);
    if (it == DeviceName().end())
        throw std::out_of_range("Must be EPN256::SerialPort::Uart11/Uart12/Uart14");
    const std::string& platform = it->second;
    {
        std::lock_guard lock(Gate());
        int n = OpenCount()[device];
        std::string driverDev = "/sys/bus/platform/drivers/ma35d1-uart/" + platform;
        if (n == 0 && access(driverDev.c_str(), F_OK) != 0) {
            WriteSysfs("/sys/bus/platform/drivers/ma35d1-uart/bind", platform);
        }
        OpenCount()[device] = n + 1;
    }
    if (!detail::WaitPath(device, false)) {
        throw std::runtime_error("Bound " + platform + " but " + device +
                                 " did not appear within 1 s");
    }
}

inline void UnInitialize(const std::string& device) {
    auto it = DeviceName().find(device);
    if (it == DeviceName().end()) return;
    const std::string& platform = it->second;
    std::lock_guard lock(Gate());
    auto cnt = OpenCount().find(device);
    if (cnt == OpenCount().end() || cnt->second <= 0) return;
    if (--cnt->second == 0) {
        OpenCount().erase(cnt);
        try {
            WriteSysfs("/sys/bus/platform/drivers/ma35d1-uart/unbind", platform);
        } catch (...) {}
    }
}

}  // namespace SerialPort

namespace GHIElectronics::Endpoint::Core::EPN256::Adc {

inline constexpr const char* Eadc0 = "eadc0";

inline std::string ResolveIioDir(const std::string& bank) {
    if (bank != "eadc0")
        throw std::out_of_range("Only EPN256::Adc::Eadc0 is supported");
    DIR* dir = opendir("/sys/bus/iio/devices");
    if (!dir)
        throw std::runtime_error("IIO subsystem not available");
    while (auto* ent = readdir(dir)) {
        if (ent->d_name[0] == '.') continue;
        std::string dirName = ent->d_name;
        std::string name = detail::ReadTrim("/sys/bus/iio/devices/" + dirName + "/name");
        if (detail::ToLower(name) == "40430000.adc" || detail::ToLower(name) == "ma35d1-eadc") {
            closedir(dir);
            return "/sys/bus/iio/devices/" + dirName;
        }
    }
    closedir(dir);
    throw std::runtime_error("IIO device for eadc0 not found");
}

inline int ReadRaw(const std::string& bank, int channel) {
    std::string path = ResolveIioDir(bank) + "/in_voltage" + std::to_string(channel) + "_raw";
    std::string s = detail::ReadTrim(path);
    if (s.empty())
        throw std::runtime_error("ADC channel not available: " + path);
    return std::stoi(s);
}

inline int ReadMillivolts(const std::string& bank, int channel) {
    std::string dir = ResolveIioDir(bank);
    int raw = ReadRaw(bank, channel);
    double scale = 3300.0 / 4096.0;
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

namespace GHIElectronics::Endpoint::Core::EPN256::StorageController {

// Storage controller name strings, for
// GHIElectronics::Endpoint::Devices::Storage::StorageController::FromName.
// Mirrors .NET's EPN256.StorageController class.
inline constexpr const char* SdCard             = "EPN256.StorageController.SdCard";
inline constexpr const char* UsbHostMassStorage = "EPN256.StorageController.UsbHostMassStorage";

}  // namespace StorageController
