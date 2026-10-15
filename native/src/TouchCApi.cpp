// The hidden C layer for Touch (see CApi.h). Language wrappers:
// .NET GHIElectronics.Endpoint.Devices.UsbHost.Touch, Python
// endpoint.Touch, C++ Endpoint::Devices::UsbHost::Touch.

#include <cstdio>

#include "CApi.h"
#include "Touch.h"

using namespace GHIElectronics::Endpoint::Internal;

typedef void (*endpoint_touch_callback)(void* context, int eventType, int pointCount,
                                        const int* ids, const int* xs, const int* ys);

namespace {

// Thin shim that unpacks the Touch::Point array into separate ids/xs/ys arrays
// for ctypes / P/Invoke friendliness (no struct marshalling needed).
struct ShimState {
    endpoint_touch_callback userCallback;
    void* userContext;
};

void Trampoline(void* context, Touch::EventType event, int count, const Touch::Point* points) {
    auto* state = static_cast<ShimState*>(context);
    int ids[Touch::MaxPoints];
    int xs[Touch::MaxPoints];
    int ys[Touch::MaxPoints];
    for (int i = 0; i < count && i < Touch::MaxPoints; ++i) {
        ids[i] = points[i].id;
        xs[i] = points[i].x;
        ys[i] = points[i].y;
    }
    state->userCallback(state->userContext, static_cast<int>(event), count, ids, xs, ys);
}

struct TouchHandle {
    Touch::TouchImpl* impl;
    ShimState* shim;
};

}  // namespace

// Fills outBuf with the /dev/input/eventN path of the first built-in
// (non-USB) touchscreen, or an empty string if none is present.
ENDPOINT_API int endpoint_touch_find_default(char* outBuf, int outSize) {
    return Call([&] {
        if (outBuf == nullptr || outSize <= 0) {
            return;
        }
        std::string path = Touch::FindDefaultDevice();
        std::snprintf(outBuf, static_cast<size_t>(outSize), "%s", path.c_str());
    });
}

ENDPOINT_API int endpoint_touch_open(const char* deviceName, endpoint_touch_callback callback,
                                     void* context, void** device) {
    return Call([&] {
        auto* shim = new ShimState{callback, context};
        try {
            auto* impl = new Touch::TouchImpl(deviceName, &Trampoline, shim);
            *device = new TouchHandle{impl, shim};
        } catch (...) {
            delete shim;
            throw;
        }
    });
}

ENDPOINT_API int endpoint_touch_close(void* device) {
    return Call([&] {
        if (device == nullptr) {
            return;
        }
        auto* handle = static_cast<TouchHandle*>(device);
        delete handle->impl;
        delete handle->shim;
        delete handle;
    });
}

ENDPOINT_API int endpoint_touch_info(void* device, int* vendorId, int* productId,
                                     int* connected, int* width, int* height) {
    return Call([&] {
        auto* handle = static_cast<TouchHandle*>(device);
        if (vendorId) *vendorId = handle->impl->VendorId();
        if (productId) *productId = handle->impl->ProductId();
        if (connected) *connected = handle->impl->Connected() ? 1 : 0;
        if (width) *width = handle->impl->Width();
        if (height) *height = handle->impl->Height();
    });
}
