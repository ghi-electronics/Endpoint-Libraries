# Board test of libendpoint's Camera, through its C layer, with a real USB
# webcam (any: MJPEG or YUYV). Run as root on the board, next to libendpoint.so:
#
#     python3 camera_test.py [/dev/video0] [hub folder to switch off for the unplug test]
#
# Pictures (the first one after the exposure settled, the next ones at once),
# sizes, MJPEG and YUYV, streaming, controls, errors, and unplugging. The
# pictures are saved in /tmp/camera-*.jpg to look at.
import ctypes
import os
import sys
import threading
import time

lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
Frame = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint8), ctypes.c_int, ctypes.c_int, ctypes.c_int)
Unplugged = ctypes.CFUNCTYPE(None, ctypes.c_void_p)
for name in ("close", "info", "format", "get_resolution", "set_resolution", "capture", "free", "start_streaming",
             "stop_streaming", "get_control", "set_control", "set_quality"):
    getattr(lib, "endpoint_camera_" + name).argtypes = None

BRIGHTNESS, AUTO_EXPOSURE, EXPOSURE, FOCUS = 0, 10, 11, 14
failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def error():
    return lib.endpoint_last_error().decode()


def capture(camera, timeout=5000):
    data, length = ctypes.POINTER(ctypes.c_uint8)(), ctypes.c_int()
    t = time.monotonic()
    result = lib.endpoint_camera_capture(camera, timeout, ctypes.byref(data), ctypes.byref(length))
    took = time.monotonic() - t
    if result != 0:
        return result, b"", took
    jpeg = ctypes.string_at(data, length.value)
    lib.endpoint_camera_free(data)
    return 0, jpeg, took


def valid_jpeg(jpeg):
    return jpeg[:2] == b"\xff\xd8" and jpeg.rstrip(b"\0")[-2:] == b"\xff\xd9" and b"\xff\xc4" in jpeg


name = sys.argv[1] if len(sys.argv) > 1 else "/dev/video0"
hub = sys.argv[2] if len(sys.argv) > 2 else None

bad = ctypes.c_void_p()
check(lib.endpoint_camera_open(b"/dev/video1", None, None, None, ctypes.byref(bad)) == 1, f"/dev/video1: {error()}")

frames = []
unplugged = threading.Event()
on_frame = Frame(lambda context, jpeg, length, width, height: frames.append((ctypes.string_at(jpeg, length), width, height)))
on_unplug = Unplugged(lambda context: unplugged.set())
camera = ctypes.c_void_p()
check(lib.endpoint_camera_open(name.encode(), on_frame, on_unplug, None, ctypes.byref(camera)) == 0, f"opened {name}")
text, connected, count = ctypes.create_string_buffer(64), ctypes.c_int(), ctypes.c_int()
lib.endpoint_camera_info(camera, text, 64, ctypes.byref(connected), ctypes.byref(count))
check(connected.value == 1 and count.value > 0, f"'{text.value.decode()}': {count.value} formats")
w, h, f, r = (ctypes.c_int() for _ in range(4))
sizes = []
for i in range(count.value):
    lib.endpoint_camera_format(camera, i, ctypes.byref(w), ctypes.byref(h), ctypes.byref(f), ctypes.byref(r))
    sizes.append((w.value, h.value, "MJPEG" if f.value == 0 else "YUYV", r.value))
print("      " + ", ".join(f"{a}x{b} {c} {d} fps" for a, b, c, d in sizes))
lib.endpoint_camera_get_resolution(camera, ctypes.byref(w), ctypes.byref(h), ctypes.byref(f), ctypes.byref(r))
print(f"      at first: {w.value}x{h.value} {'MJPEG' if f.value == 0 else 'YUYV'} {r.value} fps")

result, jpeg, took = capture(camera)
open("/tmp/camera-first.jpg", "wb").write(jpeg)
check(result == 0 and valid_jpeg(jpeg) and 0.5 < took < 3, f"first picture: {len(jpeg)} bytes in {took:.2f} s (exposure settled), a whole JPEG")
result, jpeg, took = capture(camera)
check(result == 0 and valid_jpeg(jpeg) and took < 0.3, f"next picture: {len(jpeg)} bytes in {took:.2f} s")

check(lib.endpoint_camera_set_resolution(camera, 1000, 700, 0, -1) == 1, f"1000x700: {error()}")
for width, height, pixel in ((1920, 1080, -1), (640, 480, 1), (1280, 720, 1)):
    if not any(s[0] == width and s[1] == height and (pixel < 0 or s[2] == ("MJPEG", "YUYV")[pixel]) for s in sizes):
        continue
    check(lib.endpoint_camera_set_resolution(camera, width, height, 0, pixel) == 0, f"SetResolution {width}x{height} {('any', 'MJPEG', 'YUYV')[pixel + 1]}")
    result, jpeg, took = capture(camera)
    path = f"/tmp/camera-{width}x{height}-{('auto', 'mjpeg', 'yuyv')[pixel + 1]}.jpg"
    open(path, "wb").write(jpeg)
    check(result == 0 and valid_jpeg(jpeg), f"  picture: {len(jpeg)} bytes in {took:.2f} s -> {path}" + ("" if result == 0 else f" ({error()})"))

lib.endpoint_camera_set_resolution(camera, 640, 480, 0, -1)
frames.clear()
check(lib.endpoint_camera_start_streaming(camera) == 0, "StartStreaming 640x480")
time.sleep(3)
check(lib.endpoint_camera_stop_streaming(camera) == 0, "StopStreaming")
got = len(frames)
check(got > 40 and all(valid_jpeg(j) and (fw, fh) == (640, 480) for j, fw, fh in frames),
      f"{got} frames in 3 s ({got / 3:.0f} fps), each a whole 640x480 JPEG")
time.sleep(0.3)
after = len(frames)
time.sleep(0.5)
check(len(frames) == after, "no frames after StopStreaming")

supported, low, high, step, default, value = (ctypes.c_int() for _ in range(6))
refs = [ctypes.byref(v) for v in (supported, low, high, step, default, value)]
check(lib.endpoint_camera_get_control(camera, BRIGHTNESS, *refs) == 0 and supported.value == 1,
      f"Brightness {value.value} ({low.value} to {high.value}, default {default.value})")
old = value.value
lib.endpoint_camera_set_control(camera, BRIGHTNESS, high.value // 2)
lib.endpoint_camera_get_control(camera, BRIGHTNESS, *refs)
check(value.value == high.value // 2, f"Brightness = {high.value // 2}: {value.value}")
check(lib.endpoint_camera_set_control(camera, BRIGHTNESS, high.value + 1) == 2, f"Brightness too high: {error()}")
lib.endpoint_camera_set_control(camera, BRIGHTNESS, old)
lib.endpoint_camera_get_control(camera, AUTO_EXPOSURE, *refs)
check(supported.value == 1 and value.value in (0, 1), f"AutoExposure: {value.value}")
lib.endpoint_camera_get_control(camera, FOCUS, *refs)
if not supported.value:
    check(lib.endpoint_camera_set_control(camera, FOCUS, 0) == 5, f"Focus (this camera has none): {error()}")

if hub:
    with open(os.path.join(hub, "authorized"), "w") as f:
        f.write("0")
    check(unplugged.wait(5), "unplugged (its hub switched off): the callback came")
    result, jpeg, took = capture(camera, 2000)
    check(result == 4, f"a picture after unplugging: {error()}")
    with open(os.path.join(hub, "authorized"), "w") as f:
        f.write("1")
check(lib.endpoint_camera_close(camera) == 0, "closed")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
