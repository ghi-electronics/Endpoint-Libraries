#pragma once

#include <cstdint>
#include <mutex>

namespace GHIElectronics::Endpoint::Internal {

// As TinyCLR's WatchdogController (Endpoint's GHIElectronics.Endpoint.Devices.Watchdog).
// The chip's watchdog cannot stop once it runs: Disable hands it to the system,
// which then keeps it fed. Without Disable, the board restarts after the timeout.
class WatchdogImpl {
public:
    static constexpr int MaxTimeoutMs = 131'000;  // 4096 x 1024 ticks of the chip's 32 kHz clock

    WatchdogImpl() = default;
    ~WatchdogImpl();

    WatchdogImpl(const WatchdogImpl&) = delete;
    WatchdogImpl& operator=(const WatchdogImpl&) = delete;

    bool IsEnabled();
    // The system counts whole seconds: the timeout is rounded up
    void Enable(int timeoutMs);
    void Reset();
    void Disable();

private:
    void Feed();  // gate held

    std::mutex gate_;
    int fd_ = -1;
};

// The chip's RTC, as .NET IoT's RtcBase (Endpoint's GHIElectronics.Endpoint.Devices.Rtc).
// It counts UTC; times are seconds since 1970-01-01 UTC.
namespace Rtc {
int64_t ReadTime();
void SetTime(int64_t secondsUtc);
bool IsValid();
// TinyCLR's BatteryChargeMode: None = 0, Fast = 1, Slow = 2
void SetChargeMode(int chargeMode);
}  // namespace Rtc

}  // namespace GHIElectronics::Endpoint::Internal
