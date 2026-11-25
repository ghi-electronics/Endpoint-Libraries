// EPN256 (Nuvoton MA35D1) peripheral initializers. Mirrors .NET's
// GHIElectronics.Endpoint.Core.EPN256 and Python's EPN256 class so sample
// code reads the same shape in every language.
//
// Pay-for-use rule: the kernel drivers (spi-ma35d1-spi, pwm-ma35d1,
// m_can_platform, i2c-ma35d1) are built as modules and the board DTS strips
// pinctrl-0 from every peripheral node, so nothing probes and nothing mux's
// at boot. Initialize modprobes the driver and then selects the peripheral's
// pins through endpoint-pinmux; UnInitialize puts those pins back as it
// found them and unloads the driver (ref-counted across controllers). Call
// Initialize before using a Linux API (open /dev/spidev0.0, read
// /sys/class/pwm/...), call UnInitialize when done.
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
#include <utility>
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

    // User LED on PJ14
    static constexpr int Led = Pin::PJ14;

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

// ---------- pin muxing ----------
// The board DTS strips pinctrl-0 from every peripheral node, so the kernel
// never mux's a pin behind our back: binding a driver is not enough, the
// library has to select the pin's MFP itself. Everything goes through
// /usr/sbin/endpoint-pinmux, which also stashes the pre-Initialize state in
// /run so UnInitialize can put the pin back. Same contract as .NET's
// PeripheralPins + Ma35dPins and Python's Pinmux.

inline constexpr const char* PinmuxPath = "/usr/sbin/endpoint-pinmux";

inline constexpr int PullNone = 0, PullUp = 1, PullDown = 2;

// MA35D function packing, matching Endpoint-Firmware/pinmux/src/ma35d.c:
//   [Slew:2][Pull:2][Mode:2][MFP:4]. Mode stays Input; the peripheral owns
//   the pin once MFP is non-zero.
inline unsigned PackFunction(int mfp, int pull) {
    return ((static_cast<unsigned>(pull) & 0x3u) << 6) | (static_cast<unsigned>(mfp) & 0xFu);
}

// Select the pin's peripheral function, remembering what it was.
inline void MuxPin(int pin, int mfp, int pull = PullNone) {
    char p[16], f[16];
    std::snprintf(p, sizeof p, "%d", pin);
    std::snprintf(f, sizeof f, "0x%X", PackFunction(mfp, pull));
    RunChecked(PinmuxPath, {"save", p});
    RunChecked(PinmuxPath, {"set", p, f});
}

// Put the pin back as MuxPin found it. Best effort: UnInitialize must not
// throw over a pin that was never muxed.
inline void UnmuxPin(int pin) {
    char p[16];
    std::snprintf(p, sizeof p, "%d", pin);
    TryRun(PinmuxPath, {"restore", p});
}

// All or nothing: if one pin fails to mux, roll back the ones already done.
inline void MuxPins(const std::vector<std::pair<int, int>>& pinsAndMfps, int pull = PullNone) {
    std::vector<int> done;
    try {
        for (auto [pin, mfp] : pinsAndMfps) {
            MuxPin(pin, mfp, pull);
            done.push_back(pin);
        }
    }
    catch (...) {
        for (int pin : done) UnmuxPin(pin);
        throw;
    }
}

inline void UnmuxPins(const std::vector<int>& pins) {
    for (int pin : pins) UnmuxPin(pin);
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

// SS / CLK / MOSI / MISO per bus. All four pins of an MA35D1 SPI bus share
// MFP 0x5 (include/dt-bindings/pinctrl/ma35d1-pinfunc.h).
inline constexpr int Mfp = 0x5;

inline std::array<int, 4> PinsOf(int bus) {
    using P = EPN256::Gpio::Pin;
    switch (bus) {
        case Spi0: return {P::PB8, P::PB9, P::PB10, P::PB11};
        case Spi1: return {P::PC8, P::PC9, P::PC10, P::PC11};
        case Spi2: return {P::PG8, P::PG9, P::PG10, P::PK4};
        default:   return {P::PL8, P::PL9, P::PL4,  P::PL5};
    }
}

inline void Initialize(int bus) {
    Validate(bus);
    bool first;
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"spi-ma35d1-spi"});
            detail::RunChecked("/sbin/modprobe", {"spidev"});
            ModuleLoaded() = true;
        }
        first = OpenCount()[bus]++ == 0;
    }
    char dev[32];
    std::snprintf(dev, sizeof dev, "/dev/spidev%d.0", bus);
    if (!detail::WaitPath(dev, false)) {
        throw std::runtime_error(std::string("spi-ma35d1 loaded but ") + dev +
                                 " did not appear within 1 s");
    }
    if (first) {
        auto p = PinsOf(bus);
        detail::MuxPins({{p[0], Mfp}, {p[1], Mfp}, {p[2], Mfp}, {p[3], Mfp}});
    }
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
        detail::UnmuxPins({p[0], p[1], p[2], p[3]});
    }
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

