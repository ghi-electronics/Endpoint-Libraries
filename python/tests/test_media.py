# Board test of the Python SoundDevice (GHIElectronics.Endpoint.Devices.Media)
# and Printer's errors, with the kernel's test sound cards (snd-aloop: played on
# "hw:Loopback,0", recorded on "hw:Loopback,1"; snd-dummy: a mixer). Run as root
# on the board with the packages on the path, next to tone440.wav (1 s, 440 Hz)
# and tone1000.mp3 (1 s, 1000 Hz).
import io
import os
import struct
import sys
import threading
import time

from GHIElectronics.Endpoint.Core import ArgumentException, NotSupportedException
from GHIElectronics.Endpoint.Devices.Media import SoundConnectionSettings, SoundDevice
from GHIElectronics.Endpoint.Devices.UsbHost import Printer

here = os.path.dirname(os.path.abspath(__file__))
failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def frequency(data):
    """The main frequency of a 16-bit mono WAV, from the zero crossings in the middle of its sound"""
    rate = struct.unpack_from("<I", data, 24)[0]
    samples = struct.unpack_from(f"<{(len(data) - 44) // 2}h", data, 44)
    loud = [i for i, s in enumerate(samples) if abs(s) > 3000]
    if len(loud) < 100:
        return 0
    part = samples[loud[0] + (loud[-1] - loud[0]) // 5:loud[-1] - (loud[-1] - loud[0]) // 5]
    return sum(1 for a, b in zip(part, part[1:]) if (a < 0) != (b < 0)) / 2 / (len(part) / rate)


try:
    Printer("/dev/null")
    check(False, "Printer('/dev/null'): no exception")
except ArgumentException as e:
    check(True, f"Printer('/dev/null'): {e}")

settings = SoundConnectionSettings()
settings.PlaybackDeviceName = "hw:Loopback,0"
settings.MixerDeviceName = "hw:Dummy"
player = SoundDevice.Create(settings)
recording = SoundConnectionSettings()
recording.RecordingDeviceName = "hw:Loopback,1"
recording.RecordingSampleRate = 16000
recording.RecordingChannels = 1
recorder = SoundDevice.Create(recording, False)

wav, mp3 = os.path.join(here, "tone440.wav"), os.path.join(here, "tone1000.mp3")
for name, hz, play in (("PlayMp3(path)", 1000, lambda: player.PlayMp3(mp3)),
                       ("PlayMp3(open file)", 1000, lambda: player.PlayMp3(open(mp3, "rb"))),
                       ("Play(path)", 440, lambda: player.Play(wav)),
                       ("Play(bytes)", 440, lambda: player.Play(open(wav, "rb").read()))):
    output = io.BytesIO()
    thread = threading.Thread(target=lambda: recorder.Record(2, output))
    thread.start()
    time.sleep(0.3)
    t = time.monotonic()
    play()
    took = time.monotonic() - t
    thread.join()
    found = frequency(output.getvalue())
    check(abs(found - hz) < hz * 0.03 and 0.9 < took < 2.5, f"{name}: {took:.2f} s, recorded {found:.0f} Hz (played {hz} Hz)")

recorder.StartRecording("/tmp/py-sound-test.wav")
time.sleep(0.3)
player.Play(wav)
time.sleep(0.3)
recorder.StopRecording()
found = frequency(open("/tmp/py-sound-test.wav", "rb").read())
check(abs(found - 440) < 440 * 0.03, f"StartRecording/StopRecording: recorded {found:.0f} Hz")

try:
    player.PlayMp3(wav)
    check(False, "PlayMp3 of a WAV: no exception")
except ArgumentException as e:
    check(True, f"PlayMp3 of a WAV: {e}")

player.PlaybackVolume = 37
check(player.PlaybackVolume == 37, f"PlaybackVolume = 37 (dummy card): {player.PlaybackVolume}")
try:
    player.PlaybackMute = True
    check(False, "PlaybackMute on a card without a playback switch: no exception")
except NotSupportedException as e:
    check(True, f"PlaybackMute on a card without a playback switch: {e}")
player.RecordingMute = True
check(player.RecordingMute, "RecordingMute = True")
player.RecordingMute = False
player.Dispose()
recorder.Dispose()

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
