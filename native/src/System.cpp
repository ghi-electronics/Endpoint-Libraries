#include "System.h"

#include <fcntl.h>
#include <linux/rtc.h>
#include <linux/watchdog.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <cerrno>
#include <chrono>
#include <ctime>
#include <string>
#include <thread>

#include "EndpointDevices.h"
#include "Errors.h"
#include "PhysicalMemory.h"

namespace GHIElectronics::Endpoint::Internal {

// ---------------------------------------------------------------- Watchdog

namespace {

constexpr uint64_t WatchdogAddress = 0x5A002000;  // IWDG2, the watchdog that programs use

std::string WatchdogDevice() {
    auto number = EndpointDevices::Find("watchdog", "watchdog", WatchdogAddress);
    if (number < 0) {
        ThrowInvalidOperation("The watchdog is not enabled in this firmware: update the board's firmware");
    }
    return "/dev/watchdog" + std::to_string(number);
}

}  // namespace

WatchdogImpl::~WatchdogImpl() {
    // An enabled watchdog stays enabled: with nobody to reset it, the board
    // restarts after the timeout (as on TinyCLR). Disable first to avoid that.
    if (fd_ >= 0) {
        close(fd_);
    }
}

bool WatchdogImpl::IsEnabled() {
    std::lock_guard lock(gate_);
    return fd_ >= 0;
}

void WatchdogImpl::Enable(int timeoutMs) {
    if (timeoutMs <= 0 || timeoutMs > MaxTimeoutMs) {
        ThrowArgumentOutOfRange("The timeout must be more than 0 and at most 131 seconds");
    }
    int seconds = (timeoutMs + 999) / 1000;

    std::lock_guard lock(gate_);
    if (fd_ < 0) {
        auto path = WatchdogDevice();
        // Opening the device starts the watchdog
        auto fd = open(path.c_str(), O_WRONLY | O_CLOEXEC);
        if (fd < 0) {
            if (errno == EBUSY) {
                ThrowInvalidOperation("The watchdog is already enabled by another program or WatchdogController");
            }
            ThrowSystemError("Cannot open " + path);
        }
        fd_ = fd;
    }
    if (ioctl(fd_, WDIOC_SETTIMEOUT, &seconds) < 0) {
        ThrowSystemError("Cannot set the watchdog's timeout");
    }
    Feed();
}

void WatchdogImpl::Reset() {
    std::lock_guard lock(gate_);
    if (fd_ < 0) {
        ThrowInvalidOperation("The watchdog is not enabled: call Enable first");
    }
    Feed();
}

void WatchdogImpl::Disable() {
    std::lock_guard lock(gate_);
    if (fd_ < 0) {
        return;
    }
    // "V" before closing tells the system that the stop is on purpose
    (void)!write(fd_, "V", 1);
    close(fd_);
    fd_ = -1;
}

void WatchdogImpl::Feed() {
    int unused = 0;
    if (ioctl(fd_, WDIOC_KEEPALIVE, &unused) < 0) {
        ThrowSystemError("Cannot reset the watchdog");
    }
}

// ---------------------------------------------------------------- RTC

namespace Rtc {

namespace {

constexpr uint64_t RtcAddress = 0x5C004000;

// PWR_CR3: VBE switches VBAT charging on, VBRS picks 1.5 kOhm instead of 5 kOhm
constexpr uint64_t PwrCr3 = 0x5000100C;
constexpr uint32_t ChargeEnable = 1u << 8, ChargeFast = 1u << 9;

std::string Device() {
    auto number = EndpointDevices::Find("rtc", "rtc", RtcAddress);
    if (number < 0) {
        ThrowInvalidOperation("The RTC is not enabled in this firmware: update the board's firmware");
    }
    return "/dev/rtc" + std::to_string(number);
}

// The system lets one program at a time open the RTC: open it only for
// each call, and wait a little if another program has it
void Call(unsigned long request, void* argument, const char* what) {
    auto device = Device();
    for (int attempt = 0;; attempt++) {
        auto fd = open(device.c_str(), O_RDONLY | O_CLOEXEC);
        if (fd >= 0) {
            auto result = ioctl(fd, request, argument);
            auto error = errno;
            close(fd);
            if (result < 0) {
                errno = error;
                ThrowSystemError(std::string("Cannot ") + what + " the RTC");
            }
            return;
        }
        if (errno != EBUSY || attempt == 100) {
            ThrowSystemError("Cannot open " + device);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
}

rtc_time Read() {
    rtc_time t{};
    Call(RTC_RD_TIME, &t, "read");
    return t;
}

}  // namespace

int64_t ReadTime() {
    auto t = Read();
    std::tm tm{};
    tm.tm_sec = t.tm_sec;
    tm.tm_min = t.tm_min;
    tm.tm_hour = t.tm_hour;
    tm.tm_mday = t.tm_mday;
    tm.tm_mon = t.tm_mon;
    tm.tm_year = t.tm_year;
    return static_cast<int64_t>(timegm(&tm));
}

void SetTime(int64_t secondsUtc) {
    auto seconds = static_cast<time_t>(secondsUtc);
    std::tm tm{};
    if (!gmtime_r(&seconds, &tm) || tm.tm_year + 1900 < 2000 || tm.tm_year + 1900 > 2099) {
        ThrowArgumentOutOfRange("The RTC counts from 2000 to 2099");
    }
    rtc_time t{};
    t.tm_sec = tm.tm_sec;
    t.tm_min = tm.tm_min;
    t.tm_hour = tm.tm_hour;
    t.tm_mday = tm.tm_mday;
    t.tm_mon = tm.tm_mon;
    t.tm_year = tm.tm_year;
    t.tm_wday = tm.tm_wday;
    t.tm_yday = tm.tm_yday;
    Call(RTC_SET_TIME, &t, "set");
}

// Without power the RTC starts again at 2000-01-01
bool IsValid() { return Read().tm_year > 100; }

void SetChargeMode(int chargeMode) {
    uint32_t bits;
    switch (chargeMode) {
        case 0: bits = 0; break;
        case 1: bits = ChargeEnable | ChargeFast; break;
        case 2: bits = ChargeEnable; break;
        default: ThrowArgumentOutOfRange("The charge modes are None, Fast and Slow");
    }
    PhysicalMemory::Modify(PwrCr3, ChargeEnable | ChargeFast, bits);
}

}  // namespace Rtc

}  // namespace GHIElectronics::Endpoint::Internal
