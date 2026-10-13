#include "Sound.h"

#include <dlfcn.h>

#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <cstring>
#include <mutex>

#include "Errors.h"

extern "C" {
#include "../third-party/minimp3/minimp3_ex.h"
}

namespace GHIElectronics::Endpoint::Internal::Sound {

namespace {

// The ALSA functions libendpoint uses, from the board's libasound.so.2
struct Alsa {
    int (*pcm_open)(void** pcm, const char* name, int stream, int mode);
    int (*pcm_set_params)(void* pcm, int format, int access, unsigned channels, unsigned rate, int softResample,
                          unsigned latency);
    long (*pcm_writei)(void* pcm, const void* buffer, unsigned long frames);
    long (*pcm_readi)(void* pcm, void* buffer, unsigned long frames);
    int (*pcm_recover)(void* pcm, int error, int silent);
    int (*pcm_start)(void* pcm);
    int (*pcm_drain)(void* pcm);
    int (*pcm_close)(void* pcm);
    const char* (*strerror)(int error);
    int (*mixer_open)(void** mixer, int mode);
    int (*mixer_attach)(void* mixer, const char* name);
    int (*mixer_selem_register)(void* mixer, void* options, void** classp);
    int (*mixer_load)(void* mixer);
    int (*mixer_close)(void* mixer);
    void* (*mixer_first_elem)(void* mixer);
    void* (*mixer_elem_next)(void* elem);
    int (*has_playback_volume)(void* elem);
    int (*has_capture_volume)(void* elem);
    int (*has_playback_switch)(void* elem);
    int (*has_capture_switch)(void* elem);
    int (*is_playback_mono)(void* elem);
    int (*is_capture_mono)(void* elem);
    int (*get_playback_volume)(void* elem, int channel, long* value);
    int (*get_capture_volume)(void* elem, int channel, long* value);
    int (*set_playback_volume_all)(void* elem, long value);
    int (*set_capture_volume_all)(void* elem, long value);
    int (*get_playback_switch)(void* elem, int channel, int* value);
    int (*get_capture_switch)(void* elem, int channel, int* value);
    int (*set_playback_switch_all)(void* elem, int value);
    int (*set_capture_switch_all)(void* elem, int value);
};

// ALSA's values
constexpr int StreamPlayback = 0, StreamCapture = 1;
constexpr int AccessInterleaved = 3;
constexpr int FormatU8 = 1, FormatS16 = 2, FormatS32 = 10, FormatFloat = 14, FormatS24Packed = 32;
constexpr int FrontLeft = 0, FrontRight = 1;

const Alsa& Library() {
    static Alsa alsa{};
    static std::once_flag loaded;
    static std::string problem;
    std::call_once(loaded, [] {
        void* library = dlopen("libasound.so.2", RTLD_NOW | RTLD_LOCAL);
        if (library == nullptr) {
            problem = "The board has no sound library (libasound.so.2)";
            return;
        }
        auto get = [&](auto& function, const char* name) {
            function = reinterpret_cast<std::remove_reference_t<decltype(function)>>(dlsym(library, name));
            if (function == nullptr && problem.empty()) {
                problem = std::string("The board's sound library has no ") + name;
            }
        };
        get(alsa.pcm_open, "snd_pcm_open");
        get(alsa.pcm_set_params, "snd_pcm_set_params");
        get(alsa.pcm_writei, "snd_pcm_writei");
        get(alsa.pcm_readi, "snd_pcm_readi");
        get(alsa.pcm_recover, "snd_pcm_recover");
        get(alsa.pcm_start, "snd_pcm_start");
        get(alsa.pcm_drain, "snd_pcm_drain");
        get(alsa.pcm_close, "snd_pcm_close");
        get(alsa.strerror, "snd_strerror");
        get(alsa.mixer_open, "snd_mixer_open");
        get(alsa.mixer_attach, "snd_mixer_attach");
        get(alsa.mixer_selem_register, "snd_mixer_selem_register");
        get(alsa.mixer_load, "snd_mixer_load");
        get(alsa.mixer_close, "snd_mixer_close");
        get(alsa.mixer_first_elem, "snd_mixer_first_elem");
        get(alsa.mixer_elem_next, "snd_mixer_elem_next");
        get(alsa.has_playback_volume, "snd_mixer_selem_has_playback_volume");
        get(alsa.has_capture_volume, "snd_mixer_selem_has_capture_volume");
        get(alsa.has_playback_switch, "snd_mixer_selem_has_playback_switch");
        get(alsa.has_capture_switch, "snd_mixer_selem_has_capture_switch");
        get(alsa.is_playback_mono, "snd_mixer_selem_is_playback_mono");
        get(alsa.is_capture_mono, "snd_mixer_selem_is_capture_mono");
        get(alsa.get_playback_volume, "snd_mixer_selem_get_playback_volume");
        get(alsa.get_capture_volume, "snd_mixer_selem_get_capture_volume");
        get(alsa.set_playback_volume_all, "snd_mixer_selem_set_playback_volume_all");
        get(alsa.set_capture_volume_all, "snd_mixer_selem_set_capture_volume_all");
        get(alsa.get_playback_switch, "snd_mixer_selem_get_playback_switch");
        get(alsa.get_capture_switch, "snd_mixer_selem_get_capture_switch");
        get(alsa.set_playback_switch_all, "snd_mixer_selem_set_playback_switch_all");
        get(alsa.set_capture_switch_all, "snd_mixer_selem_set_capture_switch_all");
    });
    if (!problem.empty()) {
        ThrowNotSupported(problem);
    }
    return alsa;
}

void Check(int result, const std::string& what) {
    if (result < 0) {
        ThrowIO(what + ": " + Library().strerror(result));
    }
}

// "hw:1" is the card as it is (it may not take the file's format); "plughw:1"
// converts what it needs to. The mixer is the card: "hw:1".
std::string PcmName(const std::string& name) { return name.rfind("hw:", 0) == 0 ? "plug" + name : name; }
std::string MixerName(const std::string& name) { return name.rfind("plughw:", 0) == 0 ? name.substr(4) : name; }

int FormatOf(int audioFormat, int bits) {
    if (audioFormat == 3 && bits == 32) {
        return FormatFloat;
    }
    if (audioFormat == 1) {
        switch (bits) {
            case 8: return FormatU8;
            case 16: return FormatS16;
            case 24: return FormatS24Packed;
            case 32: return FormatS32;
        }
    }
    ThrowNotSupported("This WAV's sound format is not supported (PCM 8, 16, 24 or 32 bits, or 32-bit float)");
}

uint32_t U32(const uint8_t* p) { return p[0] | (p[1] << 8) | (p[2] << 16) | (static_cast<uint32_t>(p[3]) << 24); }
uint16_t U16(const uint8_t* p) { return static_cast<uint16_t>(p[0] | (p[1] << 8)); }

// A WAV's 44-byte header (PCM, 1 data chunk)
std::vector<uint8_t> WavHeader(unsigned rate, unsigned channels, unsigned bits, uint32_t dataSize) {
    std::vector<uint8_t> header(44);
    auto put32 = [&](size_t at, uint32_t v) { for (int i = 0; i < 4; i++) header[at + i] = static_cast<uint8_t>(v >> (8 * i)); };
    auto put16 = [&](size_t at, uint16_t v) { header[at] = static_cast<uint8_t>(v); header[at + 1] = static_cast<uint8_t>(v >> 8); };
    std::memcpy(&header[0], "RIFF", 4);
    put32(4, 36 + dataSize);
    std::memcpy(&header[8], "WAVEfmt ", 8);
    put32(16, 16);
    put16(20, 1);
    put16(22, static_cast<uint16_t>(channels));
    put32(24, rate);
    put32(28, rate * channels * bits / 8);
    put16(32, static_cast<uint16_t>(channels * bits / 8));
    put16(34, static_cast<uint16_t>(bits));
    std::memcpy(&header[36], "data", 4);
    put32(40, dataSize);
    return header;
}

int RecordingFormat(unsigned bits) {
    switch (bits) {
        case 8: return FormatU8;
        case 16: return FormatS16;
        case 24: return FormatS24Packed;
        case 32: return FormatS32;
    }
    ThrowArgument("RecordingBitsPerSample must be 8, 16, 24 or 32");
}

}  // namespace

Device::Device(Settings settings, bool unmute) : settings(std::move(settings)) {
    Library();
    // As .NET's SoundDevice.Create: sound on (a card without a mixer has nothing to unmute)
    if (unmute) {
        try {
            SetMute(false, false);
        }
        catch (const EndpointError&) {
        }
        try {
            SetMute(true, false);
        }
        catch (const EndpointError&) {
        }
    }
}

Device::~Device() {
    StopRecording();
    if (mixer != nullptr) {
        Library().mixer_close(mixer);
    }
}

void* Device::OpenPcm(bool capture, int format, unsigned channels, unsigned rate) {
    const Alsa& alsa = Library();
    std::string name = PcmName(capture ? settings.recordingDeviceName : settings.playbackDeviceName);
    void* pcm = nullptr;
    Check(alsa.pcm_open(&pcm, name.c_str(), capture ? StreamCapture : StreamPlayback, 0),
          std::string("Cannot open the ") + (capture ? "recording" : "playback") + " device \"" + name + "\"");
    int result = alsa.pcm_set_params(pcm, format, AccessInterleaved, channels, rate, 1, 500000);
    if (result < 0) {
        alsa.pcm_close(pcm);
        Check(result, "The sound device \"" + name + "\" cannot use " + std::to_string(rate) + " Hz, " +
                          std::to_string(channels) + " channel(s)");
    }
    // snd_pcm_set_params starts a recording only when one read asks for a
    // whole buffer: start it now (playback starts when its buffer is full)
    if (capture) {
        result = alsa.pcm_start(pcm);
        if (result < 0) {
            alsa.pcm_close(pcm);
            Check(result, "Cannot start recording on \"" + name + "\"");
        }
    }
    return pcm;
}

void Device::WritePcm(void* pcm, const uint8_t* data, size_t frames, size_t frameSize) {
    const Alsa& alsa = Library();
    while (frames > 0) {
        long written = alsa.pcm_writei(pcm, data, frames);
        if (written < 0) {
            // An underrun (the program was late) or a suspend: carry on
            int recovered = alsa.pcm_recover(pcm, static_cast<int>(written), 1);
            if (recovered < 0) {
                alsa.pcm_close(pcm);
                Check(recovered, "Cannot play");
            }
            continue;
        }
        data += static_cast<size_t>(written) * frameSize;
        frames -= static_cast<size_t>(written);
    }
}

void Device::PlayWav(const Reader& read) {
    uint8_t riff[12];
    if (read(riff, 12) != 12 || std::memcmp(riff, "RIFF", 4) != 0 || std::memcmp(riff + 8, "WAVE", 4) != 0) {
        ThrowArgument("This is not a WAV file");
    }
    // The chunks: "fmt " says what the sound is, "data" is the sound
    int audioFormat = 0, channels = 0, bits = 0;
    uint32_t rate = 0, dataSize = 0;
    bool haveFormat = false;
    while (true) {
        uint8_t chunk[8];
        if (read(chunk, 8) != 8) {
            ThrowArgument("This WAV file has no sound (no \"data\" chunk)");
        }
        uint32_t size = U32(chunk + 4);
        if (std::memcmp(chunk, "data", 4) == 0) {
            dataSize = size;
            break;
        }
        std::vector<uint8_t> body(size + (size & 1));
        if (read(body.data(), body.size()) != body.size()) {
            ThrowArgument("This WAV file is cut short");
        }
        if (std::memcmp(chunk, "fmt ", 4) == 0 && size >= 16) {
            audioFormat = U16(&body[0]);
            channels = U16(&body[2]);
            rate = U32(&body[4]);
            bits = U16(&body[14]);
            if (audioFormat == 0xFFFE && size >= 26) {
                audioFormat = U16(&body[24]);  // WAVE_FORMAT_EXTENSIBLE: the sub-format
            }
            haveFormat = true;
        }
    }
    if (!haveFormat || channels < 1 || rate == 0) {
        ThrowArgument("This WAV file does not say its sound format");
    }
    int format = FormatOf(audioFormat, bits);
    size_t frameSize = static_cast<size_t>(channels) * (bits / 8);
    void* pcm = OpenPcm(false, format, static_cast<unsigned>(channels), rate);
    // A stream may give 0 or 0xFFFFFFFF as its size: then to its end
    size_t left = (dataSize == 0 || dataSize == 0xFFFFFFFF) ? SIZE_MAX : dataSize;
    std::vector<uint8_t> buffer(frameSize * 4096);
    size_t pending = 0;
    while (left > 0) {
        size_t got = read(buffer.data() + pending, std::min(buffer.size() - pending, left));
        if (got == 0) {
            break;
        }
        left -= got;
        pending += got;
        size_t frames = pending / frameSize;
        WritePcm(pcm, buffer.data(), frames, frameSize);
        size_t rest = pending - frames * frameSize;
        std::memmove(buffer.data(), buffer.data() + frames * frameSize, rest);
        pending = rest;
    }
    Library().pcm_drain(pcm);
    Library().pcm_close(pcm);
}

namespace {

// Plays what the decoder gives (open: the playback device; write: to it)
void PlayDecoded(mp3dec_ex_t& decoder, const std::function<void*(int, unsigned, unsigned)>& open,
                 const std::function<void(void*, const uint8_t*, size_t, size_t)>& write) {
    if (decoder.info.channels < 1 || decoder.info.hz <= 0) {
        mp3dec_ex_close(&decoder);
        ThrowArgument("This is not an MP3 file");
    }
    unsigned channels = static_cast<unsigned>(decoder.info.channels);
    void* pcm = nullptr;
    try {
        pcm = open(FormatS16, channels, static_cast<unsigned>(decoder.info.hz));
        std::vector<int16_t> samples(4096 * channels);
        while (size_t got = mp3dec_ex_read(&decoder, samples.data(), samples.size())) {
            write(pcm, reinterpret_cast<const uint8_t*>(samples.data()), got / channels, channels * 2);
        }
    }
    catch (...) {
        mp3dec_ex_close(&decoder);
        throw;
    }
    mp3dec_ex_close(&decoder);
    Library().pcm_drain(pcm);
    Library().pcm_close(pcm);
}

}  // namespace

void Device::PlayMp3File(const std::string& path) {
    mp3dec_ex_t decoder{};
    int result = mp3dec_ex_open(&decoder, path.c_str(), MP3D_SEEK_TO_SAMPLE);
    if (result != 0) {
        if (result == MP3D_E_IOERROR) {
            errno = errno == 0 ? ENOENT : errno;
            ThrowSystemError("Cannot open " + path);
        }
        ThrowArgument(path + " is not an MP3 file");
    }
    PlayDecoded(decoder, [&](int format, unsigned channels, unsigned rate) { return OpenPcm(false, format, channels, rate); },
                [&](void* pcm, const uint8_t* data, size_t frames, size_t frameSize) { WritePcm(pcm, data, frames, frameSize); });
}

void Device::PlayMp3Buffer(const uint8_t* data, size_t length) {
    mp3dec_ex_t decoder{};
    if (mp3dec_ex_open_buf(&decoder, data, length, MP3D_SEEK_TO_SAMPLE) != 0) {
        ThrowArgument("This is not an MP3 file");
    }
    PlayDecoded(decoder, [&](int format, unsigned channels, unsigned rate) { return OpenPcm(false, format, channels, rate); },
                [&](void* pcm, const uint8_t* bytes, size_t frames, size_t frameSize) { WritePcm(pcm, bytes, frames, frameSize); });
}

void Device::Record(unsigned seconds, const std::function<void(const void*, size_t)>& write) {
    unsigned rate = settings.recordingSampleRate, channels = settings.recordingChannels, bits = settings.recordingBitsPerSample;
    int format = RecordingFormat(bits);
    size_t frameSize = static_cast<size_t>(channels) * bits / 8;
    size_t total = static_cast<size_t>(seconds) * rate;
    auto header = WavHeader(rate, channels, bits, static_cast<uint32_t>(total * frameSize));
    void* pcm = OpenPcm(true, format, channels, rate);
    const Alsa& alsa = Library();
    write(header.data(), header.size());
    std::vector<uint8_t> buffer(frameSize * 1024);
    while (total > 0) {
        long got = alsa.pcm_readi(pcm, buffer.data(), std::min<size_t>(1024, total));
        if (got < 0) {
            int recovered = alsa.pcm_recover(pcm, static_cast<int>(got), 1);
            if (recovered < 0) {
                alsa.pcm_close(pcm);
                Check(recovered, "Cannot record");
            }
            continue;
        }
        write(buffer.data(), static_cast<size_t>(got) * frameSize);
        total -= static_cast<size_t>(got);
    }
    alsa.pcm_close(pcm);
}

void Device::StartRecording(const std::string& path) {
    if (recording) {
        ThrowInvalidOperation("The device is recording already: call StopRecording first");
    }
    unsigned rate = settings.recordingSampleRate, channels = settings.recordingChannels, bits = settings.recordingBitsPerSample;
    int format = RecordingFormat(bits);
    FILE* file = std::fopen(path.c_str(), "wb");
    if (file == nullptr) {
        ThrowSystemError("Cannot write " + path);
    }
    void* pcm = nullptr;
    try {
        pcm = OpenPcm(true, format, channels, rate);
    }
    catch (...) {
        std::fclose(file);
        throw;
    }
    auto header = WavHeader(rate, channels, bits, 0);
    std::fwrite(header.data(), 1, header.size(), file);
    recording = true;
    recorder = std::thread([this, pcm, file, rate, channels, bits] {
        const Alsa& alsa = Library();
        size_t frameSize = static_cast<size_t>(channels) * bits / 8;
        std::vector<uint8_t> buffer(frameSize * 1024);
        uint64_t bytes = 0;
        while (recording) {
            long got = alsa.pcm_readi(pcm, buffer.data(), 1024);
            if (got < 0) {
                if (alsa.pcm_recover(pcm, static_cast<int>(got), 1) < 0) {
                    break;
                }
                continue;
            }
            std::fwrite(buffer.data(), 1, static_cast<size_t>(got) * frameSize, file);
            bytes += static_cast<uint64_t>(got) * frameSize;
        }
        alsa.pcm_close(pcm);
        // The sizes, now that they are known
        auto header = WavHeader(rate, channels, bits, static_cast<uint32_t>(std::min<uint64_t>(bytes, 0xFFFFFFDBu)));
        std::fseek(file, 0, SEEK_SET);
        std::fwrite(header.data(), 1, header.size(), file);
        std::fclose(file);
    });
}

void Device::StopRecording() {
    recording = false;
    if (recorder.joinable()) {
        recorder.join();
    }
}

// The mixer control for playback or recording volume (or switch): the first
// one the card has for it
void* Device::MixerElement(bool capture, bool volume) {
    const Alsa& alsa = Library();
    if (mixer == nullptr) {
        std::string name = MixerName(settings.mixerDeviceName);
        void* opened = nullptr;
        Check(alsa.mixer_open(&opened, 0), "Cannot open the mixer");
        int result = alsa.mixer_attach(opened, name.c_str());
        if (result >= 0) result = alsa.mixer_selem_register(opened, nullptr, nullptr);
        if (result >= 0) result = alsa.mixer_load(opened);
        if (result < 0) {
            alsa.mixer_close(opened);
            Check(result, "Cannot open the mixer of \"" + name + "\"");
        }
        mixer = opened;
    }
    for (void* elem = alsa.mixer_first_elem(mixer); elem != nullptr; elem = alsa.mixer_elem_next(elem)) {
        bool fits = capture ? (volume ? alsa.has_capture_volume(elem) : alsa.has_capture_switch(elem))
                            : (volume ? alsa.has_playback_volume(elem) : alsa.has_playback_switch(elem));
        if (fits) {
            return elem;
        }
    }
    ThrowNotSupported(std::string("The sound card has no ") + (capture ? "recording " : "playback ") +
                      (volume ? "volume" : "mute switch"));
}

long Device::GetVolume(bool capture) {
    const Alsa& alsa = Library();
    void* elem = MixerElement(capture, true);
    auto get = capture ? alsa.get_capture_volume : alsa.get_playback_volume;
    long left = 0, right = 0;
    Check(get(elem, FrontLeft, &left), "Cannot read the volume");
    // As .NET's SoundDevice: the left and right channels' mean
    if ((capture ? alsa.is_capture_mono : alsa.is_playback_mono)(elem) || get(elem, FrontRight, &right) < 0) {
        return left;
    }
    return (left + right) / 2;
}

void Device::SetVolume(bool capture, long volume) {
    const Alsa& alsa = Library();
    void* elem = MixerElement(capture, true);
    Check((capture ? alsa.set_capture_volume_all : alsa.set_playback_volume_all)(elem, volume), "Cannot set the volume");
}

bool Device::GetMute(bool capture) {
    const Alsa& alsa = Library();
    void* elem = MixerElement(capture, false);
    int on = 1;
    Check((capture ? alsa.get_capture_switch : alsa.get_playback_switch)(elem, FrontLeft, &on), "Cannot read the mute switch");
    return on == 0;
}

void Device::SetMute(bool capture, bool mute) {
    const Alsa& alsa = Library();
    void* elem = MixerElement(capture, false);
    Check((capture ? alsa.set_capture_switch_all : alsa.set_playback_switch_all)(elem, mute ? 0 : 1), "Cannot set the mute switch");
}

// ---- Mp3Decoder

struct Mp3Decoder::State {
    mp3dec_ex_t decoder{};
    std::vector<uint8_t> data;  // a copy, when decoded from memory
};

Mp3Decoder::Mp3Decoder(const std::string& path) : state(std::make_unique<State>()) {
    int result = mp3dec_ex_open(&state->decoder, path.c_str(), MP3D_SEEK_TO_SAMPLE);
    if (result == MP3D_E_IOERROR) {
        errno = errno == 0 ? ENOENT : errno;
        ThrowSystemError("Cannot open " + path);
    }
    if (result != 0 || state->decoder.info.channels < 1) {
        mp3dec_ex_close(&state->decoder);
        ThrowArgument(path + " is not an MP3 file");
    }
}

Mp3Decoder::Mp3Decoder(const uint8_t* data, size_t length) : state(std::make_unique<State>()) {
    state->data.assign(data, data + length);
    if (mp3dec_ex_open_buf(&state->decoder, state->data.data(), state->data.size(), MP3D_SEEK_TO_SAMPLE) != 0 ||
        state->decoder.info.channels < 1) {
        mp3dec_ex_close(&state->decoder);
        ThrowArgument("This is not an MP3 file");
    }
}

Mp3Decoder::~Mp3Decoder() { mp3dec_ex_close(&state->decoder); }

int Mp3Decoder::SampleRate() const { return state->decoder.info.hz; }

int Mp3Decoder::Channels() const { return state->decoder.info.channels; }

uint64_t Mp3Decoder::Samples() const { return state->decoder.samples; }

size_t Mp3Decoder::Read(int16_t* samples, size_t count) { return mp3dec_ex_read(&state->decoder, samples, count); }

}  // namespace GHIElectronics::Endpoint::Internal::Sound
