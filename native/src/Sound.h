#pragma once

// Sound for Python and C++ with the API of .NET's Iot.Device.Media.SoundDevice
// (Microsoft's, which .NET programs use): play WAV, record WAV, volume and
// mute; and MP3 (PlayMp3; .NET gets it with the Mp3Decoder below). Any sound
// card the board has: a USB one ("hw:1" from UsbHostController), or "default".
// The board's ALSA library (libasound.so.2) is loaded the first time a program
// uses sound, so programs without sound do not need it.

#include <atomic>
#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <thread>
#include <vector>

namespace GHIElectronics::Endpoint::Internal::Sound {

// .NET's SoundConnectionSettings
struct Settings {
    std::string playbackDeviceName = "default";
    std::string recordingDeviceName = "default";
    std::string mixerDeviceName = "default";
    unsigned recordingSampleRate = 8000;
    unsigned recordingChannels = 2;
    unsigned recordingBitsPerSample = 16;
};

// Reads up to the given number of bytes, gives how many it read (0: the end)
using Reader = std::function<size_t(void* buffer, size_t size)>;

class Device {
public:
    Device(Settings settings, bool unmute);
    ~Device();
    Device(const Device&) = delete;
    Device& operator=(const Device&) = delete;

    // Plays a WAV (PCM 8, 16, 24 or 32 bits, or 32-bit float) or an MP3 file to
    // its end (the calling thread waits)
    void PlayWav(const Reader& read);
    void PlayMp3File(const std::string& path);
    void PlayMp3Buffer(const uint8_t* data, size_t length);

    // Records a WAV with the recording settings
    void Record(unsigned seconds, const std::function<void(const void*, size_t)>& write);
    void StartRecording(const std::string& path);
    void StopRecording();

    // The card's own volume units (as .NET's SoundDevice); capture: recording
    long GetVolume(bool capture);
    void SetVolume(bool capture, long volume);
    bool GetMute(bool capture);
    void SetMute(bool capture, bool mute);

private:
    void* OpenPcm(bool capture, int format, unsigned channels, unsigned rate);
    void WritePcm(void* pcm, const uint8_t* data, size_t frames, size_t frameSize);
    void* MixerElement(bool capture, bool volume);

    Settings settings;
    void* mixer = nullptr;
    std::thread recorder;
    std::atomic<bool> recording{false};
};

// MP3 to 16-bit samples (interleaved), for .NET's PlayMp3
class Mp3Decoder {
public:
    explicit Mp3Decoder(const std::string& path);
    Mp3Decoder(const uint8_t* data, size_t length);  // copies the data
    ~Mp3Decoder();
    Mp3Decoder(const Mp3Decoder&) = delete;
    Mp3Decoder& operator=(const Mp3Decoder&) = delete;

    int SampleRate() const;
    int Channels() const;
    uint64_t Samples() const;  // all channels' samples
    // Up to count samples: how many there were (0: the end)
    size_t Read(int16_t* samples, size_t count);

private:
    struct State;
    std::unique_ptr<State> state;
};

}  // namespace GHIElectronics::Endpoint::Internal::Sound
