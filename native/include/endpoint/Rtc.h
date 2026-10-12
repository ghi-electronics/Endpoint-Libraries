// The real-time clock of Endpoint, with the same API as .NET IoT's RtcBase
// (and the .NET GHIElectronics.Endpoint.Devices.Rtc), plus TinyCLR's
// IsValid and SetChargeMode:
//
//   using namespace GHIElectronics::Endpoint::Devices::Rtc;
//
//   RtcController rtc;
//   if (!rtc.IsValid()) {
//       rtc.DateTime(std::chrono::system_clock::now());   // set it once; a backup battery keeps it
//   }
//   auto now = rtc.DateTime();
//
// It counts UTC: at boot, the system time is set from it.
#pragma once

#include <chrono>
#include <cstdint>
#include <span>

#include "Core.h"

namespace GHIElectronics::Endpoint::Devices::Rtc {

// How the backup battery or supercapacitor (VBAT) is charged
enum class BatteryChargeMode {
    None = 0,
    Fast = 1,  // through 1.5 kOhm
    Slow = 2,  // through 5 kOhm
};

class RtcController {
public:
    using TimePoint = std::chrono::system_clock::time_point;

    // The time (UTC); .NET's DateTimeOffset
    TimePoint DateTime() const { return RtcDateTime(); }
    void DateTime(TimePoint value) { RtcDateTime(value); }

    // The RTC's own time (UTC)
    TimePoint RtcDateTime() const {
        int64_t seconds = 0;
        Core::detail::Check(endpoint_rtc_read_time(&seconds));
        return TimePoint(std::chrono::seconds(seconds));
    }
    void RtcDateTime(TimePoint value) {
        auto seconds = std::chrono::duration_cast<std::chrono::seconds>(value.time_since_epoch()).count();
        Core::detail::Check(endpoint_rtc_set_time(seconds));
    }

    // Whether the RTC has been set since it last lost power (without power it
    // starts again at 2000-01-01)
    bool IsValid() const {
        int valid = 0;
        Core::detail::Check(endpoint_rtc_is_valid(&valid));
        return valid != 0;
    }

    // Charges the backup battery or supercapacitor on VBAT, or stops charging
    void SetChargeMode(BatteryChargeMode chargeMode) {
        Core::detail::Check(endpoint_rtc_set_charge_mode(static_cast<int>(chargeMode)));
    }

    // TODO: backup memory and Calibrate (TinyCLR API), as in .NET
    uint32_t BackupMemorySize() const { NotImplemented(); }
    void WriteBackupMemory(std::span<const uint8_t>, uint32_t = 0) { NotImplemented(); }
    int ReadBackupMemory(std::span<uint8_t>, uint32_t = 0) { NotImplemented(); }
    void Calibrate(int) { NotImplemented(); }

    void Dispose() {}

private:
    [[noreturn]] static void NotImplemented() { Core::detail::Check(endpoint_rtc_not_implemented()); throw Core::NotImplementedException("Not implemented"); }
};

}  // namespace GHIElectronics::Endpoint::Devices::Rtc
