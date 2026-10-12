// The watchdog of Endpoint, with the same API as TinyCLR (and the .NET
// GHIElectronics.Endpoint.Devices.Watchdog):
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Watchdog;
//
//   auto watchdog = WatchdogController::FromName(EPM815::Watchdog::Id);
//   watchdog.Enable(std::chrono::seconds(10));
//   while (true) { DoWork(); watchdog.Reset(); }
//
// The chip's watchdog cannot be stopped once it runs: Disable hands it to the
// system, which then keeps it fed. If the program ends or crashes without
// Disable, the board restarts after the timeout, also when debugging stops.
#pragma once

#include <chrono>
#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Watchdog {

class WatchdogController {
public:
    static WatchdogController GetDefault() { return WatchdogController(); }

    static WatchdogController FromName(const std::string& name) {
        if (name != Core::EPM815::Watchdog::Id) {
            throw Core::ArgumentException("There is no watchdog named \"" + name + "\": use EPM815.Watchdog.Id");
        }
        return WatchdogController();
    }

    WatchdogController() {
        void* handle = nullptr;
        Core::detail::Check(endpoint_watchdog_create(&handle));
        handle_ = Handle(handle);
    }

    // 131 seconds (4096 x 1024 ticks of the chip's 32 kHz clock)
    std::chrono::milliseconds MaxTimeout() const {
        int milliseconds = 0;
        Core::detail::Check(endpoint_watchdog_max_timeout(&milliseconds));
        return std::chrono::milliseconds(milliseconds);
    }

    bool IsEnabled() const {
        int enabled = 0;
        Core::detail::Check(endpoint_watchdog_is_enabled(h(), &enabled));
        return enabled != 0;
    }

    // Starts the watchdog, or changes its timeout. The system counts whole
    // seconds: the timeout is rounded up.
    void Enable(std::chrono::milliseconds timeout) {
        Core::detail::Check(endpoint_watchdog_enable(h(), static_cast<int>(timeout.count())));
    }

    // Starts the timeout again: call it more often than the timeout
    void Reset() { Core::detail::Check(endpoint_watchdog_reset(h())); }

    // Stops watching this program: the system keeps the watchdog fed from now on
    void Disable() { Core::detail::Check(endpoint_watchdog_disable(h())); }

    // An enabled watchdog stays enabled: call Disable first to avoid a restart
    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_watchdog_dispose>;

    void* h() const { return handle_.Get("WatchdogController"); }

    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Watchdog
