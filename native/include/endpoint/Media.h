// Sound for Endpoint, with the API of .NET IoT's SoundDevice (Iot.Device.Media):
// play WAV and MP3, record WAV, volume and mute, on any sound card of the board.
//
//   using namespace GHIElectronics::Endpoint::Devices::Media;
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
#include <iterator>
#include <istream>
#include <ostream>
#include <span>
#include <string>
#include <vector>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Media {

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
