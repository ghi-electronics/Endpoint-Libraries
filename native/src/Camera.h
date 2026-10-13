#pragma once

// Webcams (any USB Video Class camera: "/dev/video0", the DeviceName of a
// Webcam from UsbHostController) for all three languages: pictures as JPEG,
// whether the camera sends MJPEG (passed on, with the standard Huffman tables
// added when the camera leaves them out) or raw YUYV (encoded here). The
// first picture waits until the camera's auto exposure has settled; while
// pictures are taken, the camera keeps running (for 3 s after the last one),
// so the next ones come at once. Streaming gives every frame as a JPEG.

#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <vector>

namespace GHIElectronics::Endpoint::Internal::Camera {

// What the camera sends (pictures always come out as JPEG)
enum PixelFormat : int { Mjpeg = 0, Yuyv = 1 };

struct Format {
    int width = 0, height = 0;
    int pixelFormat = Mjpeg;
    int maxFrameRate = 0;  // frames per second
};

// The picture controls (the values of Camera's CameraControl)
enum Control : int {
    Brightness, Contrast, Saturation, Hue, Gamma, Gain, Sharpness, BacklightCompensation,
    AutoWhiteBalance, WhiteBalanceTemperature, AutoExposure, Exposure, PowerLineFrequency,
    AutoFocus, Focus, Zoom, Pan, Tilt, ControlCount
};

struct ControlInfo {
    bool supported = false;
    int minimum = 0, maximum = 0, step = 1, defaultValue = 0, value = 0;
};

// A frame while streaming: a JPEG
using FrameCallback = std::function<void(const uint8_t* jpeg, size_t length, int width, int height)>;

struct State;

class Device {
public:
    Device(const std::string& deviceName, FrameCallback frame, std::function<void()> unplugged);
    // Stops the camera (a frame being delivered finishes first)
    ~Device();
    Device(const Device&) = delete;
    Device& operator=(const Device&) = delete;

    // The camera's name ("USB 2.0 Camera: USB Camera")
    std::string Name() const;
    bool Connected() const;
    std::vector<Format> Formats() const;
    // The size in use, what the camera sends for it, and the frame rate asked for (0: its highest)
    Format Current() const;
    // pixelFormat -1: MJPEG if the camera has it at that size, else YUYV
    void SetResolution(int width, int height, int frameRate, int pixelFormat = -1);
    int JpegQuality() const;
    void SetJpegQuality(int quality);

    // A picture (JPEG); a TimeoutException if the camera sends none in timeoutMs
    std::vector<uint8_t> Capture(int timeoutMs);
    void StartStreaming();
    void StopStreaming();
    bool Streaming() const;

    ControlInfo GetControl(int control) const;
    void SetControl(int control, int value);

private:
    std::shared_ptr<State> state;
};

}  // namespace GHIElectronics::Endpoint::Internal::Camera
