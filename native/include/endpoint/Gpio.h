// GPIO for Endpoint, with the same API as .NET's System.Device.Gpio:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Gpio;
//
//   GpioController gpio;
//   gpio.OpenPin(EPM815::Gpio::Pin::PC0, PinMode::Output);
//   gpio.Write(EPM815::Gpio::Pin::PC0, PinValue::High);
//
// Pins are the EPM815::Gpio::Pin constants. Open pins are reserved, so the
// other Endpoint classes cannot take them by mistake.
#pragma once

#include <chrono>
#include <functional>
#include <map>
#include <memory>
#include <mutex>

#include "Core.h"

namespace GHIElectronics::Endpoint::Devices::Gpio {

enum class PinMode { Input = 0, Output = 1, InputPullDown = 2, InputPullUp = 3 };

enum class PinValue { Low = 0, High = 1 };

enum class PinEventTypes { None = 0, Rising = 1, Falling = 2 };

inline PinEventTypes operator|(PinEventTypes a, PinEventTypes b) {
    return static_cast<PinEventTypes>(static_cast<int>(a) | static_cast<int>(b));
}
inline PinEventTypes operator&(PinEventTypes a, PinEventTypes b) {
    return static_cast<PinEventTypes>(static_cast<int>(a) & static_cast<int>(b));
}

struct PinValueChangedEventArgs {
    PinEventTypes ChangeType;
    int PinNumber;
};

struct WaitForEventResult {
    PinEventTypes EventTypes;
    bool TimedOut;
};

class GpioController;

// An open pin, as returned by GpioController::OpenPin
class GpioPin {
public:
    GpioPin(GpioController& controller, int pinNumber) : controller_(&controller), pinNumber_(pinNumber) {}

    int PinNumber() const { return pinNumber_; }
    PinMode GetPinMode() const;
    void SetPinMode(PinMode mode);
    bool IsPinModeSupported(PinMode mode) const;
    PinValue Read() const;
    void Write(PinValue value);
    void Toggle();

private:
    GpioController* controller_;
    int pinNumber_;
};

// GPIO on all of the EPM815's ports. The controller cannot be copied or
// moved (its callbacks refer to it); the destructor closes its pins.
class GpioController {
public:
    // .NET's PinChangeEventHandler(object sender, PinValueChangedEventArgs e)
    using PinChangeEventHandler = std::function<void(GpioController& sender, const PinValueChangedEventArgs& e)>;

    GpioController() { Core::detail::Check(endpoint_gpio_create(&handle_)); }
    ~GpioController() { Dispose(); }

    GpioController(const GpioController&) = delete;
    GpioController& operator=(const GpioController&) = delete;

    int PinCount() const {
        int count = 0;
        Core::detail::Check(endpoint_gpio_pin_count(Handle(), &count));
        return count;
    }

    GpioPin OpenPin(int pinNumber) {
        Core::detail::Check(endpoint_gpio_open_pin(Handle(), pinNumber, -1, -1));
        return GpioPin(*this, pinNumber);
    }

    GpioPin OpenPin(int pinNumber, PinMode mode) {
        Core::detail::Check(endpoint_gpio_open_pin(Handle(), pinNumber, static_cast<int>(mode), -1));
        return GpioPin(*this, pinNumber);
    }

    // An output opened with its first value: no glitch
    GpioPin OpenPin(int pinNumber, PinMode mode, PinValue initialValue) {
        Core::detail::Check(endpoint_gpio_open_pin(Handle(), pinNumber, static_cast<int>(mode), static_cast<int>(initialValue)));
        return GpioPin(*this, pinNumber);
    }

    void ClosePin(int pinNumber) {
        Core::detail::Check(endpoint_gpio_close_pin(Handle(), pinNumber));
        std::lock_guard lock(gate_);
        for (auto it = callbacks_.begin(); it != callbacks_.end();) {
            it = it->second->pin == pinNumber ? callbacks_.erase(it) : std::next(it);
        }
    }

    bool IsPinOpen(int pinNumber) const {
        int open = 0;
        Core::detail::Check(endpoint_gpio_is_pin_open(Handle(), pinNumber, &open));
        return open != 0;
    }

    void SetPinMode(int pinNumber, PinMode mode) {
        Core::detail::Check(endpoint_gpio_set_pin_mode(Handle(), pinNumber, static_cast<int>(mode), -1));
    }

