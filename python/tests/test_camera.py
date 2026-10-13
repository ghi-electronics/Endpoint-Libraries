# Board test of the Python Camera (GHIElectronics.Endpoint.Devices.Media) with a
# real USB webcam, found the customer way (UsbHostController). Run as root on
# the board with the packages on the path:
#
#     python3 test_camera.py [hub folder to switch off for the unplug test]
import sys
import threading
import time

from GHIElectronics.Endpoint.Core import (ArgumentException, ArgumentOutOfRangeException, DeviceConnectionStatus,
                                          IOException)
from GHIElectronics.Endpoint.Devices.Media import Camera, CameraControl, CameraPixelFormat
from GHIElectronics.Endpoint.Devices.UsbHost import DeviceType, UsbHostController

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def valid(jpeg):
    return jpeg[:2] == b"\xff\xd8" and jpeg[-2:] == b"\xff\xd9"


found = []
usb = UsbHostController.GetDefault()
usb.OnConnectionChangedEvent += lambda s, e: found.append(e.DeviceName) if (
    e.Type == DeviceType.Webcam and e.DeviceStatus == DeviceConnectionStatus.Connected) else None
usb.Enable()
time.sleep(1)
usb.Dispose()
if not found:
    sys.exit("FAIL  no webcam plugged in")

camera = Camera(found[0])
check(len(camera.SupportedFormats) > 0, f"{camera!r} '{camera.Name}': {camera.SupportedFormats}")
check((camera.Width, camera.Height) == (640, 480), f"at first {camera.Width}x{camera.Height} {camera.PixelFormat.name} {camera.FrameRate} fps")
t = time.monotonic()
jpeg = camera.Capture()
check(valid(jpeg) and time.monotonic() - t > 0.5, f"first picture: {len(jpeg)} bytes in {time.monotonic() - t:.2f} s")
t = time.monotonic()
camera.Capture("/tmp/py-camera.jpg")
check(valid(open("/tmp/py-camera.jpg", "rb").read()) and time.monotonic() - t < 0.3, f"Capture(path): {time.monotonic() - t:.2f} s")
try:
    camera.SetResolution(1000, 700)
    check(False, "1000x700: no exception")
except ArgumentException as e:
    check(True, f"1000x700: {e}")
camera.SetResolution(1280, 720)
jpeg = camera.Capture()
check(valid(jpeg) and camera.Width == 1280, f"1280x720 {camera.PixelFormat.name}: {len(jpeg)} bytes")
yuyv = [f for f in camera.SupportedFormats if f.PixelFormat == CameraPixelFormat.Yuyv and f.Width == 640]
if yuyv:
    camera.SetResolution(yuyv[0])
    camera.JpegQuality = 70
    jpeg = camera.Capture()
    check(valid(jpeg) and camera.PixelFormat == CameraPixelFormat.Yuyv, f"{yuyv[0]} (JPEG quality 70): {len(jpeg)} bytes")

camera.SetResolution(640, 480)
frames = []
camera.FrameReceived += lambda sender, e: frames.append((valid(e.Jpeg), e.Width))
try:
    camera.FrameReceived = None
    check(False, "= instead of +=: no exception")
except TypeError:
    pass
camera.StartStreaming()
time.sleep(3)
camera.StopStreaming()
check(len(frames) > 40 and all(ok and width == 640 for ok, width in frames), f"streaming: {len(frames)} frames in 3 s, each a whole JPEG")

check(CameraControl.Brightness in camera.SupportedControls, f"controls: {[c.name for c in camera.SupportedControls]}")
brightness = camera.GetControl(CameraControl.Brightness)
camera.SetControl(CameraControl.Brightness, brightness.Maximum // 2)
check(camera.GetControl(CameraControl.Brightness).Value == brightness.Maximum // 2, f"{brightness!r} -> {camera.GetControl(CameraControl.Brightness).Value}")
camera.SetControl(CameraControl.Brightness, brightness.Value)
try:
    camera.SetControl(CameraControl.Brightness, brightness.Maximum + 1)
    check(False, "Brightness too high: no exception")
except ArgumentOutOfRangeException as e:
    check(True, f"Brightness too high: {e}")

if len(sys.argv) > 1:
    unplugged = threading.Event()
    camera.Disconnected += lambda sender, e: unplugged.set()
    with open(sys.argv[1] + "/authorized", "w") as f:
        f.write("0")
    check(unplugged.wait(5) and not camera.Connected, "unplugged (its hub switched off): Disconnected")
    try:
        camera.Capture(timeout=2000)
        check(False, "a picture after unplugging: no exception")
    except IOException as e:
        check(True, f"a picture after unplugging: {e}")
    with open(sys.argv[1] + "/authorized", "w") as f:
        f.write("1")
    time.sleep(3)
camera.Dispose()

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