// SCL / SDA and their MFP per bus (ma35d1-pinfunc.h). Both lines of a bus
// share one MFP value.
struct BusPins { int scl, sda, mfp; };

inline BusPins PinsOf(int bus) {
    using P = EPN256::Gpio::Pin;
    switch (bus) {
        case I2c1: return {P::PB11, P::PB10, 0xC};
        case I2c2: return {P::PB9,  P::PB8,  0x4};
        case I2c3: return {P::PM15, P::PM14, 0x6};
        case I2c4: return {P::PL5,  P::PL4,  0x4};
        default:   return {P::PJ13, P::PJ12, 0x4};
    }
}

inline void Initialize(int bus, int frequency = DefaultFrequency) {
    Validate(bus);
    if (frequency < MinFrequency || frequency > MaxFrequency)
        throw std::out_of_range("I2C speeds are 10 kHz to 1 MHz");
    bool first;
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"i2c-ma35d1"});
            ModuleLoaded() = true;
        }
        first = OpenCount()[bus]++ == 0;
    }
    char dev[32];
    std::snprintf(dev, sizeof dev, "/dev/i2c-%d", bus);
    if (!detail::WaitPath(dev, false)) {
        throw std::runtime_error(std::string("i2c-ma35d1 loaded but ") + dev +
                                 " did not appear within 1 s");
    }
    if (first) {
        auto p = PinsOf(bus);
        detail::MuxPins({{p.scl, p.mfp}, {p.sda, p.mfp}}, detail::PullUp);
    }
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