    void SetPinMode(int pinNumber, PinMode mode, PinValue initialValue) {
        Core::detail::Check(
            endpoint_gpio_set_pin_mode(Handle(), pinNumber, static_cast<int>(mode), static_cast<int>(initialValue)));
    }

    PinMode GetPinMode(int pinNumber) const {
        int mode = 0;
        Core::detail::Check(endpoint_gpio_get_pin_mode(Handle(), pinNumber, &mode));
        return static_cast<PinMode>(mode);
    }

    bool IsPinModeSupported(int pinNumber, PinMode mode) const {
        int supported = 0;
        Core::detail::Check(endpoint_gpio_is_pin_mode_supported(Handle(), pinNumber, static_cast<int>(mode), &supported));
        return supported != 0;
    }

    PinValue Read(int pinNumber) const {
        int value = 0;
        Core::detail::Check(endpoint_gpio_read(Handle(), pinNumber, &value));
        return static_cast<PinValue>(value);
    }

    void Write(int pinNumber, PinValue value) {
        Core::detail::Check(endpoint_gpio_write(Handle(), pinNumber, static_cast<int>(value)));
    }

    void Toggle(int pinNumber) { Write(pinNumber, Read(pinNumber) == PinValue::High ? PinValue::Low : PinValue::High); }

    // Waits for a pin change, at most timeout (a negative timeout waits forever)
    WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, std::chrono::milliseconds timeout) {
        int happened = 0, timedOut = 0;
        Core::detail::Check(endpoint_gpio_wait_for_event(Handle(), pinNumber, static_cast<int>(eventTypes),
                                                         timeout.count() < 0 ? -1 : static_cast<int>(timeout.count()),
                                                         &happened, &timedOut));
        return WaitForEventResult{static_cast<PinEventTypes>(happened), timedOut != 0};
    }

    // callback runs on the controller's own thread. Returns an id for
    // UnregisterCallbackForPinValueChangedEvent (C++ cannot compare functions).
    int RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback) {
        auto registered = std::make_unique<Registered>(Registered{this, pinNumber, std::move(callback)});
        int id = 0;
        Core::detail::Check(endpoint_gpio_add_callback(Handle(), pinNumber, static_cast<int>(eventTypes), &Trampoline,
                                                       registered.get(), &id));
        std::lock_guard lock(gate_);
        callbacks_[id] = std::move(registered);
        return id;
    }

    void UnregisterCallbackForPinValueChangedEvent(int pinNumber, int callbackId) {
        (void)pinNumber;
        Core::detail::Check(endpoint_gpio_remove_callback(Handle(), callbackId));
        std::lock_guard lock(gate_);
        callbacks_.erase(callbackId);
    }

    // Closes every pin and stops the controller
    void Dispose() {
        if (handle_ != nullptr) {
            endpoint_gpio_dispose(handle_);
            handle_ = nullptr;
            std::lock_guard lock(gate_);
            callbacks_.clear();
        }
    }

private:
    struct Registered {
        GpioController* controller;
        int pin;
        PinChangeEventHandler handler;
    };

    static void Trampoline(void* context, int pin, int change) {
        auto r = static_cast<Registered*>(context);
        r->handler(*r->controller, PinValueChangedEventArgs{static_cast<PinEventTypes>(change), pin});
    }

    void* Handle() const {
        if (handle_ == nullptr) {
            throw Core::ObjectDisposedException("The GpioController is disposed");
        }
        return handle_;
    }

    void* handle_ = nullptr;
    std::mutex gate_;
    std::map<int, std::unique_ptr<Registered>> callbacks_;
};

inline PinMode GpioPin::GetPinMode() const { return controller_->GetPinMode(pinNumber_); }
inline void GpioPin::SetPinMode(PinMode mode) { controller_->SetPinMode(pinNumber_, mode); }
inline bool GpioPin::IsPinModeSupported(PinMode mode) const { return controller_->IsPinModeSupported(pinNumber_, mode); }
inline PinValue GpioPin::Read() const { return controller_->Read(pinNumber_); }
inline void GpioPin::Write(PinValue value) { controller_->Write(pinNumber_, value); }
inline void GpioPin::Toggle() { controller_->Toggle(pinNumber_); }

}  // namespace GHIElectronics::Endpoint::Devices::Gpio
