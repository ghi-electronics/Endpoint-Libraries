# Board test of libendpoint's RawDevice (for .NET), through its C layer, with
# the Realtek RTL8153 USB Ethernet adapter (0bda:8153) as "a program's own
# device". Run as root on the board, next to libendpoint.so:
#
#     python3 usbraw_test.py
#
# Control transfers (descriptors, status, the product name), an interrupt
# transfer (the interface is taken from the r8152 driver, eth1 goes away, and
# comes back when the device is closed), errors, and the unplug callback (the
# hub in front of it is switched off and on in software).
import ctypes
import glob
import os
import struct
import sys
import threading
import time

lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
Unplugged = ctypes.CFUNCTYPE(None, ctypes.c_void_p)
for name in ("close", "info", "control", "transfer"):
    getattr(lib, "endpoint_usbraw_" + name).argtypes = None

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def find_adapter():
    for folder in glob.glob("/sys/bus/usb/devices/*"):
        try:
            if open(folder + "/idVendor").read().strip() == "0bda" and open(folder + "/idProduct").read().strip() == "8153":
                bus = int(open(folder + "/busnum").read())
                dev = int(open(folder + "/devnum").read())
                return f"/dev/bus/usb/{bus:03d}/{dev:03d}", folder
        except OSError:
            pass
    return None, None


def control(device, request_type, request, value, index, length, timeout=1000):
    data = (ctypes.c_uint8 * max(length, 1))()
    done = ctypes.c_int()
    result = lib.endpoint_usbraw_control(device, request_type, request, value, index, data, length, timeout,
                                         ctypes.byref(done))
    return result, bytes(data[:done.value])


name, folder = find_adapter()
if name is None:
    sys.exit("The RTL8153 USB Ethernet adapter (0bda:8153) is not plugged in")
hub = os.path.dirname(os.path.realpath(folder))
print(f"adapter {name} ({folder}), behind {os.path.basename(hub)}")

bad = ctypes.c_void_p()
check(lib.endpoint_usbraw_open(b"/dev/null", None, None, ctypes.byref(bad)) == 1,
      f"/dev/null: {lib.endpoint_last_error().decode()}")
check(lib.endpoint_usbraw_open(b"/dev/bus/usb/001/250", None, None, ctypes.byref(bad)) == 1,
      f"no such device: {lib.endpoint_last_error().decode()}")

unplugged = threading.Event()
callback = Unplugged(lambda context: unplugged.set())
device = ctypes.c_void_p()
check(lib.endpoint_usbraw_open(name.encode(), callback, None, ctypes.byref(device)) == 0, f"opened {name}")
vendor, product, connected = ctypes.c_int(), ctypes.c_int(), ctypes.c_int()
lib.endpoint_usbraw_info(device, ctypes.byref(vendor), ctypes.byref(product), ctypes.byref(connected))
check((vendor.value, product.value, connected.value) == (0x0BDA, 0x8153, 1), f"VendorId {vendor.value:04X}, ProductId {product.value:04X}, connected")

result, descriptor = control(device, 0x80, 0x06, 0x0100, 0, 18)
check(result == 0 and len(descriptor) == 18 and struct.unpack_from("<HH", descriptor, 8) == (0x0BDA, 0x8153),
      f"GET_DESCRIPTOR device: {descriptor.hex()}")
result, configuration = control(device, 0x80, 0x06, 0x0200, 0, 9)
total = struct.unpack_from("<H", configuration, 2)[0] if result == 0 else 0
result, configuration = control(device, 0x80, 0x06, 0x0200, 0, total)
check(result == 0 and len(configuration) == total and total > 9, f"GET_DESCRIPTOR configuration: {total} bytes")
product_index = descriptor[15]
result, text = control(device, 0x80, 0x06, 0x0300 | product_index, 0x0409, 255)
check(result == 0 and text[2:].decode("utf-16-le") == "USB 10/100/1000 LAN", f"the product's name: {text[2:].decode('utf-16-le')!r}")
result, status = control(device, 0x80, 0x00, 0, 0, 2)
check(result == 0 and len(status) == 2, f"GET_STATUS: {status.hex()}")

data = (ctypes.c_uint8 * 8)()
done = ctypes.c_int()
check(lib.endpoint_usbraw_transfer(device, 0x85, data, 8, 100, ctypes.byref(done)) == 1,
      f"an endpoint it does not have: {lib.endpoint_last_error().decode()}")
check(os.path.exists("/sys/class/net/eth1"), "eth1 is there (r8152 has the interface)")
t = time.monotonic()
result = lib.endpoint_usbraw_transfer(device, 0x83, data, 8, 300, ctypes.byref(done))
waited = time.monotonic() - t
check(result == 0 or result == 8, f"interrupt IN 0x83: {'got ' + bytes(data[:done.value]).hex() if result == 0 else 'timeout after %.2f s' % waited}")
check(not os.path.exists("/sys/class/net/eth1"), "eth1 is gone (the interface was taken from r8152)")
check(lib.endpoint_usbraw_close(device) == 0, "closed")
for _ in range(50):
    if os.path.exists("/sys/class/net/eth1"):
        break
    time.sleep(0.1)
check(os.path.exists("/sys/class/net/eth1"), "eth1 is back (the interface went back to r8152)")

# Unplugged: the hub in front of it off and on
device = ctypes.c_void_p()
check(lib.endpoint_usbraw_open(name.encode(), callback, None, ctypes.byref(device)) == 0, "opened again")
with open(hub + "/authorized", "w") as f:
    f.write("0")
check(unplugged.wait(5), "unplugged: the callback came")
lib.endpoint_usbraw_info(device, ctypes.byref(vendor), ctypes.byref(product), ctypes.byref(connected))
check(connected.value == 0, "Connected is false")
result, _ = control(device, 0x80, 0x00, 0, 0, 2)
check(result == 4, f"a transfer after unplugging: {lib.endpoint_last_error().decode()}")
check(lib.endpoint_usbraw_close(device) == 0, "closed")
with open(hub + "/authorized", "w") as f:
    f.write("1")
time.sleep(3)

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
