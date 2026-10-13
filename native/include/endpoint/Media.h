// Webcams and sound for Endpoint.
//
// Camera: any USB webcam (the DeviceName of a Webcam from UsbHostController).
// Pictures come as JPEG files, whether the camera sends MJPEG or raw YUYV:
//
//   using namespace GHIElectronics::Endpoint::Devices::Media;
//
//   auto camera = std::make_unique<Camera>(e.DeviceName());   // "/dev/video0"
//   camera->SetResolution(1280, 720);
//   camera->Capture("/media/usb0/photo.jpg");
//   camera->FrameReceived += [](Camera&, const FrameReceivedEventArgs& frame) { Send(frame.Jpeg()); };
//   camera->StartStreaming();
//
// The first picture waits until the camera's automatic exposure has settled
// (about a second); while pictures are taken the camera keeps running, so the
// next ones come at once. The events come on the camera's own thread.
//
// Sound, with the API of .NET IoT's SoundDevice (Iot.Device.Media): play WAV
// and MP3, record WAV, volume and mute, on any sound card of the board.
//
//   auto sound = SoundDevice::Create(SoundConnectionSettings());   // the first sound card
//   sound.Play("/media/usb0/beep.wav");
//   sound.PlayMp3("/media/usb0/song.mp3");
//   sound.Record(5, "/data/voice.wav");                            // 5 s from the microphone
//
// A USB sound card is "hw:1" (the DeviceName of its UsbHostController event):
// with more than one card, give it as PlaybackDeviceName, RecordingDeviceName
// and MixerDeviceName ("hw:" names are opened as "plughw:", which converts any
// WAV to what the card plays). Play, PlayMp3 and Record wait until they are
// done. Volumes are in the card's own units, as on .NET.
#pragma once

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <exception>
#include <fstream>
#include <iterator>
#include <istream>
#include <ostream>
#include <span>
#include <string>
#include <vector>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Media {

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
class Camera {
public:
    // The camera UsbHostController reported ("/dev/video0"; the number can change when it is plugged in again)
    explicit Camera(const std::string& deviceName) : deviceName_(deviceName) {
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
    ~Camera() { Dispose(); }
    Camera(const Camera&) = delete;
    Camera& operator=(const Camera&) = delete;

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
    Core::Event<Camera&, const FrameReceivedEventArgs&> FrameReceived;
    Core::Event<Camera&> Disconnected;

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
            throw Core::ObjectDisposedException("The Camera is disposed");
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
        auto self = static_cast<Camera*>(context);
        try {
            self->FrameReceived(*self, FrameReceivedEventArgs({jpeg, static_cast<size_t>(length)}, width, height));
        }
        catch (const std::exception& error) {
            std::fprintf(stderr, "Unhandled exception in FrameReceived: %s\n", error.what());
        }
    }

    static void Unplugged(void* context) {
        auto self = static_cast<Camera*>(context);
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

// Which sound card plays, records and has the volume controls, and how recordings sound
struct SoundConnectionSettings {
    std::string PlaybackDeviceName = "default";
    std::string RecordingDeviceName = "default";
    std::string MixerDeviceName = "default";
    uint32_t RecordingSampleRate = 8000;
    uint16_t RecordingChannels = 2;
    uint16_t RecordingBitsPerSample = 16;
};

class SoundDevice {
public:
    // unmute: turn the sound on (playback and recording), as .NET does
    static SoundDevice Create(const SoundConnectionSettings& settings, bool unmute = true) {
        return SoundDevice(settings, unmute);
    }

    SoundDevice(const SoundConnectionSettings& settings, bool unmute = true) : settings_(settings) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_sound_create(settings.PlaybackDeviceName.c_str(), settings.RecordingDeviceName.c_str(),
                                                  settings.MixerDeviceName.c_str(), static_cast<int>(settings.RecordingSampleRate),
                                                  settings.RecordingChannels, settings.RecordingBitsPerSample, unmute ? 1 : 0,
                                                  &handle));
        handle_ = Handle(handle);
    }

