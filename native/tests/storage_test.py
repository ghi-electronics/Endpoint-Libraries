# Board test of libendpoint's storage (SD cards and USB drives), through its C
# layer. Run as root on the board, next to libendpoint.so:
#
#     python3 storage_test.py [seconds]
#
# For the given time (default 60 s) it reports every drive that is plugged in
# or pulled out. A drive that comes is mounted, gets a test file (written,
# flushed, read back) and its information is shown. On the DK2 the SD slot holds
# the system: the SD card controller must report nothing.
import ctypes
import os
import sys
import threading
import time

lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
Callback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_ssize_t, ctypes.c_int, ctypes.c_char_p)
lib.endpoint_storage_mount.argtypes = [ctypes.c_ssize_t, ctypes.c_char_p, ctypes.c_int]
lib.endpoint_storage_flush.argtypes = [ctypes.c_ssize_t]
lib.endpoint_storage_unmount.argtypes = [ctypes.c_ssize_t]

STATUS = {0: "Disconnected", 1: "Connected", 2: "Bad (no file system the board reads)"}
start = time.monotonic()
failures = 0


def log(text):
    print(f"{time.monotonic() - start:6.1f} s  {text}", flush=True)


def check(code, what):
    global failures
    if code != 0:
        failures += 1
        log(f"FAIL  {what}: error {code}: {lib.endpoint_last_error().decode()}")
        return False
    return True


def drive_info(path):
    fmt = ctypes.create_string_buffer(64)
    kind, ready = ctypes.c_int(), ctypes.c_int()
    total, free, available = ctypes.c_int64(), ctypes.c_int64(), ctypes.c_int64()
    if check(lib.endpoint_driveinfo_get(path.encode(), fmt, 64, ctypes.byref(kind), ctypes.byref(ready),
                                        ctypes.byref(total), ctypes.byref(free), ctypes.byref(available)), "driveinfo"):
        return f"format={fmt.value.decode()} type={kind.value} ready={ready.value} total={total.value >> 20} MB " \
               f"available={available.value >> 20} MB"


def use(hdc):
    name = ctypes.create_string_buffer(256)
    t = time.monotonic()
    if not check(lib.endpoint_storage_mount(hdc, name, 256), "mount"):
        return
    folder = name.value.decode()
    log(f"      mounted on {folder} in {(time.monotonic() - t) * 1000:.0f} ms: {drive_info(folder)}")
    again = ctypes.create_string_buffer(256)
    check(lib.endpoint_storage_mount(hdc, again, 256), "mount again")
    log(f"      {'PASS' if again.value == name.value else 'FAIL'}  a second Mount gives the same folder")
    path = os.path.join(folder, "endpoint-test.txt")
    text = f"Written by the Endpoint storage test, handle {hdc}\n"
    with open(path, "w") as f:
        f.write(text)
    t = time.monotonic()
    check(lib.endpoint_storage_flush(hdc), "flush")
    log(f"      flushed in {(time.monotonic() - t) * 1000:.0f} ms")
    with open(path) as f:
        log(f"      {'PASS' if f.read() == text else 'FAIL'}  the file reads back")
    log(f"      files: {sorted(os.listdir(folder))[:8]}")


def changed(context, hdc, status, label):
    label = label.decode() if label else ""
    log(f"{context and ctypes.cast(context, ctypes.c_char_p).value.decode()}: handle {hdc} {STATUS[status]}, label '{label}'")
    if status == 1:
        # Not on the storage thread: a program may do what it likes in its handler,
        # but the test also shows that other threads can use the drive
        threading.Thread(target=use, args=(hdc,)).start()


callback = Callback(changed)
names = {}
controllers = {}
for name in ("EPM815.StorageController.UsbHostMassStorage", "EPM815.StorageController.SdCard"):
    names[name] = ctypes.c_char_p(name.split(".")[-1].encode())
    handle = ctypes.c_void_p()
    check(lib.endpoint_storage_create(name.encode(), callback, ctypes.cast(names[name], ctypes.c_void_p),
                                      ctypes.byref(handle)), f"create {name}")
    controllers[name] = handle

wrong = ctypes.c_void_p()
code = lib.endpoint_storage_create(b"EPM815.StorageController.Floppy", callback, None, ctypes.byref(wrong))
log(f"{'PASS' if code == 1 else 'FAIL'}  an unknown name is an ArgumentException: {lib.endpoint_last_error().decode()}")
code = lib.endpoint_storage_mount(12345, ctypes.create_string_buffer(256), 256)
log(f"{'PASS' if code == 3 else 'FAIL'}  a wrong handle is an InvalidOperationException: {lib.endpoint_last_error().decode()}")

for name, handle in controllers.items():
    check(lib.endpoint_storage_enable(handle), f"enable {name}")
log("enabled: plug in and pull out USB drives now")

time.sleep(float(sys.argv[1]) if len(sys.argv) > 1 else 60)
for handle in controllers.values():
    check(lib.endpoint_storage_dispose(handle), "dispose")
log(f"done, {failures} failure(s); mounts under /media: "
    f"{[line.split()[1] for line in open('/proc/mounts') if ' /media/' in line]}")
