// The hidden C layer (see CApi.h) for USB keyboards, mice and joysticks

#include <cstdint>

#include "CApi.h"
#include "Input.h"

using namespace GHIElectronics::Endpoint::Internal;

// (context, event, a, b, c, d, e): see Input::Event
typedef void (*endpoint_input_callback)(void* context, int event, int a, int b, int c, int d, int e);

namespace {

Input::Device* Device(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The device is disposed");
    }
    return static_cast<Input::Device*>(handle);
}

}  // namespace

// deviceName: a DeviceName from UsbHostController ("/dev/input/event2"); type:
// DeviceType Mouse (3), Keyboard (4) or Joystick (5). The callback runs on the
// device's own thread.
ENDPOINT_API int endpoint_input_open(const char* deviceName, int type, endpoint_input_callback callback, void* context,
                                     void** device) {
    return Call([&] {
        *device = new Input::Device(deviceName != nullptr ? deviceName : "", type,
                                    [callback, context](int event, int a, int b, int c, int d, int e) {
                                        if (callback != nullptr) {
                                            callback(context, event, a, b, c, d, e);
                                        }
                                    });
    });
}

ENDPOINT_API int endpoint_input_close(void* device) {
    return Call([&] { delete Device(device); });
}

// capabilities: a joystick's DeviceCapabilities
ENDPOINT_API int endpoint_input_info(void* device, int* vendorId, int* productId, int* connected, int* capabilities) {
    return Call([&] {
        auto d = Device(device);
        *vendorId = d->VendorId();
        *productId = d->ProductId();
        *connected = d->Connected() ? 1 : 0;
        *capabilities = d->Capabilities();
    });
}

// state: 1 pressed, 2 released
ENDPOINT_API int endpoint_input_key_state(void* device, int key, int* state) {
    return Call([&] { *state = Device(device)->KeyState(key); });
}

ENDPOINT_API int endpoint_input_mouse_state(void* device, int* x, int* y, int* wheel, int* buttons) {
    return Call([&] { Device(device)->GetMouse(*x, *y, *wheel, *buttons); });
}

ENDPOINT_API int endpoint_input_joystick_state(void* device, int cursor, int* x, int* y, int* hat, uint32_t* buttons) {
    return Call([&] { Device(device)->GetJoystick(cursor, *x, *y, *hat, *buttons); });
}

// A mouse's cursor (cursor 0), or a joystick's cursor 0 or 1
ENDPOINT_API int endpoint_input_set_cursor_bounds(void* device, int cursor, int minX, int maxX, int minY, int maxY) {
    return Call([&] { Device(device)->SetCursorBounds(cursor, minX, maxX, minY, maxY); });
}

ENDPOINT_API int endpoint_input_set_cursor_position(void* device, int x, int y) {
    return Call([&] { Device(device)->SetCursorPosition(x, y); });
}

ENDPOINT_API int endpoint_input_set_scale(void* device, double scale) {
    return Call([&] { Device(device)->SetScale(scale); });
}