// MMIO base per EPWM controller (arch/arm64/.../ma35d1.dtsi).
inline std::array<const char*, 3> PlatformDevices() {
    return {"40580000.epwm0", "40590000.epwm1", "405a0000.epwm2"};
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<int, int>& OpenCount() { static std::map<int, int> m; return m; }
inline bool& ModuleLoaded() { static bool b = false; return b; }

// Pin -> (controller 0..2, channel 0..5, MFP). Returns true if the pin is a
// known PWM pin on EPN256. MFP values are from ma35d1-pinfunc.h: the EPWM0
// pins on port D use 0x8, everything else uses 0x1.
inline bool LookupOutput(int pin, int& controller, int& channel, int& mfp) {
    mfp = 0x1;
    switch (pin) {
        case Pin::PD6:  controller = 0; channel = 0; mfp = 0x8; return true;
        case Pin::PD7:  controller = 0; channel = 1; mfp = 0x8; return true;
        case Pin::PD8:  controller = 0; channel = 2; mfp = 0x8; return true;
        case Pin::PD9:  controller = 0; channel = 3; mfp = 0x8; return true;
        case Pin::PD10: controller = 0; channel = 4; mfp = 0x8; return true;
        case Pin::PD11: controller = 0; channel = 5; mfp = 0x8; return true;
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

inline void ValidatePin(int pin, int& controller, int& channel, int& mfp) {
    if (!LookupOutput(pin, controller, channel, mfp))
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
    int controller, channel, mfp;
    ValidatePin(pin, controller, channel, mfp);
    bool first;
    {
        std::lock_guard lock(Gate());
        if (!ModuleLoaded()) {
            detail::RunChecked("/sbin/modprobe", {"pwm-ma35d1"});
            ModuleLoaded() = true;
        }
        first = OpenCount()[pin]++ == 0;
    }
    for (int i = 0; i < 50; i++) {
        int chip;
        if (TryGetChipId(controller, chip)) {
            if (first) detail::MuxPin(pin, mfp);
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
    throw std::runtime_error(std::string("pwm-ma35d1 loaded but no pwmchip matched ") +
                             PlatformDevices()[controller] + " within 1 s");
}

inline void UnInitialize(int pin) {
    int controller, channel, mfp;
    ValidatePin(pin, controller, channel, mfp);
    std::lock_guard lock(Gate());
    auto& counts = OpenCount();
    auto it = counts.find(pin);
    if (it == counts.end() || it->second <= 0) return;
    if (--it->second == 0) {
        counts.erase(it);
        detail::UnmuxPin(pin);
    }
    if (counts.empty() && ModuleLoaded()) {
        detail::TryRun("/sbin/rmmod", {"pwm-ma35d1"});
        ModuleLoaded() = false;
    }
}

inline int GetChipId(int pin) {
    int controller, channel, mfp;
    ValidatePin(pin, controller, channel, mfp);
    int chip;
    if (!TryGetChipId(controller, chip))
        throw std::runtime_error(std::string("EPWM") + std::to_string(controller) +
                                 " is not enabled in this firmware; call Initialize first.");
    return chip;
}

inline int GetChannelId(int pin) {
    int controller, channel, mfp;
    ValidatePin(pin, controller, channel, mfp);
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

// RXD / TXD and their MFP per interface (ma35d1-pinfunc.h). can0 is the DTS
// alias for CAN1 silicon, can1 for CAN3.
struct PortPins { int rxd, txd, mfp; };

inline PortPins PinsOf(const std::string& iface) {
    using P = EPN256::Gpio::Pin;
    return iface == "can0" ? PortPins{P::PL14, P::PL15, 0x4}
                           : PortPins{P::PL10, P::PL11, 0x3};
}

inline void Initialize(const std::string& iface, int bitrate = 500'000) {
    Validate(iface);
    if (bitrate < 10'000 || bitrate > 1'000'000)
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
    if (iface != "can0" && iface != "can1") return;
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

namespace GHIElectronics::Endpoint::Core::EPN256::SerialPort {

inline constexpr const char* Uart5 = "/dev/ttyS5";
inline constexpr const char* Uart11 = "/dev/ttyS11";
inline constexpr const char* Uart12 = "/dev/ttyS12";
inline constexpr const char* Uart14 = "/dev/ttyS14";

inline const std::map<std::string, std::string>& DeviceName() {
    static const std::map<std::string, std::string> m = {
        {"/dev/ttyS5", "40750000.serial"},
        {"/dev/ttyS11", "407b0000.serial"},
        {"/dev/ttyS12", "407c0000.serial"},
        {"/dev/ttyS14", "407e0000.serial"},
    };
    return m;
}

// RXD / TXD / RTS / CTS and the MFP they share (ma35d1-pinfunc.h). -1 means
// the signal is not bonded out on EPN256.
struct PortPins { int rxd, txd, rts, cts, mfp; };

inline PortPins PinsOf(const std::string& device) {
    using P = EPN256::Gpio::Pin;
    if (device == "/dev/ttyS5")  return {P::PG6, P::PG7, P::PG5, P::PG4, 0x2};
    if (device == "/dev/ttyS11") return {P::PL2, P::PL3, P::PL1, P::PL0, 0x2};
    if (device == "/dev/ttyS12") return {P::PI2, P::PI3, P::PI1, -1,     0x2};
    return {P::PI6, P::PI7, P::PI5, -1, 0x2};
}

inline std::mutex& Gate() { static std::mutex g; return g; }
inline std::map<std::string, int>& OpenCount() { static std::map<std::string, int> m; return m; }

// The pins Initialize actually muxed, so UnInitialize releases those and no
// others: with flow control off, RTS / CTS were never saved and asking
// endpoint-pinmux to restore them prints an error.
inline std::map<std::string, std::vector<int>>& MuxedPins() {
    static std::map<std::string, std::vector<int>> m;
    return m;
}

inline void WriteSysfs(const std::string& path, const std::string& value) {
    std::string cmd = "echo " + value + " > " + path;
    detail::RunChecked("/bin/sh", {"-c", cmd.c_str()});
}

// enableHardwareFlowControl mux's RTS and CTS as well as RXD / TXD. UART12 and
// UART14 have no CTS pin bonded out, so CTS is skipped silently on those.
inline void Initialize(const std::string& device, bool enableHardwareFlowControl = false) {
    auto it = DeviceName().find(device);
    if (it == DeviceName().end())
        throw std::out_of_range("Must be EPN256::SerialPort::Uart5/Uart11/Uart12/Uart14");
    const std::string& platform = it->second;
    bool first;
    {
        std::lock_guard lock(Gate());
        int n = OpenCount()[device];
        std::string driverDev = "/sys/bus/platform/drivers/ma35d1-uart/" + platform;
        if (n == 0 && access(driverDev.c_str(), F_OK) != 0) {
            WriteSysfs("/sys/bus/platform/drivers/ma35d1-uart/bind", platform);
        }
        OpenCount()[device] = n + 1;
        first = n == 0;
    }
    if (!detail::WaitPath(device, false)) {
        throw std::runtime_error("Bound " + platform + " but " + device +
                                 " did not appear within 1 s");
    }
    if (!first) return;

    // The ma35d1-uart driver is built in and auto-binds at boot, but the board
    // DTS strips its pinctrl-0, so binding alone leaves the pins free GPIO.
    auto p = PinsOf(device);
    std::vector<std::pair<int, int>> pins{{p.rxd, p.mfp}, {p.txd, p.mfp}};
    if (enableHardwareFlowControl) {
        if (p.rts >= 0) pins.push_back({p.rts, p.mfp});
        if (p.cts >= 0) pins.push_back({p.cts, p.mfp});
    }
    detail::MuxPins(pins);

    std::vector<int> muxed;
    for (auto [pin, mfp] : pins) muxed.push_back(pin);
    std::lock_guard lock(Gate());
    MuxedPins()[device] = std::move(muxed);
}

// The pins are the pay-for-use resource and they go back to free GPIO here.
// The driver stays bound: unbinding would delete /dev/ttySN for every other
// process on the board until the next boot, and .NET's EPN256.SerialPort
// does not unbind either.
inline void UnInitialize(const std::string& device) {
    if (DeviceName().find(device) == DeviceName().end()) return;
    std::lock_guard lock(Gate());
    auto cnt = OpenCount().find(device);
    if (cnt == OpenCount().end() || cnt->second <= 0) return;
    if (--cnt->second == 0) {
        OpenCount().erase(cnt);
        auto muxed = MuxedPins().find(device);
        if (muxed != MuxedPins().end()) {
            detail::UnmuxPins(muxed->second);
            MuxedPins().erase(muxed);
        }
    }
}

}  // namespace SerialPort

namespace GHIElectronics::Endpoint::Core::EPN256::Adc {

// The analog inputs bundled with EPN256, for Devices::Adc::AdcController.
// MA35D1's EADC0 is the general-purpose 12-bit ADC, 8 channels on PB0..PB7.
// The resistive touchscreen uses a separate ADC, so the whole EADC range is
// free for application code. Mirrors .NET's EPN256.Adc.
//
//   AdcController adc;
//   auto input = adc.OpenPin(EPN256::Adc::Pin::PB0);
//   printf("%.3f V\n", input->ReadVoltage().Volts());
//
// The pin-to-channel map and the MFP values live in BoardSupportInit.cpp,
// next to the dispatch that consumes them.
struct Pin {
    static constexpr int PB0 = Gpio::Pin::PB0;  // EADC0 channel 0
    static constexpr int PB1 = Gpio::Pin::PB1;  // EADC0 channel 1
    static constexpr int PB2 = Gpio::Pin::PB2;  // EADC0 channel 2
    static constexpr int PB3 = Gpio::Pin::PB3;  // EADC0 channel 3
    static constexpr int PB4 = Gpio::Pin::PB4;  // EADC0 channel 4
    static constexpr int PB5 = Gpio::Pin::PB5;  // EADC0 channel 5
    static constexpr int PB6 = Gpio::Pin::PB6;  // EADC0 channel 6
    static constexpr int PB7 = Gpio::Pin::PB7;  // EADC0 channel 7
};

}  // namespace Adc

namespace GHIElectronics::Endpoint::Core::EPN256::StorageController {

// Storage controller name strings, for
// GHIElectronics::Endpoint::Devices::Storage::StorageController::FromName.
// Mirrors .NET's EPN256.StorageController class.
inline constexpr const char* SdCard             = "EPN256.StorageController.SdCard";
inline constexpr const char* UsbHostMassStorage = "EPN256.StorageController.UsbHostMassStorage";

}  // namespace StorageController
