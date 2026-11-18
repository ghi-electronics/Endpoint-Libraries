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
//   using namespace GHIElectronics::Endpoint::Core;
//   SpiController::Initialize(EPN256::Spi::Spi0);
//   int fd = open("/dev/spidev0.0", O_RDWR);
//   ...
//   close(fd);
//   SpiController::UnInitialize(EPN256::Spi::Spi0);

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
#include <vector>

#include "Core.h"

namespace GHIElectronics::Endpoint::Core::EPN256 {

// GPIO pins, for Devices::Gpio::GpioController
struct Gpio {
    // The EPN256's pins: port * 16 + line, ports A to N
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

    // The pin's name, for example "PJ14"
    static std::string Name(int pin) {
        if (pin < 0 || pin > Pin::PN15) {
            return "#" + std::to_string(pin);
        }
        int port = pin / 16;
        return std::string("P") + static_cast<char>('A' + port) + std::to_string(pin % 16);
    }

    // Linux gpiochip number for the pin. On EPN256 each port is its own
    // gpiochip (A..N = 0..13).
    static int GetChipId(int pin) { return pin / 16; }

    // Line number within the gpiochip (0 to 15 on EPN256).
    static int GetLineId(int pin) { return pin % 16; }
};

}  // namespace GHIElectronics::Endpoint::Core::EPN256

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

// MMIO base per I2C controller (arch/arm64/.../ma35d1.dtsi). Indexed by bus-1.
inline constexpr unsigned long Addresses[] = {
    0x40810000UL, 0x40820000UL, 0x40830000UL, 0x40840000UL, 0x40850000UL,
};

// Current bus speed in Hz. Returns DefaultFrequency if the driver does not expose it.
inline int GetFrequency(int bus) {
    Validate(bus);
    char path[96];
    std::snprintf(path, sizeof path,
                  "/sys/bus/platform/devices/%lx.i2c/clock_frequency", Addresses[bus - 1]);
    std::ifstream f(path);
    int freq = 0;
    if (f && (f >> freq) && freq > 0) return freq;
    return DefaultFrequency;
}

}  // namespace I2c

