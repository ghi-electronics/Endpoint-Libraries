# Board test of libendpoint's sound (SoundDevice for Python and C++, the MP3
# decoder for .NET), through its C layer, with the kernel's test sound cards:
# snd-aloop (what is played on "hw:Loopback,0" is recorded on "hw:Loopback,1")
# and snd-dummy (a mixer with volumes and switches). Run as root on the board,
# next to libendpoint.so, tone440.wav (1 s, 440 Hz, stereo) and tone1000.mp3
# (1 s, 1000 Hz, mono):
#
#     modprobe snd-aloop; modprobe snd-dummy; python3 sound_test.py
import ctypes
import os
import struct
import subprocess
import sys
import threading
import time

here = os.path.dirname(os.path.abspath(__file__))
lib = ctypes.CDLL(os.path.join(here, "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
lib.endpoint_sound_set_volume.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int64]
for name in ("dispose", "play_file", "play_buffer", "record_file", "record_buffer", "start_recording",
             "stop_recording", "get_volume", "get_mute", "set_mute"):
    getattr(lib, "endpoint_sound_" + name).argtypes = None

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def create(playback="default", recording="default", mixer="default", rate=8000, channels=2, bits=16):
    device = ctypes.c_void_p()
    result = lib.endpoint_sound_create(playback.encode(), recording.encode(), mixer.encode(), rate, channels, bits, 1,
                                       ctypes.byref(device))
    if result != 0:
        raise RuntimeError(lib.endpoint_last_error().decode())
    return device


def frequency(path):
    """The main frequency of a recorded 16-bit WAV (channel 0), from its zero crossings"""
    data = open(path, "rb").read()
    channels, rate = struct.unpack_from("<HI", data, 22)
    samples = struct.unpack_from(f"<{(len(data) - 44) // 2}h", data, 44)[::channels]
    loud = [i for i, s in enumerate(samples) if abs(s) > 3000]
    if len(loud) < 100:
        return 0, 0
    # The middle of the sound (an MP3 encoder adds a little at its start and end)
    seconds = (loud[-1] - loud[0]) / rate
    part = samples[loud[0] + (loud[-1] - loud[0]) // 5:loud[-1] - (loud[-1] - loud[0]) // 5]
    crossings = sum(1 for a, b in zip(part, part[1:]) if (a < 0) != (b < 0))
    return crossings / 2 / (len(part) / rate), seconds


for module in ("snd-aloop", "snd-dummy"):
    if subprocess.run(["modprobe", module], capture_output=True).returncode != 0 and os.path.exists(os.path.join(here, module + ".ko")):
        subprocess.run(["insmod", os.path.join(here, module + ".ko")], capture_output=True)
cards = open("/proc/asound/cards").read()
if "Loopback" not in cards or "Dummy" not in cards:
    sys.exit("The test sound cards are missing: modprobe snd-aloop; modprobe snd-dummy")

wav = os.path.join(here, "tone440.wav")
mp3 = os.path.join(here, "tone1000.mp3")
recorded = "/tmp/endpoint-sound-test.wav"

player = create(playback="hw:Loopback,0", mixer="hw:Loopback")
recorder = create(recording="hw:Loopback,1", mixer="hw:Loopback", rate=16000, channels=1)

for name, file, is_mp3, hz in (("WAV", wav, 0, 440), ("MP3", mp3, 1, 1000)):
    check(lib.endpoint_sound_start_recording(recorder, recorded.encode()) == 0, f"StartRecording (16 kHz mono) for the {name}")
    time.sleep(0.3)
    t = time.monotonic()
    result = lib.endpoint_sound_play_file(player, file.encode(), is_mp3)
    took = time.monotonic() - t
    check(result == 0 and 0.9 < took < 2.5, f"Play the {name} file: {took:.2f} s" + ("" if result == 0 else f" ({lib.endpoint_last_error().decode()})"))
    time.sleep(0.3)
    check(lib.endpoint_sound_stop_recording(recorder) == 0, "StopRecording")
    found, seconds = frequency(recorded)
    check(abs(found - hz) < hz * 0.03 and 0.9 < seconds < 1.2, f"recorded: {found:.0f} Hz for {seconds:.2f} s (played {hz} Hz)")

# Record (seconds) into memory while the WAV plays from memory
data = open(wav, "rb").read()
playing = threading.Thread(target=lambda: lib.endpoint_sound_play_buffer(player, data, len(data), 0))
size = 44 + 2 * 16000 * 2
buffer = (ctypes.c_uint8 * size)()
length = ctypes.c_int()
playing.start()
check(lib.endpoint_sound_record_buffer(recorder, 2, buffer, size, ctypes.byref(length)) == 0 and length.value == size,
      f"Record(2 s) into memory while playing from memory: {length.value} bytes")
playing.join()
open(recorded, "wb").write(bytes(buffer))
found, seconds = frequency(recorded)
check(abs(found - 440) < 440 * 0.03, f"recorded: {found:.0f} Hz for {seconds:.2f} s")
mp3data = open(mp3, "rb").read()
check(lib.endpoint_sound_play_buffer(player, mp3data, len(mp3data), 1) == 0, "Play an MP3 from memory")

# Errors
check(lib.endpoint_sound_play_file(player, mp3.encode(), 0) == 1, f"an MP3 played as a WAV: {lib.endpoint_last_error().decode()}")
check(lib.endpoint_sound_play_file(player, wav.encode(), 1) == 1, f"a WAV played as an MP3: {lib.endpoint_last_error().decode()}")
check(lib.endpoint_sound_play_file(player, b"/no/such.wav", 0) == 4, f"no file: {lib.endpoint_last_error().decode()}")
nowhere = create(playback="hw:9")
check(lib.endpoint_sound_play_file(nowhere, wav.encode(), 0) == 4, f"a card that is not there: {lib.endpoint_last_error().decode()}")
lib.endpoint_sound_dispose(nowhere)
lib.endpoint_sound_dispose(player)
lib.endpoint_sound_dispose(recorder)

# Volume and mute on the dummy card's mixer: its "Master" has one volume for
# playing and recording, and only a recording switch
mixer = create(mixer="hw:Dummy")
volume = ctypes.c_int64()
check(lib.endpoint_sound_get_volume(mixer, 0, ctypes.byref(volume)) == 0, f"PlaybackVolume: {volume.value}")
check(lib.endpoint_sound_set_volume(mixer, 0, 37) == 0 and lib.endpoint_sound_get_volume(mixer, 0, ctypes.byref(volume)) == 0
      and volume.value == 37, f"PlaybackVolume = 37: {volume.value}")
check("Front Left: 37" in subprocess.run(["amixer", "-c", "Dummy", "sget", "Master"], capture_output=True, text=True).stdout,
      "amixer sees 37")
check(lib.endpoint_sound_set_volume(mixer, 1, 12) == 0 and lib.endpoint_sound_get_volume(mixer, 1, ctypes.byref(volume)) == 0
      and volume.value == 12, f"RecordingVolume = 12: {volume.value}")
mute = ctypes.c_int()
check(lib.endpoint_sound_get_mute(mixer, 0, ctypes.byref(mute)) == 5, f"PlaybackMute: {lib.endpoint_last_error().decode()}")
check(lib.endpoint_sound_get_mute(mixer, 1, ctypes.byref(mute)) == 0 and mute.value == 0, "RecordingMute is false (Create unmutes)")
check(lib.endpoint_sound_set_mute(mixer, 1, 1) == 0 and lib.endpoint_sound_get_mute(mixer, 1, ctypes.byref(mute)) == 0
      and mute.value == 1, "RecordingMute = true")
check("Capture [off]" in subprocess.run(["amixer", "-c", "Dummy", "sget", "Master"], capture_output=True, text=True).stdout,
      "amixer sees the recording muted")
lib.endpoint_sound_set_mute(mixer, 1, 0)
lib.endpoint_sound_dispose(mixer)

# The MP3 decoder (.NET's PlayMp3)
decoder = ctypes.c_void_p()
rate, channels, samples = ctypes.c_int(), ctypes.c_int(), ctypes.c_int64()
check(lib.endpoint_mp3_open(mp3.encode(), None, 0, ctypes.byref(decoder), ctypes.byref(rate), ctypes.byref(channels),
                            ctypes.byref(samples)) == 0 and (rate.value, channels.value) == (44100, 1) and 44100 <= samples.value < 48000,
      f"MP3 decoder: {rate.value} Hz, {channels.value} channel(s), {samples.value} samples")
block = (ctypes.c_int16 * 4096)()
got, total = ctypes.c_int(), 0
while lib.endpoint_mp3_read(decoder, block, 4096, ctypes.byref(got)) == 0 and got.value > 0:
    total += got.value
check(total == samples.value, f"read {total} samples")
lib.endpoint_mp3_close(decoder)
check(lib.endpoint_mp3_open(wav.encode(), None, 0, ctypes.byref(decoder), ctypes.byref(rate), ctypes.byref(channels),
                            ctypes.byref(samples)) == 1, f"a WAV in the MP3 decoder: {lib.endpoint_last_error().decode()}")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
