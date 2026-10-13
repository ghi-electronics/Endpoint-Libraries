// The hidden C layer (see CApi.h) for the USB host's device events

#include <cstdint>

#include "CApi.h"
#include "UsbHost.h"

using namespace GHIElectronics::Endpoint::Internal;

// status: 0 disconnected, 1 connected. id: the device's (the same for all its
// events). type: DeviceType. deviceName: what to open ("/dev/ttyUSB0", "eth1"...).
typedef void (*endpoint_usbhost_callback)(void* context, int status, uint32_t id, int type, int vendorId,
                                          int productId, const char* deviceName, const char* manufacturer,
                                          const char* product, const char* serialNumber);

namespace {

UsbHost::Controller* Controller(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The UsbHostController is disposed");
    }
    return static_cast<UsbHost::Controller*>(handle);
}

}  // namespace

// The callback runs on the library's USB thread, for every device connected
// (also those already there when the controller is enabled) or disconnected
ENDPOINT_API int endpoint_usbhost_create(endpoint_usbhost_callback callback, void* context, void** controller) {
    return Call([&] {
        *controller = new UsbHost::Controller([callback, context](const UsbHost::DeviceEvent& e) {
            if (callback != nullptr) {
                callback(context, e.status, e.id, e.type, e.vendorId, e.productId, e.deviceName.c_str(),
                         e.manufacturer.c_str(), e.product.c_str(), e.serialNumber.c_str());
            }
        });
    });
}

ENDPOINT_API int endpoint_usbhost_dispose(void* controller) {
    return Call([&] { delete Controller(controller); });
}

ENDPOINT_API int endpoint_usbhost_enable(void* controller) {
    return Call([&] { Controller(controller)->Enable(); });
}

ENDPOINT_API int endpoint_usbhost_disable(void* controller) {
    return Call([&] { Controller(controller)->Disable(); });
}
