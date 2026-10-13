# Board test of libendpoint's USB host events, through its C layer. Run as
# root on the board, next to libendpoint.so:
#
#     python3 usbhost_test.py [seconds]
#
# For the given time (default 60 s) it prints every USB device thing that
# connects or disconnects (the devices already plugged in first), and checks
# that each name it gives is there: a port in /dev, a network interface, a
# sound card. A second controller, enabled later, must get the same devices.
import ctypes
import os
import sys
import threading
import time

lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
Callback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_uint32, ctypes.c_int, ctypes.c_int,
                            ctypes.c_int, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_char_p)

TYPES = ["Unknown", "Hub", "HID", "Mouse", "Keyboard", "Joystick", "MassStorage", "Printer", "Serial", "Webcam",
         "Audio", "Network", "Bluetooth"]
start = time.monotonic()
failures = 0
lock = threading.Lock()


def log(text):
    print(f"{time.monotonic() - start:6.1f} s  {text}", flush=True)


def check(code, what):
    global failures
    if code != 0:
        failures += 1
        log(f"FAIL  {what}: error {code}: {lib.endpoint_last_error().decode()}")
        return False
    return True


def name_is_there(kind, name):
    if kind == "MassStorage":
        return name == ""
    if kind == "Network":
        return os.path.exists(f"/sys/class/net/{name}")
    if kind == "Bluetooth":
        return os.path.exists(f"/sys/class/bluetooth/{name}")
    if kind == "Audio":
        return name.startswith("hw:") and os.path.exists(f"/dev/snd/controlC{name[3:]}")
    return name.startswith("/dev/") and os.path.exists(name)


def make_handler(seen, label):
    def changed(context, status, id, kind, vendor, product_id, name, manufacturer, product, serial):
        global failures
        kind = TYPES[kind] if 0 <= kind < len(TYPES) else f"type {kind}"
        name = name.decode()
        state = "connected   " if status == 1 else "disconnected"
        with lock:
            log(f"{label}{state} #{id} {kind:<11} {name:<22} {vendor:04x}:{product_id:04x} "
                f"{manufacturer.decode()!r} {product.decode()!r} serial={serial.decode()!r}")
            key = (id, kind, name)
            if status == 1:
                seen.add(key)
                if not name_is_there(kind, name):
                    failures += 1
                    log(f"      FAIL  {name!r} is not there")
            else:
                seen.discard(key)
    return Callback(changed)


seconds = float(sys.argv[1]) if len(sys.argv) > 1 else 60

first, second = set(), set()
handler1 = make_handler(first, "")
controller = ctypes.c_void_p()
check(lib.endpoint_usbhost_create(handler1, None, ctypes.byref(controller)), "create")
t = time.monotonic()
check(lib.endpoint_usbhost_enable(controller), "enable")
log(f"enabled in {(time.monotonic() - t) * 1000:.0f} ms; plug USB devices in and out")

# A second controller later gets the same devices (as they are then)
time.sleep(min(5, seconds / 2))
handler2 = make_handler(second, "[2] ")
controller2 = ctypes.c_void_p()
check(lib.endpoint_usbhost_create(handler2, None, ctypes.byref(controller2)), "create 2")
check(lib.endpoint_usbhost_enable(controller2), "enable 2")
time.sleep(1)
with lock:
    if second == first:
        log(f"PASS  the second controller got the same {len(first)} things")
    else:
        failures += 1
        log(f"FAIL  the second controller got {sorted(second)}, the first has {sorted(first)}")
check(lib.endpoint_usbhost_dispose(controller2), "dispose 2")

time.sleep(max(0, seconds - (time.monotonic() - start)))
check(lib.endpoint_usbhost_disable(controller), "disable")
check(lib.endpoint_usbhost_dispose(controller), "dispose")
log("PASS" if failures == 0 else f"{failures} FAILURES")
sys.exit(1 if failures else 0)
