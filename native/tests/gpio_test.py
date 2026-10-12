# Board test of libendpoint's GPIO through its hidden C layer (the path the
# Python package uses). Run as root: python3 gpio_test.py /path/libendpoint.so
# PF13 and PF14 must be unconnected: PF13 follows its pull-up/down, which
# also makes the pin change events.
import ctypes
import sys
import threading
import time

lib = ctypes.CDLL(sys.argv[1] if len(sys.argv) > 1 else "./libendpoint.so")
lib.endpoint_last_error.restype = ctypes.c_char_p
lib.endpoint_version.restype = ctypes.c_char_p

PF13, PF14 = 93, 94  # EPM815 pin numbers (port * 16 + line)
INPUT, OUTPUT, INPUT_PULL_DOWN, INPUT_PULL_UP = 0, 1, 2, 3
RISING, FALLING = 1, 2
ARGUMENT_OUT_OF_RANGE, INVALID_OPERATION = 2, 3

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what)
    if not ok:
        failures += 1


def call(result):
    if result != 0:
        raise RuntimeError(f"error {result}: {lib.endpoint_last_error().decode()}")


def fails(kind, function, *args, what=""):
    result = function(*args)
    message = lib.endpoint_last_error().decode()
    check(result == kind, f"{what}: error {result}: {message}")


def value(function, *args):
    out = ctypes.c_int()
    call(function(*args, ctypes.byref(out)))
    return out.value


print("libendpoint", lib.endpoint_version().decode())
gpio = ctypes.c_void_p()
call(lib.endpoint_gpio_create(ctypes.byref(gpio)))
check(value(lib.endpoint_gpio_pin_count, gpio) == 152, "PinCount 152 (PA0 to PZ7)")

print("=== Output")
call(lib.endpoint_gpio_open_pin(gpio, PF14, OUTPUT, 1))
check(value(lib.endpoint_gpio_read, gpio, PF14) == 1, "PF14 opened as an output, High")
call(lib.endpoint_gpio_write(gpio, PF14, 0))
check(value(lib.endpoint_gpio_read, gpio, PF14) == 0, "write Low, reads Low")
call(lib.endpoint_gpio_write(gpio, PF14, 1))
check(value(lib.endpoint_gpio_read, gpio, PF14) == 1, "write High, reads High")
check(value(lib.endpoint_gpio_get_pin_mode, gpio, PF14) == OUTPUT, "GetPinMode: Output")
call(lib.endpoint_gpio_set_pin_mode(gpio, PF14, OUTPUT, -1))
check(value(lib.endpoint_gpio_read, gpio, PF14) == 1, "setting Output again keeps High (no glitch)")

print("=== Input with pull-up and pull-down")
call(lib.endpoint_gpio_open_pin(gpio, PF13, INPUT_PULL_UP, -1))
time.sleep(0.01)
check(value(lib.endpoint_gpio_read, gpio, PF13) == 1, "PF13 with pull-up reads High")
call(lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_DOWN, -1))
time.sleep(0.01)
check(value(lib.endpoint_gpio_read, gpio, PF13) == 0, "PF13 with pull-down reads Low")
check(value(lib.endpoint_gpio_get_pin_mode, gpio, PF13) == INPUT_PULL_DOWN, "GetPinMode: InputPullDown")

print("=== Errors")
fails(INVALID_OPERATION, lib.endpoint_gpio_write, gpio, PF13, 1, what="write to an input")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_gpio_open_pin, gpio, 200, INPUT, -1, what="pin 200")
fails(INVALID_OPERATION, lib.endpoint_gpio_read, gpio, 0, ctypes.byref(ctypes.c_int()), what="read a pin that is not open")
other = ctypes.c_void_p()
call(lib.endpoint_gpio_create(ctypes.byref(other)))
fails(INVALID_OPERATION, lib.endpoint_gpio_open_pin, other, PF13, INPUT, -1, what="PF13 in a second controller")

print("=== Pin change events")
events = []
CALLBACK = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_int)
on_change = CALLBACK(lambda context, pin, change: events.append((pin, change)))
callback_id = value(lib.endpoint_gpio_add_callback, gpio, PF13, RISING | FALLING, on_change, None)
call(lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_UP, -1))
time.sleep(0.1)
call(lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_DOWN, -1))
time.sleep(0.1)
check(events[:2] == [(PF13, RISING), (PF13, FALLING)], f"callback got {events} (Rising, then Falling)")
call(lib.endpoint_gpio_remove_callback(gpio, callback_id))
events.clear()
call(lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_UP, -1))
time.sleep(0.1)
check(events == [], "no more calls after removing the callback")
call(lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_DOWN, -1))
time.sleep(0.05)

threading.Timer(0.2, lambda: lib.endpoint_gpio_set_pin_mode(gpio, PF13, INPUT_PULL_UP, -1)).start()
happened, timed_out = ctypes.c_int(), ctypes.c_int()
start = time.monotonic()
call(lib.endpoint_gpio_wait_for_event(gpio, PF13, RISING, 2000, ctypes.byref(happened), ctypes.byref(timed_out)))
check(happened.value == RISING and not timed_out.value,
      f"WaitForEvent(Rising) got it after {(time.monotonic() - start) * 1000:.0f} ms")
start = time.monotonic()
call(lib.endpoint_gpio_wait_for_event(gpio, PF13, FALLING, 300, ctypes.byref(happened), ctypes.byref(timed_out)))
check(timed_out.value == 1, f"WaitForEvent(Falling) times out after {(time.monotonic() - start) * 1000:.0f} ms")
fails(INVALID_OPERATION, lib.endpoint_gpio_add_callback, gpio, PF14, RISING, on_change, None,
      ctypes.byref(ctypes.c_int()), what="events on an output")

print("=== Close")
call(lib.endpoint_gpio_close_pin(gpio, PF13))
check(value(lib.endpoint_gpio_is_pin_open, gpio, PF13) == 0, "PF13 closed")
call(lib.endpoint_gpio_open_pin(other, PF13, INPUT, -1))
check(True, "the second controller can open PF13 now")
call(lib.endpoint_gpio_dispose(other))
call(lib.endpoint_gpio_dispose(gpio))
check(True, "both controllers disposed")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