    const SoundConnectionSettings& Settings() const { return settings_; }

    // The card's volumes, in its own units, and mute switches
    long PlaybackVolume() const { return GetVolume(0); }
    void PlaybackVolume(long volume) { Core::detail::Check(endpoint_sound_set_volume(Get(), 0, volume)); }
    bool PlaybackMute() const { return GetMute(0); }
    void PlaybackMute(bool mute) { Core::detail::Check(endpoint_sound_set_mute(Get(), 0, mute ? 1 : 0)); }
    long RecordingVolume() const { return GetVolume(1); }
    void RecordingVolume(long volume) { Core::detail::Check(endpoint_sound_set_volume(Get(), 1, volume)); }
    bool RecordingMute() const { return GetMute(1); }
    void RecordingMute(bool mute) { Core::detail::Check(endpoint_sound_set_mute(Get(), 1, mute ? 1 : 0)); }

    // Plays a WAV file, or a WAV from a stream (read to its end first)
    void Play(const std::string& wavPath) { Core::detail::Check(endpoint_sound_play_file(Get(), wavPath.c_str(), 0)); }
    void Play(std::istream& wavStream) { PlayBytes(ReadAll(wavStream), 0); }
    void Play(std::span<const uint8_t> wav) { PlayBytes(wav, 0); }

    // Plays an MP3 file, or an MP3 from a stream (read to its end first)
    void PlayMp3(const std::string& mp3Path) { Core::detail::Check(endpoint_sound_play_file(Get(), mp3Path.c_str(), 1)); }
    void PlayMp3(std::istream& mp3Stream) { PlayBytes(ReadAll(mp3Stream), 1); }
    void PlayMp3(std::span<const uint8_t> mp3) { PlayBytes(mp3, 1); }

    // Records a WAV for the given seconds, with the recording settings
    void Record(uint32_t recordTimeSeconds, const std::string& outputFilePath) {
        Core::detail::Check(endpoint_sound_record_file(Get(), static_cast<int>(recordTimeSeconds), outputFilePath.c_str()));
    }
    void Record(uint32_t recordTimeSeconds, std::ostream& outputStream) {
        std::vector<uint8_t> wav(44 + static_cast<size_t>(recordTimeSeconds) * settings_.RecordingSampleRate *
                                          settings_.RecordingChannels * settings_.RecordingBitsPerSample / 8);
        int length = 0;
        Core::detail::Check(endpoint_sound_record_buffer(Get(), static_cast<int>(recordTimeSeconds), wav.data(),
                                                         static_cast<int>(wav.size()), &length));
        outputStream.write(reinterpret_cast<const char*>(wav.data()), length);
    }

    // Records a WAV file in the background until StopRecording
    void StartRecording(const std::string& outputFilePath) {
        Core::detail::Check(endpoint_sound_start_recording(Get(), outputFilePath.c_str()));
    }
    void StopRecording() { Core::detail::Check(endpoint_sound_stop_recording(Get())); }

    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_sound_dispose>;

    void* Get() const { return handle_.Get("SoundDevice"); }

    long GetVolume(int capture) const {
        int64_t volume = 0;
        Core::detail::Check(endpoint_sound_get_volume(Get(), capture, &volume));
        return static_cast<long>(volume);
    }

    bool GetMute(int capture) const {
        int mute = 0;
        Core::detail::Check(endpoint_sound_get_mute(Get(), capture, &mute));
        return mute != 0;
    }

    void PlayBytes(std::span<const uint8_t> data, int mp3) {
        Core::detail::Check(endpoint_sound_play_buffer(Get(), data.data(), static_cast<int>(data.size()), mp3));
    }

    static std::vector<uint8_t> ReadAll(std::istream& stream) {
        return std::vector<uint8_t>(std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>());
    }

    SoundConnectionSettings settings_;
    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Media