namespace GHIElectronics::Endpoint::Core::EPN256::Pwm {

// PWM pins bundled with EPN256, for Devices::Pwm::PwmChannel. MA35D1 has
// three EPWM controllers (EPWM0 / EPWM1 / EPWM2), each exposing six
// channels (CH0..CH5) that share the controller's frequency. Initialize
// connects a pin to its controller + channel; GetChipId and GetChannelId
// give what PwmChannel::Create needs. Pay-for-use: initializing one pin
// claims only that pin. Mirrors .NET's EPN256.Pwm.
//
//   EPN256::Pwm::Initialize(EPN256::Pwm::Pin::PK5);
//   int chip    = EPN256::Pwm::GetChipId(EPN256::Pwm::Pin::PK5);
//   int channel = EPN256::Pwm::GetChannelId(EPN256::Pwm::Pin::PK5);
struct Pin {
    // One representative pin per (controller, channel); if your board wires
    // a different pin alternate, request it and we'll add it.
    static constexpr int PD6  = 54;   // EPWM0 channel 0
    static constexpr int PD7  = 55;   // EPWM0 channel 1
    static constexpr int PD8  = 56;   // EPWM0 channel 2
    static constexpr int PD9  = 57;   // EPWM0 channel 3
    static constexpr int PD10 = 58;   // EPWM0 channel 4
    static constexpr int PD11 = 59;   // EPWM0 channel 5
    static constexpr int PG4  = 100;  // EPWM1 channel 0
    static constexpr int PK5  = 165;  // EPWM1 channel 1 (the EPN256 reference panel's backlight pin)
    static constexpr int PG6  = 102;  // EPWM1 channel 2
    static constexpr int PG7  = 103;  // EPWM1 channel 3
    static constexpr int PG8  = 104;  // EPWM1 channel 4
    static constexpr int PM13 = 205;  // EPWM1 channel 5
    static constexpr int PC8  = 40;   // EPWM2 channel 0
    static constexpr int PC9  = 41;   // EPWM2 channel 1
    static constexpr int PC10 = 42;   // EPWM2 channel 2
    static constexpr int PC11 = 43;   // EPWM2 channel 3
    static constexpr int PB9  = 25;   // EPWM2 channel 4
    static constexpr int PB10 = 26;   // EPWM2 channel 5
};

// MMIO base per EPWM controller (arch/arm64/.../ma35d1.dtsi). Pin MFPs
// are set by the kernel's pinctrl framework through the pwm DTS groups;
// userspace only needs the pin -> (controller, channel) map.
inline std::array<const char*, 3> PlatformDevices() {
    return {"40580000.epwm0", "40590000.epwm1", "405a0000.epwm2"};
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

// Pin -> (controller 0..2, channel 0..5). Returns true if the pin is a
// known PWM pin on EPN256.
inline bool LookupOutput(int pin, int& controller, int& channel) {
    switch (pin) {
        case Pin::PD6:  controller = 0; channel = 0; return true;
        case Pin::PD7:  controller = 0; channel = 1; return true;
        case Pin::PD8:  controller = 0; channel = 2; return true;
        case Pin::PD9:  controller = 0; channel = 3; return true;
        case Pin::PD10: controller = 0; channel = 4; return true;
        case Pin::PD11: controller = 0; channel = 5; return true;
        case Pin::PG4:  controller = 1; channel = 0; return true;
        case Pin::PK5:  controller = 1; channel = 1; return true;
        case Pin::PG6:  controller = 1; channel = 2; return true;
        case Pin::PG7:  controller = 1; channel = 3; return true;
        case Pin::PG8:  controller = 1; channel = 4; return true;
        case Pin::PM13: controller = 1; channel = 5; return true;
        case Pin::PC8:  controller = 2; channel = 0; return true;
        case Pin::PC9:  controller = 2; channel = 1; return true;
        case Pin::PC10: controller = 2; channel = 2; return true;
        case Pin::PC11: controller = 2; channel = 3; return true;
        case Pin::PB9:  controller = 2; channel = 4; return true;
        case Pin::PB10: controller = 2; channel = 5; return true;
        default: return false;
    }
}

inline void ValidatePin(int pin, int& controller, int& channel) {
    if (!LookupOutput(pin, controller, channel))
        throw std::out_of_range(std::string("Pin ") + std::to_string(pin) + " has no PWM on EPN256");
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

inline void Initialize(int pin) {
    int controller, channel;
    ValidatePin(pin, controller, channel);
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"pwm-ma35d1"});
            ModuleLoaded() = true;
        }
        OpenCount()[pin]++;
    }
    for (int i = 0; i < 50; i++) {
        int chip;
        if (TryGetChipId(controller, chip)) return;
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
    throw std::runtime_error(std::string("pwm-ma35d1 loaded but no pwmchip matched ") +
                             PlatformDevices()[controller] + " within 1 s");
}

inline void UnInitialize(int pin) {
    int controller, channel;
    ValidatePin(pin, controller, channel);
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(pin);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) counts.erase(it);
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"pwm-ma35d1"});
        ModuleLoaded() = false;
    }
}

inline int GetChipId(int pin) {
    int controller, channel;
    ValidatePin(pin, controller, channel);
    int chip;
    if (!TryGetChipId(controller, chip))
        throw std::runtime_error(std::string("EPWM") + std::to_string(controller) +
                                 " is not enabled in this firmware; call Initialize first.");
    return chip;
}

inline int GetChannelId(int pin) {
    int controller, channel;
    ValidatePin(pin, controller, channel);
    return channel;
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

// enableHardwareFlowControl is accepted for signature parity with the chip-neutral
// SerialPortController. On EPN256, pin muxing (including RTS/CTS when declared in
// the board DTS) happens via platform-driver bind at probe time, so the flag has
// no runtime effect here. Boards without RTS/CTS lines bonded (UART12, UART14 CTS)
// simply have no DTS pinctrl entry for those lines.
inline void Initialize(const std::string& device, bool enableHardwareFlowControl = false) {
    (void)enableHardwareFlowControl;
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
