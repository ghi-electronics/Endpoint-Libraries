// The hidden C layer (see CApi.h) for sound: SoundDevice (Python, C++) and
// the MP3 decoder (.NET's PlayMp3)

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>

#include "CApi.h"
#include "Sound.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {

Sound::Device* Device(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The SoundDevice is disposed");
    }
    return static_cast<Sound::Device*>(handle);
}

Sound::Mp3Decoder* Decoder(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The MP3 decoder is disposed");
    }
    return static_cast<Sound::Mp3Decoder*>(handle);
}

std::string Text(const char* text) { return text != nullptr ? text : ""; }

}  // namespace

ENDPOINT_API int endpoint_sound_create(const char* playback, const char* recording, const char* mixer, int sampleRate,
                                       int channels, int bitsPerSample, int unmute, void** device) {
    return Call([&] {
        Sound::Settings settings;
        settings.playbackDeviceName = Text(playback);
        settings.recordingDeviceName = Text(recording);
        settings.mixerDeviceName = Text(mixer);
        settings.recordingSampleRate = static_cast<unsigned>(sampleRate);
        settings.recordingChannels = static_cast<unsigned>(channels);
        settings.recordingBitsPerSample = static_cast<unsigned>(bitsPerSample);
        *device = new Sound::Device(settings, unmute != 0);
    });
}

ENDPOINT_API int endpoint_sound_dispose(void* device) {
    return Call([&] { delete Device(device); });
}

// mp3: 0 a WAV file, 1 an MP3 file
ENDPOINT_API int endpoint_sound_play_file(void* device, const char* path, int mp3) {
    return Call([&] {
        auto d = Device(device);
        if (mp3) {
            d->PlayMp3File(Text(path));
            return;
        }
        FILE* file = std::fopen(Text(path).c_str(), "rb");
        if (file == nullptr) {
            ThrowSystemError("Cannot open " + Text(path));
        }
        try {
            d->PlayWav([file](void* buffer, size_t size) { return std::fread(buffer, 1, size, file); });
        }
        catch (...) {
            std::fclose(file);
            throw;
        }
        std::fclose(file);
    });
}

ENDPOINT_API int endpoint_sound_play_buffer(void* device, const uint8_t* data, int length, int mp3) {
    return Call([&] {
        auto d = Device(device);
        if (mp3) {
            d->PlayMp3Buffer(data, static_cast<size_t>(length));
            return;
        }
        size_t at = 0;
        d->PlayWav([&](void* buffer, size_t size) {
            size_t n = std::min(size, static_cast<size_t>(length) - at);
            std::memcpy(buffer, data + at, n);
            at += n;
            return n;
        });
    });
}

ENDPOINT_API int endpoint_sound_record_file(void* device, int seconds, const char* path) {
    return Call([&] {
        FILE* file = std::fopen(Text(path).c_str(), "wb");
        if (file == nullptr) {
            ThrowSystemError("Cannot write " + Text(path));
        }
        try {
            Device(device)->Record(static_cast<unsigned>(seconds), [file](const void* data, size_t size) { std::fwrite(data, 1, size, file); });
        }
        catch (...) {
            std::fclose(file);
            throw;
        }
        std::fclose(file);
    });
}

// The WAV in buffer (44 + seconds * rate * channels * bits / 8 bytes); length: what it took
ENDPOINT_API int endpoint_sound_record_buffer(void* device, int seconds, uint8_t* buffer, int size, int* length) {
    return Call([&] {
        size_t at = 0;
        Device(device)->Record(static_cast<unsigned>(seconds), [&](const void* data, size_t n) {
            if (at + n > static_cast<size_t>(size)) {
                ThrowArgument("The buffer is too small for the recording");
            }
            std::memcpy(buffer + at, data, n);
            at += n;
        });
        *length = static_cast<int>(at);
    });
}

ENDPOINT_API int endpoint_sound_start_recording(void* device, const char* path) {
    return Call([&] { Device(device)->StartRecording(Text(path)); });
}

ENDPOINT_API int endpoint_sound_stop_recording(void* device) {
    return Call([&] { Device(device)->StopRecording(); });
}

// capture: 0 playback, 1 recording; in the card's own units
ENDPOINT_API int endpoint_sound_get_volume(void* device, int capture, int64_t* volume) {
    return Call([&] { *volume = Device(device)->GetVolume(capture != 0); });
}

ENDPOINT_API int endpoint_sound_set_volume(void* device, int capture, int64_t volume) {
    return Call([&] { Device(device)->SetVolume(capture != 0, static_cast<long>(volume)); });
}

ENDPOINT_API int endpoint_sound_get_mute(void* device, int capture, int* mute) {
    return Call([&] { *mute = Device(device)->GetMute(capture != 0) ? 1 : 0; });
}

ENDPOINT_API int endpoint_sound_set_mute(void* device, int capture, int mute) {
    return Call([&] { Device(device)->SetMute(capture != 0, mute != 0); });
}

// An MP3 file (path) or one in memory (data, length: copied); samples: all
// channels' 16-bit samples together
ENDPOINT_API int endpoint_mp3_open(const char* path, const uint8_t* data, int length, void** decoder, int* sampleRate,
                                   int* channels, int64_t* samples) {
    return Call([&] {
        auto d = path != nullptr ? new Sound::Mp3Decoder(path) : new Sound::Mp3Decoder(data, static_cast<size_t>(length));
        *decoder = d;
        *sampleRate = d->SampleRate();
        *channels = d->Channels();
        *samples = static_cast<int64_t>(d->Samples());
    });
}

// Up to count samples (interleaved): got 0 at the end
ENDPOINT_API int endpoint_mp3_read(void* decoder, int16_t* samples, int count, int* got) {
    return Call([&] { *got = static_cast<int>(Decoder(decoder)->Read(samples, static_cast<size_t>(count))); });
}

ENDPOINT_API int endpoint_mp3_close(void* decoder) {
    return Call([&] { delete Decoder(decoder); });
}
