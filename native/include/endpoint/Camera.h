// Camera for Endpoint.
//
// UsbCamera: any USB webcam (the DeviceName of a Webcam from UsbHostController).
// Pictures come as JPEG files, whether the camera sends MJPEG or raw YUYV:
//
//   using namespace GHIElectronics::Endpoint::Devices::Camera;
//
//   auto camera = std::make_unique<UsbCamera>(e.DeviceName());   // "/dev/video0"
//   camera->SetResolution(1280, 720);
//   camera->Capture("/media/usb0/photo.jpg");
//   camera->FrameReceived += [](UsbCamera&, const FrameReceivedEventArgs& frame) { Send(frame.Jpeg()); };
//   camera->StartStreaming();
//
// The first picture waits until the camera's automatic exposure has settled
// (about a second); while pictures are taken the camera keeps running, so the
// next ones come at once. The events come on the camera's own thread.
#pragma once

#include <cstdint>
#include <cstdio>
#include <exception>
#include <fstream>
#include <span>
#include <string>
#include <vector>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Camera {

// What a camera sends (pictures always come out as JPEG)
enum class CameraPixelFormat { Mjpeg = 0, Yuyv = 1 };

// A picture size a camera has, what it sends for it, and its highest frame rate
struct CameraFormat {
    int Width = 0;
    int Height = 0;
    CameraPixelFormat PixelFormat = CameraPixelFormat::Mjpeg;
    int MaxFrameRate = 0;  // frames per second
};

// A camera's picture controls (each camera has some of them)
enum class CameraControl {
    Brightness, Contrast, Saturation, Hue, Gamma, Gain, Sharpness, BacklightCompensation,
    AutoWhiteBalance,         // 1: on, 0: off (then WhiteBalanceTemperature counts)
    WhiteBalanceTemperature,
    AutoExposure,             // 1: on, 0: off (then Exposure counts)
    Exposure,
    PowerLineFrequency,       // 0: off, 1: 50 Hz, 2: 60 Hz (against flicker from lights)
    AutoFocus,                // 1: on, 0: off (then Focus counts)
    Focus, Zoom, Pan, Tilt,
};

// A camera control's range and value, in the camera's own units
struct CameraControlValue {
    CameraControl Control;
    int Minimum = 0;
    int Maximum = 0;
    int Step = 1;
    int DefaultValue = 0;
    int Value = 0;
};

// A frame while streaming: a JPEG
class FrameReceivedEventArgs {
public:
    FrameReceivedEventArgs(std::span<const uint8_t> jpeg, int width, int height) : jpeg_(jpeg), width_(width), height_(height) {}
    // Valid during the event: copy it to keep it
    std::span<const uint8_t> Jpeg() const { return jpeg_; }
    int Width() const { return width_; }
    int Height() const { return height_; }

private:
    std::span<const uint8_t> jpeg_;
    int width_;
    int height_;
};

// A webcam: any USB Video Class camera. It cannot be copied or moved: keep it
// in a std::unique_ptr (or a variable that lives long enough).
class UsbCamera {
public:
    // The camera UsbHostController reported ("/dev/video0"; the number can change when it is plugged in again)
    explicit UsbCamera(const std::string& deviceName) : deviceName_(deviceName) {
        Core::detail::Check(endpoint_camera_open(deviceName.c_str(), &Frame, &Unplugged, this, &handle_));
        char name[256];
        int connected = 0, count = 0;
        Core::detail::Check(endpoint_camera_info(handle_, name, sizeof name, &connected, &count));
        name_ = name;
        for (int i = 0; i < count; i++) {
            int width, height, pixelFormat, rate;
            Core::detail::Check(endpoint_camera_format(handle_, i, &width, &height, &pixelFormat, &rate));
            formats_.push_back({width, height, static_cast<CameraPixelFormat>(pixelFormat), rate});
        }
    }
    ~UsbCamera() { Dispose(); }
    UsbCamera(const UsbCamera&) = delete;
    UsbCamera& operator=(const UsbCamera&) = delete;

    const std::string& DeviceName() const { return deviceName_; }
    // The camera's name ("USB 2.0 Camera: USB Camera")
    const std::string& Name() const { return name_; }
    // The sizes the camera has, with what it sends for each
    const std::vector<CameraFormat>& SupportedFormats() const { return formats_; }

    bool Connected() const {
        if (handle_ == nullptr) {
            return false;
        }
        int connected = 0, count = 0;
        Core::detail::Check(endpoint_camera_info(handle_, nullptr, 0, &connected, &count));
        return connected != 0;
    }

    int Width() const { return Resolution().Width; }
    int Height() const { return Resolution().Height; }
    CameraPixelFormat PixelFormat() const { return Resolution().PixelFormat; }
    int FrameRate() const { return Resolution().MaxFrameRate; }

    // The JPEG quality (1 to 100) of pictures the library makes from raw YUYV pictures (85 at first)
    int JpegQuality() const {
        int quality = 0;
        Core::detail::Check(endpoint_camera_get_quality(Get(), &quality));
        return quality;
    }
    void JpegQuality(int quality) { Core::detail::Check(endpoint_camera_set_quality(Get(), quality)); }

