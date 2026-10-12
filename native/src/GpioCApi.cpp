// The hidden C layer for GPIO (see CApi.h). The controller is a handle;
// pins are EPM815 pin numbers; modes, values and event types have the values
// of .NET's PinMode, PinValue and PinEventTypes.

#include "CApi.h"
#include "Gpio.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {
GpioControllerImpl* Controller(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The GpioController is disposed");
    }
    return static_cast<GpioControllerImpl*>(handle);
}
}  // namespace

typedef void (*endpoint_gpio_callback)(void* context, int pin, int changeType);

ENDPOINT_API int endpoint_gpio_create(void** controller) {
    return Call([&] { *controller = new GpioControllerImpl(); });
}

ENDPOINT_API int endpoint_gpio_dispose(void* controller) {
    return Call([&] { delete static_cast<GpioControllerImpl*>(controller); });
}

ENDPOINT_API int endpoint_gpio_pin_count(void* controller, int* count) {
    return Call([&] { *count = Controller(controller)->PinCount(); });
}

// mode -1: open without setting a mode; initialValue -1: none
ENDPOINT_API int endpoint_gpio_open_pin(void* controller, int pin, int mode, int initialValue) {
    return Call([&] {
        auto c = Controller(controller);
        if (mode < 0) {
            c->OpenPin(pin);
        }
        else if (initialValue < 0) {
            c->OpenPin(pin, static_cast<PinMode>(mode));
        }
        else {
            c->OpenPin(pin, static_cast<PinMode>(mode), initialValue);
        }
    });
}

ENDPOINT_API int endpoint_gpio_close_pin(void* controller, int pin) {
    return Call([&] { Controller(controller)->ClosePin(pin); });
}

ENDPOINT_API int endpoint_gpio_is_pin_open(void* controller, int pin, int* open) {
    return Call([&] { *open = Controller(controller)->IsPinOpen(pin); });
}

ENDPOINT_API int endpoint_gpio_set_pin_mode(void* controller, int pin, int mode, int initialValue) {
    return Call([&] { Controller(controller)->SetPinMode(pin, static_cast<PinMode>(mode), initialValue); });
}

ENDPOINT_API int endpoint_gpio_get_pin_mode(void* controller, int pin, int* mode) {
    return Call([&] { *mode = static_cast<int>(Controller(controller)->GetPinMode(pin)); });
}

ENDPOINT_API int endpoint_gpio_is_pin_mode_supported(void* controller, int pin, int mode, int* supported) {
    return Call([&] { *supported = Controller(controller)->IsPinModeSupported(pin, static_cast<PinMode>(mode)); });
}

ENDPOINT_API int endpoint_gpio_read(void* controller, int pin, int* value) {
    return Call([&] { *value = Controller(controller)->Read(pin); });
}

ENDPOINT_API int endpoint_gpio_write(void* controller, int pin, int value) {
    return Call([&] { Controller(controller)->Write(pin, value); });
}

// timeoutMs -1: wait without a limit
ENDPOINT_API int endpoint_gpio_wait_for_event(void* controller, int pin, int eventTypes, int timeoutMs, int* happened,
                                              int* timedOut) {
    return Call([&] {
        auto result = Controller(controller)->WaitForEvent(pin, eventTypes, timeoutMs);
        *happened = result.eventTypes;
        *timedOut = result.timedOut;
    });
}

// callback runs on the controller's own thread
ENDPOINT_API int endpoint_gpio_add_callback(void* controller, int pin, int eventTypes, endpoint_gpio_callback callback,
                                            void* context, int* id) {
    return Call([&] {
        *id = Controller(controller)->AddCallback(pin, eventTypes,
                                                  [callback, context](int p, int change) { callback(context, p, change); });
    });
}

ENDPOINT_API int endpoint_gpio_remove_callback(void* controller, int id) {
    return Call([&] { Controller(controller)->RemoveCallback(id); });
}
