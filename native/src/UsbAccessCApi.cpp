// The hidden C layer (see CApi.h) for a program's own USB devices: .NET's
// RawDevice and HidDevice

#include <algorithm>
#include <cstdint>
#include <cstring>

#include "CApi.h"
#include "UsbAccess.h"

using namespace GHIElectronics::Endpoint::Internal;

// Called once, on the device's own thread, when it is unplugged
typedef void (*endpoint_unplugged_callback)(void* context);

namespace {

template <typename T>
T* Device(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The device is disposed");
    }
    return static_cast<T*>(handle);
}

UsbAccess::UnpluggedCallback Unplugged(endpoint_unplugged_callback callback, void* context) {
    return [callback, context] {
        if (callback != nullptr) {
            callback(context);
        }
    };
}

}  // namespace

// deviceName: "/dev/bus/usb/001/005", the DeviceName of an Unknown device
ENDPOINT_API int endpoint_usbraw_open(const char* deviceName, endpoint_unplugged_callback callback, void* context,
                                      void** device) {
    return Call([&] { *device = new UsbAccess::RawDevice(deviceName ? deviceName : "", Unplugged(callback, context)); });
}

ENDPOINT_API int endpoint_usbraw_close(void* device) {
    return Call([&] { delete Device<UsbAccess::RawDevice>(device); });
}

ENDPOINT_API int endpoint_usbraw_info(void* device, int* vendorId, int* productId, int* connected) {
    return Call([&] {
        auto d = Device<UsbAccess::RawDevice>(device);
        *vendorId = d->VendorId();
        *productId = d->ProductId();
        *connected = d->Connected() ? 1 : 0;
    });
}

// timeoutMs 0: no limit
ENDPOINT_API int endpoint_usbraw_control(void* device, int requestType, int request, int value, int index,
                                         uint8_t* data, int length, int timeoutMs, int* transferred) {
    return Call([&] {
        *transferred = Device<UsbAccess::RawDevice>(device)->Control(requestType, request, value, index, data, length, timeoutMs);
    });
}

// endpoint: its address (0x81: IN 1, 0x02: OUT 2); bulk or interrupt
ENDPOINT_API int endpoint_usbraw_transfer(void* device, int endpoint, uint8_t* data, int length, int timeoutMs,
                                          int* transferred) {
    return Call([&] { *transferred = Device<UsbAccess::RawDevice>(device)->Transfer(endpoint, data, length, timeoutMs); });
}

// deviceName: "/dev/hidraw0", the DeviceName of a HID device
ENDPOINT_API int endpoint_hid_open(const char* deviceName, endpoint_unplugged_callback callback, void* context,
                                   void** device) {
    return Call([&] { *device = new UsbAccess::HidDevice(deviceName ? deviceName : "", Unplugged(callback, context)); });
}

ENDPOINT_API int endpoint_hid_close(void* device) {
    return Call([&] { delete Device<UsbAccess::HidDevice>(device); });
}

ENDPOINT_API int endpoint_hid_info(void* device, int* vendorId, int* productId, int* connected, char* name, int nameSize) {
    return Call([&] {
        auto d = Device<UsbAccess::HidDevice>(device);
        *vendorId = d->VendorId();
        *productId = d->ProductId();
        *connected = d->Connected() ? 1 : 0;
        if (name != nullptr && nameSize > 0) {
            std::strncpy(name, d->Name().c_str(), static_cast<size_t>(nameSize) - 1);
            name[nameSize - 1] = '\0';
        }
    });
}

ENDPOINT_API int endpoint_hid_report_descriptor(void* device, uint8_t* buffer, int size, int* length) {
    return Call([&] {
        auto descriptor = Device<UsbAccess::HidDevice>(device)->ReportDescriptor();
        *length = static_cast<int>(descriptor.size());
        if (buffer != nullptr) {
            std::memcpy(buffer, descriptor.data(), std::min(descriptor.size(), static_cast<size_t>(std::max(size, 0))));
        }
    });
}

ENDPOINT_API int endpoint_hid_write(void* device, const uint8_t* data, int length, int* written) {
    return Call([&] { *written = Device<UsbAccess::HidDevice>(device)->Write(data, length); });
}

// length 0: no report came in timeoutMs (-1: no limit)
ENDPOINT_API int endpoint_hid_read(void* device, uint8_t* buffer, int size, int timeoutMs, int* length) {
    return Call([&] { *length = Device<UsbAccess::HidDevice>(device)->Read(buffer, size, timeoutMs); });
}

ENDPOINT_API int endpoint_hid_get_feature(void* device, uint8_t* buffer, int size, int* length) {
    return Call([&] { *length = Device<UsbAccess::HidDevice>(device)->GetFeatureReport(buffer, size); });
}

ENDPOINT_API int endpoint_hid_send_feature(void* device, const uint8_t* data, int length) {
    return Call([&] { Device<UsbAccess::HidDevice>(device)->SendFeatureReport(data, length); });
}