    // A size the camera has (MJPEG if the camera has it at that size, else YUYV); frameRate 0: its highest
    void SetResolution(int width, int height, int frameRate = 0) {
        Core::detail::Check(endpoint_camera_set_resolution(Get(), width, height, frameRate, -1));
    }
    // One of SupportedFormats, as it is (YUYV too)
    void SetResolution(const CameraFormat& format, int frameRate = 0) {
        Core::detail::Check(endpoint_camera_set_resolution(Get(), format.Width, format.Height, frameRate,
                                                           static_cast<int>(format.PixelFormat)));
    }

    // Takes a picture: a JPEG; a TimeoutException if the camera sends none in timeoutMs
    std::vector<uint8_t> Capture(int timeoutMs = 5000) {
        uint8_t* jpeg = nullptr;
        int length = 0;
        Core::detail::Check(endpoint_camera_capture(Get(), timeoutMs, &jpeg, &length));
        std::vector<uint8_t> picture(jpeg, jpeg + length);
        endpoint_camera_free(jpeg);
        return picture;
    }
    // Takes a picture and saves it as a JPEG file
    void Capture(const std::string& path) {
        auto picture = Capture();
        std::ofstream file(path, std::ios::binary);
        file.write(reinterpret_cast<const char*>(picture.data()), static_cast<std::streamsize>(picture.size()));
        if (!file) {
            throw Core::IOException("Cannot write " + path);
        }
    }

    // A frame while streaming; the camera was unplugged. += gives an id for -=.
    Core::Event<UsbCamera&, const FrameReceivedEventArgs&> FrameReceived;
    Core::Event<UsbCamera&> Disconnected;

    // Every frame comes as a JPEG in FrameReceived
    void StartStreaming() {
        Core::detail::Check(endpoint_camera_start_streaming(Get()));
        streaming_ = true;
    }
    void StopStreaming() {
        Core::detail::Check(endpoint_camera_stop_streaming(Get()));
        streaming_ = false;
    }
    bool IsStreaming() const { return streaming_; }

    // The controls this camera has
    std::vector<CameraControl> SupportedControls() const {
        std::vector<CameraControl> controls;
        for (int c = 0; c <= static_cast<int>(CameraControl::Tilt); c++) {
            CameraControlValue value;
            if (TryGet(static_cast<CameraControl>(c), value)) {
                controls.push_back(static_cast<CameraControl>(c));
            }
        }
        return controls;
    }
    // A control's range and value; a NotSupportedException if the camera does not have it
    CameraControlValue GetControl(CameraControl control) const {
        CameraControlValue value;
        if (!TryGet(control, value)) {
            throw Core::NotSupportedException("The camera does not have this control");
        }
        return value;
    }
    // Sets a control, in the camera's own units (GetControl gives its range)
    void SetControl(CameraControl control, int value) {
        Core::detail::Check(endpoint_camera_set_control(Get(), static_cast<int>(control), value));
    }

    // Stops the camera (a frame being delivered finishes first) and closes it
    void Dispose() {
        if (handle_ != nullptr) {
            endpoint_camera_close(handle_);
            handle_ = nullptr;
            streaming_ = false;
        }
    }

private:
    void* Get() const {
        if (handle_ == nullptr) {
            throw Core::ObjectDisposedException("The UsbCamera is disposed");
        }
        return handle_;
    }

    CameraFormat Resolution() const {
        int width = 0, height = 0, pixelFormat = 0, rate = 0;
        Core::detail::Check(endpoint_camera_get_resolution(Get(), &width, &height, &pixelFormat, &rate));
        return {width, height, static_cast<CameraPixelFormat>(pixelFormat), rate};
    }

    bool TryGet(CameraControl control, CameraControlValue& value) const {
        int supported = 0;
        value.Control = control;
        Core::detail::Check(endpoint_camera_get_control(Get(), static_cast<int>(control), &supported, &value.Minimum,
                                                        &value.Maximum, &value.Step, &value.DefaultValue, &value.Value));
        return supported != 0;
    }

    // From the camera's threads. A handler's error must not stop the frames: it
    // is printed, as .NET prints an unhandled exception.
    static void Frame(void* context, const uint8_t* jpeg, int length, int width, int height) {
        auto self = static_cast<UsbCamera*>(context);
        try {
            self->FrameReceived(*self, FrameReceivedEventArgs({jpeg, static_cast<size_t>(length)}, width, height));
        }
        catch (const std::exception& error) {
            std::fprintf(stderr, "Unhandled exception in FrameReceived: %s\n", error.what());
        }
    }

    static void Unplugged(void* context) {
        auto self = static_cast<UsbCamera*>(context);
        try {
            self->Disconnected(*self);
        }
        catch (const std::exception& error) {
            std::fprintf(stderr, "Unhandled exception in Disconnected: %s\n", error.what());
        }
    }

    std::string deviceName_;
    std::string name_;
    std::vector<CameraFormat> formats_;
    bool streaming_ = false;
    void* handle_ = nullptr;
};

}  // namespace GHIElectronics::Endpoint::Devices::Camera
