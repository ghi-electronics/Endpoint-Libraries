// The hidden C layer (see CApi.h) for webcams: Camera

#include <cstdint>
#include <cstdlib>
#include <cstring>

#include "CApi.h"
#include "Camera.h"

using namespace GHIElectronics::Endpoint::Internal;

// A frame while streaming (a JPEG), on the camera's own thread
typedef void (*endpoint_camera_frame_callback)(void* context, const uint8_t* jpeg, int length, int width, int height);
typedef void (*endpoint_unplugged_callback)(void* context);

namespace {

Camera::Device* Device(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The Camera is disposed");
    }
    return static_cast<Camera::Device*>(handle);
}

}  // namespace

// deviceName: "/dev/video0", the DeviceName of a Webcam
ENDPOINT_API int endpoint_camera_open(const char* deviceName, endpoint_camera_frame_callback frame,
                                      endpoint_unplugged_callback unplugged, void* context, void** camera) {
    return Call([&] {
        *camera = new Camera::Device(
            deviceName != nullptr ? deviceName : "",
            [frame, context](const uint8_t* jpeg, size_t length, int width, int height) {
                if (frame != nullptr) {
                    frame(context, jpeg, static_cast<int>(length), width, height);
                }
            },
            [unplugged, context] {
                if (unplugged != nullptr) {
                    unplugged(context);
                }
            });
    });
}

ENDPOINT_API int endpoint_camera_close(void* camera) {
    return Call([&] { delete Device(camera); });
}

ENDPOINT_API int endpoint_camera_info(void* camera, char* name, int nameSize, int* connected, int* formats) {
    return Call([&] {
        auto d = Device(camera);
        if (name != nullptr && nameSize > 0) {
            std::strncpy(name, d->Name().c_str(), static_cast<size_t>(nameSize) - 1);
            name[nameSize - 1] = '\0';
        }
        *connected = d->Connected() ? 1 : 0;
        *formats = static_cast<int>(d->Formats().size());
    });
}

// The camera's formats one by one: pixelFormat 0 MJPEG, 1 YUYV
ENDPOINT_API int endpoint_camera_format(void* camera, int index, int* width, int* height, int* pixelFormat,
                                        int* maxFrameRate) {
    return Call([&] {
        auto formats = Device(camera)->Formats();
        if (index < 0 || index >= static_cast<int>(formats.size())) {
            ThrowArgumentOutOfRange("There is no such format");
        }
        *width = formats[index].width;
        *height = formats[index].height;
        *pixelFormat = formats[index].pixelFormat;
        *maxFrameRate = formats[index].maxFrameRate;
    });
}

// The size in use; frameRate: the one asked for (or the camera's highest)
ENDPOINT_API int endpoint_camera_get_resolution(void* camera, int* width, int* height, int* pixelFormat, int* frameRate) {
    return Call([&] {
        auto current = Device(camera)->Current();
        *width = current.width;
        *height = current.height;
        *pixelFormat = current.pixelFormat;
        *frameRate = current.maxFrameRate;
    });
}

// frameRate 0: the camera's highest for that size; pixelFormat -1: MJPEG if it can, else YUYV
ENDPOINT_API int endpoint_camera_set_resolution(void* camera, int width, int height, int frameRate, int pixelFormat) {
    return Call([&] { Device(camera)->SetResolution(width, height, frameRate, pixelFormat); });
}

ENDPOINT_API int endpoint_camera_get_quality(void* camera, int* quality) {
    return Call([&] { *quality = Device(camera)->JpegQuality(); });
}

ENDPOINT_API int endpoint_camera_set_quality(void* camera, int quality) {
    return Call([&] { Device(camera)->SetJpegQuality(quality); });
}

// A picture: a JPEG the library allocates (give it back with endpoint_camera_free)
ENDPOINT_API int endpoint_camera_capture(void* camera, int timeoutMs, uint8_t** jpeg, int* length) {
    return Call([&] {
        auto picture = Device(camera)->Capture(timeoutMs);
        *jpeg = static_cast<uint8_t*>(std::malloc(picture.size()));
        if (*jpeg == nullptr) {
            ThrowIO("Not enough memory for the picture");
        }
        std::memcpy(*jpeg, picture.data(), picture.size());
        *length = static_cast<int>(picture.size());
    });
}

ENDPOINT_API int endpoint_camera_free(uint8_t* jpeg) {
    std::free(jpeg);
    return 0;
}

ENDPOINT_API int endpoint_camera_start_streaming(void* camera) {
    return Call([&] { Device(camera)->StartStreaming(); });
}

ENDPOINT_API int endpoint_camera_stop_streaming(void* camera) {
    return Call([&] { Device(camera)->StopStreaming(); });
}

// control: CameraControl; supported 0: the camera does not have it
ENDPOINT_API int endpoint_camera_get_control(void* camera, int control, int* supported, int* minimum, int* maximum,
                                             int* step, int* defaultValue, int* value) {
    return Call([&] {
        auto info = Device(camera)->GetControl(control);
        *supported = info.supported ? 1 : 0;
        *minimum = info.minimum;
        *maximum = info.maximum;
        *step = info.step;
        *defaultValue = info.defaultValue;
        *value = info.value;
    });
}

ENDPOINT_API int endpoint_camera_set_control(void* camera, int control, int value) {
    return Call([&] { Device(camera)->SetControl(control, value); });
}
