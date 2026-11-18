// GHI Electronics Endpoint for C++: chip-neutral machinery - exceptions,
// Event, the Chip enum, ChipDetect, and the libendpoint error bridge.
// Mirrors .NET's GHIElectronics.Endpoint.Core.
//
// Board pin constants and peripheral initializers live in the Pins
// headers (EPM815.h, EPDomino24.h, EPN256.h, EPN128.h), or pull them all
// in with #include <endpoint/Pins.h>. User code should prefer the
// chip-neutral controllers (I2cController, SpiController, ...) which
// dispatch to the running board automatically.
#pragma once

#include <cctype>
#include <fstream>
#include <functional>
#include <iterator>
#include <map>
#include <mutex>
#include <stdexcept>
#include <string>
#include <vector>

#include "detail/CApi.h"

namespace GHIElectronics::Endpoint::Core {

// The errors: the same names as the .NET exceptions, and also the matching
// standard C++ ones (an ArgumentException is a std::invalid_argument)
class ArgumentException : public std::invalid_argument {
public:
    using std::invalid_argument::invalid_argument;
};
class ArgumentOutOfRangeException : public ArgumentException {
public:
    using ArgumentException::ArgumentException;
};
class InvalidOperationException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class IOException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class NotSupportedException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class ObjectDisposedException : public InvalidOperationException {
public:
    using InvalidOperationException::InvalidOperationException;
};
class NotImplementedException : public std::logic_error {
public:
    using std::logic_error::logic_error;
};
class TimeoutException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};

// A .NET-style event: handlers are added with += and removed with -=.
// C++ cannot compare functions, so += gives an id for -=:
//
//   int id = (usb.ConnectionChanged += handler);
//   usb.ConnectionChanged -= id;
//
// The handlers run on the thread that raises the event (the library's).
template <typename... Args>
class Event {
public:
    using Handler = std::function<void(Args...)>;

    Event() = default;
    Event(const Event&) = delete;
    Event& operator=(const Event&) = delete;

    int operator+=(Handler handler) {
        std::lock_guard lock(mutex_);
        handlers_[++lastId_] = std::move(handler);
        return lastId_;
    }

    Event& operator-=(int id) {
        std::lock_guard lock(mutex_);
        handlers_.erase(id);
        return *this;
    }

    // Raises the event: every handler, in the order they were added (used by the library)
    void operator()(Args... args) const {
        std::vector<Handler> handlers;
        {
            std::lock_guard lock(mutex_);
            for (const auto& [id, handler] : handlers_) {
                handlers.push_back(handler);
            }
        }
        for (const auto& handler : handlers) {
            handler(args...);
        }
    }

private:
    mutable std::mutex mutex_;
    std::map<int, Handler> handlers_;
    int lastId_ = 0;
};

// A device connected or disconnected (StorageController, UsbHostController)
enum class DeviceConnectionStatus {
    Disconnected = 0,
    Connected = 1,
    Bad = 2,  // a drive with no file system the board reads (format it as FAT32 or exFAT)
};

// The chip the library is running on, read once from
// /proc/device-tree/model + /proc/device-tree/compatible at first use.
// Mirrors .NET's GHIElectronics.Endpoint.Core.Internal.ChipDetect; chip
// dispatch inside the library (pin muxing, device paths) uses this.
enum class Chip {
    Unknown = 0,  // dev host without a Linux device tree
    Epm815 = 1,   // STM32MP153 (EPM815 family)
    Epn256 = 2,   // Nuvoton MA35D1 (EPN256 family)
    Epn128 = 3,   // Nuvoton MA35D0 (EPN128 / Chili Pro)
};

namespace ChipDetect {

namespace detail {

// Read a /proc/device-tree node file and strip the trailing NUL that the
// kernel appends, so Contains-style substring matches behave intuitively.
inline std::string ReadDtNodeText(const char* path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) return {};
    std::string s((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
    while (!s.empty() && s.back() == '\0') s.pop_back();
    return s;
}

inline bool Contains(const std::string& haystack, const char* needle) {
    // Case-insensitive substring match: /proc/device-tree/model is mixed case.
    std::string h; h.reserve(haystack.size());
    for (char c : haystack) h.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(c))));
    std::string n;
    for (const char* p = needle; *p; ++p) n.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(*p))));
    return h.find(n) != std::string::npos;
}

inline Chip Detect() {
    // /proc/device-tree/model carries the board/SoC name verbatim ("Nuvoton
    // MA35D05K-IoT" on Chili Pro, "Nuvoton MA35D1 SoM ..." on EPN256). Use
    // it first because /proc/device-tree/compatible on MA35D0 reports just
    // "nuvoton,ma35d1" - the family string - which would send MA35D0 down
    // the MA35D1 path. Check "MA35D0" first (matches "MA35D05K"/"MA35D06K")
    // before "MA35D1" fallback.
    std::string model = ReadDtNodeText("/proc/device-tree/model");
    if (!model.empty()) {
        if (Contains(model, "MA35D0")) return Chip::Epn128;
        if (Contains(model, "MA35D1")) return Chip::Epn256;
        if (Contains(model, "STM32MP15")) return Chip::Epm815;
    }
    std::string compat = ReadDtNodeText("/proc/device-tree/compatible");
    if (!compat.empty()) {
        if (Contains(compat, "ma35d0")) return Chip::Epn128;
        if (Contains(compat, "ma35d1")) return Chip::Epn256;
        if (Contains(compat, "stm32mp15")) return Chip::Epm815;
    }
    return Chip::Unknown;
}

}  // namespace detail

// The detected chip, cached for the life of the process (Meyers singleton).
// Thread-safe one-time init on first call.
inline Chip Current() {
    static const Chip chip = detail::Detect();
    return chip;
}

// True for any Nuvoton MA35 chip (EPN256 or EPN128).
inline bool IsMa35() { return Current() == Chip::Epn256 || Current() == Chip::Epn128; }

// True for the STM32MP1 family (EPM815).
inline bool IsStm32Mp1() { return Current() == Chip::Epm815; }

}  // namespace ChipDetect

namespace detail {

// Throws the error libendpoint reported (its kind and message)
[[noreturn]] inline void Throw(int kind) {
    std::string message = endpoint_last_error();
    switch (kind) {
        case 1: throw ArgumentException(message);
        case 2: throw ArgumentOutOfRangeException(message);
        case 3: throw InvalidOperationException(message);
        case 5: throw NotSupportedException(message);
        case 6: throw ObjectDisposedException(message);
        case 7: throw NotImplementedException(message);
        case 8: throw TimeoutException(message);
        default: throw IOException(message);
    }
}

inline void Check(int result) {
    if (result != 0) {
        Throw(result);
    }
}

}  // namespace detail

}  // namespace GHIElectronics::Endpoint::Core
