// Board test of the C++ SoundDevice and Printer (only <endpoint.h>, linked with
// libendpoint.a), with the kernel's test sound cards: snd-aloop (played on
// "hw:Loopback,0", recorded on "hw:Loopback,1") and snd-dummy (a mixer). Run
// as root on the board, next to tone440.wav (1 s, 440 Hz) and tone1000.mp3
// (1 s, 1000 Hz):
//
//     ./cpp-media-test

#include <endpoint.h>

#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <functional>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

using namespace GHIElectronics::Endpoint::Core;
using namespace GHIElectronics::Endpoint::Devices;
using namespace std::chrono_literals;

static int failures = 0;

static void Check(bool ok, const std::string& what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what.c_str());
    if (!ok) failures++;
}

// The main frequency of a 16-bit mono WAV, from the zero crossings in the middle of its sound
static double Frequency(const std::string& wav) {
    uint32_t rate;
    std::memcpy(&rate, wav.data() + 24, 4);
    std::vector<int16_t> samples((wav.size() - 44) / 2);
    std::memcpy(samples.data(), wav.data() + 44, samples.size() * 2);
    size_t first = 0, last = 0;
    bool any = false;
    for (size_t i = 0; i < samples.size(); i++) {
        if (std::abs(samples[i]) > 3000) {
            if (!any) first = i;
            last = i;
            any = true;
        }
    }
    if (!any || last - first < 100) return 0;
    size_t from = first + (last - first) / 5, to = last - (last - first) / 5;
    int crossings = 0;
    for (size_t i = from; i < to; i++) {
        if ((samples[i] < 0) != (samples[i + 1] < 0)) crossings++;
    }
    return crossings / 2.0 / ((to - from) / static_cast<double>(rate));
}

int main() {
    try {
        UsbHost::Printer printer("/dev/null");
        Check(false, "Printer(/dev/null): no exception");
    }
    catch (const ArgumentException& e) {
        Check(true, std::string("Printer(/dev/null): ") + e.what());
    }

    Media::SoundConnectionSettings playing;
    playing.PlaybackDeviceName = "hw:Loopback,0";
    playing.MixerDeviceName = "hw:Dummy";
    auto player = Media::SoundDevice::Create(playing);
    Media::SoundConnectionSettings recording;
    recording.RecordingDeviceName = "hw:Loopback,1";
    recording.RecordingSampleRate = 16000;
    recording.RecordingChannels = 1;
    auto recorder = Media::SoundDevice::Create(recording, false);

    struct Case {
        const char* name;
        int hz;
        std::function<void()> play;
    };
    std::vector<Case> cases = {
        {"PlayMp3(path)", 1000, [&] { player.PlayMp3("tone1000.mp3"); }},
        {"PlayMp3(stream)", 1000, [&] { std::ifstream mp3("tone1000.mp3", std::ios::binary); player.PlayMp3(mp3); }},
        {"Play(path)", 440, [&] { player.Play("tone440.wav"); }},
    };
    for (auto& c : cases) {
        std::ostringstream output;
        std::thread record([&] { recorder.Record(2, output); });
        std::this_thread::sleep_for(300ms);
        auto t = std::chrono::steady_clock::now();
        c.play();
        double took = std::chrono::duration<double>(std::chrono::steady_clock::now() - t).count();
        record.join();
        double found = Frequency(output.str());
        char line[160];
        std::snprintf(line, sizeof line, "%s: %.2f s, recorded %.0f Hz (played %d Hz)", c.name, took, found, c.hz);
        Check(std::fabs(found - c.hz) < c.hz * 0.03 && took > 0.9 && took < 2.5, line);
    }

    try {
        player.PlayMp3("tone440.wav");
        Check(false, "PlayMp3 of a WAV: no exception");
    }
    catch (const ArgumentException& e) {
        Check(true, std::string("PlayMp3 of a WAV: ") + e.what());
    }
    player.PlaybackVolume(37);
    Check(player.PlaybackVolume() == 37, "PlaybackVolume = 37 (dummy card): " + std::to_string(player.PlaybackVolume()));
    try {
        player.PlaybackMute(true);
        Check(false, "PlaybackMute on a card without a playback switch: no exception");
    }
    catch (const NotSupportedException& e) {
        Check(true, std::string("PlaybackMute on a card without a playback switch: ") + e.what());
    }

    std::printf("\n%s\n", failures == 0 ? "ALL PASSED" : (std::to_string(failures) + " FAILED").c_str());
    return failures == 0 ? 0 : 1;
}
