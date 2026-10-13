# Board test of libendpoint's keyboards, mice and joysticks, through its C
# layer, with virtual devices (the kernel's uinput: modprobe uinput). Run as
# root on the board, next to libendpoint.so:
#
#     python3 input_test.py
#
# It makes a virtual mouse, keyboard and joystick, sends them moves, keys and
# buttons, and checks each event the library gives: TinyCLR's values (USB key
# codes and their ASCII, the cursor kept inside its bounds, sticks scaled to
# -512..512, the hat switch), the state getters, and Disconnected when the
# virtual device goes away.
import ctypes
import fcntl
import glob
import os
import queue
import struct
import subprocess
import sys
import time

from virtual_input import Virtual

lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
lib.endpoint_last_error.restype = ctypes.c_char_p
lib.endpoint_input_set_scale.argtypes = [ctypes.c_void_p, ctypes.c_double]
for name in ("close", "info", "key_state", "mouse_state", "joystick_state", "set_cursor_bounds",
             "set_cursor_position"):
    getattr(lib, "endpoint_input_" + name).argtypes = None
Callback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                            ctypes.c_int, ctypes.c_int)

MOUSE, KEYBOARD, JOYSTICK = 3, 4, 5
KEY, MOUSE_BUTTON, MOUSE_MOVE, WHEEL, JOY_BUTTON, JOY_MOVE, HAT, DISCONNECTED = range(1, 9)
EV_SYN, EV_KEY, EV_REL, EV_ABS, EV_LED = 0, 1, 2, 3, 0x11
REL_X, REL_Y, REL_WHEEL = 0, 1, 8
ABS_X, ABS_Y, ABS_RX, ABS_RY, ABS_HAT0X, ABS_HAT0Y = 0, 1, 3, 4, 0x10, 0x11
BTN_LEFT, BTN_RIGHT, BTN_MIDDLE = 0x110, 0x111, 0x112
BTN_SOUTH, BTN_EAST, BTN_TR = 0x130, 0x131, 0x137
KEY_ENTER, KEY_LEFTSHIFT, KEY_A, KEY_C, KEY_1, KEY_LEFTCTRL, KEY_CAPSLOCK = 28, 42, 30, 46, 2, 29, 58
LED_CAPSL = 1

UI_DEV_CREATE, UI_DEV_DESTROY = 0x5501, 0x5502
UI_SET_EVBIT, UI_SET_KEYBIT, UI_SET_RELBIT, UI_SET_ABSBIT, UI_SET_LEDBIT = (0x40045564, 0x40045565, 0x40045566,
                                                                            0x40045567, 0x40045569)

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


class Opened:
    def __init__(self, node, kind):
        self.events = queue.Queue()
        self.callback = Callback(lambda context, event, a, b, c, d, e: self.events.put((event, a, b, c, d, e)))
        self.handle = ctypes.c_void_p()
        self.result = lib.endpoint_input_open(node.encode(), kind, self.callback, None, ctypes.byref(self.handle))

    def next(self, timeout=1.0):
        try:
            return self.events.get(timeout=timeout)
        except queue.Empty:
            return None

    def expect(self, wanted, what):
        got = self.next()
        check(got == wanted, f"{what}: {got}" + ("" if got == wanted else f" (wanted {wanted})"))

    def quiet(self, what):
        got = self.next(0.3)
        check(got is None, f"{what}: {got}")

    def ints(self, function, *args, count):
        values = [ctypes.c_int() for _ in range(count)]
        check(function(self.handle, *args, *[ctypes.byref(v) for v in values]) == 0, function.__name__)
        return [v.value for v in values]

    def close(self):
        check(lib.endpoint_input_close(self.handle) == 0, "close")


subprocess.run(["modprobe", "uinput"], check=False)
if not os.path.exists("/dev/uinput"):
    sys.exit("No /dev/uinput: modprobe uinput")

print("=== Mouse")
mouse = Virtual("endpoint test mouse", keys=(BTN_LEFT, BTN_RIGHT, BTN_MIDDLE), rel=(REL_X, REL_Y, REL_WHEEL))
wrong = Opened(mouse.node, KEYBOARD)
check(wrong.result == 1 and b"is not a keyboard" in lib.endpoint_last_error(),
      f"a mouse opened as a keyboard: {lib.endpoint_last_error().decode()}")
m = Opened(mouse.node, MOUSE)
check(m.result == 0, f"opened {mouse.node}")
vendor, product, connected, caps = m.ints(lib.endpoint_input_info, count=4)
check((vendor, product, connected) == (0x1234, 0x5678, 1), f"VendorId 0x{vendor:04X}, ProductId 0x{product:04X}, connected")
mouse.send((EV_REL, REL_X, 10), (EV_REL, REL_Y, 5))
m.expect((MOUSE_MOVE, 10, 5, 10, 5, 0), "moved 10, 5")
mouse.send((EV_REL, REL_X, -100))
m.expect((MOUSE_MOVE, 0, 5, -10, 0, 0), "moved -100: stops at 0")
mouse.send((EV_REL, REL_X, 1000), (EV_REL, REL_Y, 1000))
m.expect((MOUSE_MOVE, 512, 512, 512, 507, 0), "moved far: stops at TinyCLR's 512")
mouse.send((EV_KEY, BTN_LEFT, 1))
m.expect((MOUSE_BUTTON, 1, 1, 0, 0, 0), "left pressed")
mouse.send((EV_KEY, BTN_RIGHT, 1))
m.expect((MOUSE_BUTTON, 2, 1, 0, 0, 0), "right pressed")
x, y, wheel, buttons = m.ints(lib.endpoint_input_mouse_state, count=4)
check((x, y, buttons) == (512, 512, 3), f"state: {x}, {y}, buttons {buttons}")
mouse.send((EV_KEY, BTN_LEFT, 0), (EV_KEY, BTN_RIGHT, 0))
m.expect((MOUSE_BUTTON, 1, 0, 0, 0, 0), "left released")
m.expect((MOUSE_BUTTON, 2, 0, 0, 0, 0), "right released")
mouse.send((EV_REL, REL_WHEEL, 1))
m.expect((WHEEL, 1, 1, 0, 0, 0), "wheel up")
mouse.send((EV_REL, REL_WHEEL, -3))
m.expect((WHEEL, -2, -3, 0, 0, 0), "wheel down 3")
check(lib.endpoint_input_set_cursor_bounds(m.handle, 0, 0, 799, 0, 479) == 0, "SetCursorBounds 800 x 480")
check(lib.endpoint_input_set_cursor_position(m.handle, 400, 240) == 0, "SetCursorPosition 400, 240")
mouse.send((EV_REL, REL_X, 3))
m.expect((MOUSE_MOVE, 403, 240, 3, 0, 0), "moved from the new position")
lib.endpoint_input_set_scale(m.handle, 0.5)
mouse.send((EV_REL, REL_X, -3))
m.expect((MOUSE_MOVE, 402, 240, -1, 0, 0), "scale 0.5: -3 moves -1 (half kept)")
mouse.send((EV_REL, REL_X, -3))
m.expect((MOUSE_MOVE, 400, 240, -2, 0, 0), "then -3 moves -2")
mouse.send((EV_REL, REL_X, 0))
m.quiet("no move, no event")
check(lib.endpoint_input_set_cursor_bounds(m.handle, 0, 10, 0, 0, 0) == 1, "bounds with min > max: ArgumentException")
mouse.destroy()
m.expect((DISCONNECTED, 0, 0, 0, 0, 0), "unplugged: Disconnected")
check(m.ints(lib.endpoint_input_info, count=4)[2] == 0, "Connected is false")
m.close()

print("=== Keyboard")
keyboard = Virtual("endpoint test keyboard", keys=range(1, 120), leds=(0, 1, 2))
k = Opened(keyboard.node, KEYBOARD)
check(k.result == 0, f"opened {keyboard.node}")
keyboard.send((EV_KEY, KEY_A, 1))
k.expect((KEY, 0x04, 1, ord("a"), 0, 0), "A pressed: Key.A (4), Pressed (1), 'a'")
check(k.ints(lib.endpoint_input_key_state, 0x04, count=1) == [1], "IsKeyPressed(A): Pressed")
keyboard.send((EV_KEY, KEY_A, 2))
k.quiet("held (repeat): no event")
keyboard.send((EV_KEY, KEY_A, 0))
k.expect((KEY, 0x04, 2, ord("a"), 0, 0), "A released: Released (2)")
check(k.ints(lib.endpoint_input_key_state, 0x04, count=1) == [2], "IsKeyPressed(A): Released")
keyboard.send((EV_KEY, KEY_LEFTSHIFT, 1))
k.expect((KEY, 0xE1, 1, 0, 0, 0), "Left Shift pressed (no character)")
keyboard.send((EV_KEY, KEY_1, 1))
k.expect((KEY, 0x1E, 1, ord("!"), 0, 0), "Shift + 1: '!'")
keyboard.send((EV_KEY, KEY_1, 0), (EV_KEY, KEY_LEFTSHIFT, 0))
k.next(), k.next()
keyboard.read_leds()
keyboard.send((EV_KEY, KEY_CAPSLOCK, 1), (EV_KEY, KEY_CAPSLOCK, 0))
k.next(), k.next()
time.sleep(0.1)
check(keyboard.read_leds().get(LED_CAPSL) == 1, "Caps Lock turns its light on")
keyboard.send((EV_KEY, KEY_A, 1))
k.expect((KEY, 0x04, 1, ord("A"), 0, 0), "Caps Lock + A: 'A'")
keyboard.send((EV_KEY, KEY_A, 0), (EV_KEY, KEY_CAPSLOCK, 1), (EV_KEY, KEY_CAPSLOCK, 0))
k.next(), k.next(), k.next()
keyboard.send((EV_KEY, KEY_LEFTCTRL, 1), (EV_KEY, KEY_C, 1))
k.next()
k.expect((KEY, 0x06, 1, 3, 0, 0), "Ctrl + C: 3")
keyboard.send((EV_KEY, KEY_C, 0), (EV_KEY, KEY_LEFTCTRL, 0), (EV_KEY, KEY_ENTER, 1))
k.next(), k.next()
k.expect((KEY, 0x28, 1, 13, 0, 0), "Enter: Key.Enter (0x28), '\\r'")
keyboard.destroy()
k.expect((KEY, 0x28, 2, 13, 0, 0), "unplugged with Enter held: Enter released first")
k.expect((DISCONNECTED, 0, 0, 0, 0, 0), "then Disconnected")
k.close()

print("=== Joystick")
joystick = Virtual("endpoint test gamepad", keys=(BTN_SOUTH, BTN_EAST, BTN_TR),
                   abs={ABS_X: (0, 255), ABS_Y: (0, 255), ABS_RX: (-32768, 32767), ABS_RY: (-32768, 32767),
                        ABS_HAT0X: (-1, 1), ABS_HAT0Y: (-1, 1)})
j = Opened(joystick.node, JOYSTICK)
check(j.result == 0, f"opened {joystick.node}")
caps = j.ints(lib.endpoint_input_info, count=4)[3]
check(caps == 0x3F, f"Capabilities 0x{caps:02X}: both cursors, hat switch, buttons")
x, y, hat, _ = j.ints(lib.endpoint_input_joystick_state, 0, count=4)
check((x, y, hat) == (-512, -512, 8), f"at first: cursor 1 at {x}, {y}, hat None (8): the virtual stick starts at 0")
joystick.send((EV_ABS, ABS_X, 255), (EV_ABS, ABS_Y, 128))
j.expect((JOY_MOVE, 0, 512, 2, 1024, 514), "cursor 1 to the right, Y middle")
joystick.send((EV_ABS, ABS_RX, 32767))
j.expect((JOY_MOVE, 1, 512, 0, 512, 0), "cursor 2 (RX) to the right")
joystick.send((EV_KEY, BTN_SOUTH, 1))
j.expect((JOY_BUTTON, 0, 1, 0, 0, 0), "button 0 (A / south) pressed")
joystick.send((EV_KEY, BTN_TR, 1))
j.expect((JOY_BUTTON, 2, 1, 0, 0, 0), "button 2 (right shoulder) pressed")
joystick.send((EV_KEY, BTN_SOUTH, 0))
j.expect((JOY_BUTTON, 0, 0, 0, 0, 0), "button 0 released")
joystick.send((EV_ABS, ABS_HAT0X, 1), (EV_ABS, ABS_HAT0Y, -1))
j.expect((HAT, 1, 0, 0, 0, 0), "hat right + up: UpRight (1)")
joystick.send((EV_ABS, ABS_HAT0X, 0), (EV_ABS, ABS_HAT0Y, 0))
j.expect((HAT, 8, 0, 0, 0, 0), "hat back: None (8)")
x, y, hat, buttons = j.ints(lib.endpoint_input_joystick_state, 0, count=4)
check((x, y, hat, buttons & 0xFFFFFFFF) == (512, 2, 8, 4), f"state: {x}, {y}, hat {hat}, buttons {buttons}")
check(lib.endpoint_input_set_cursor_bounds(j.handle, 0, 0, 100, 0, 100) == 0, "SetCursorBounds(Cursor1, 0..100)")
joystick.send((EV_ABS, ABS_X, 0))
j.expect((JOY_MOVE, 0, 0, 50, -100, 0), "cursor 1 in the new bounds")
joystick.destroy()
j.expect((JOY_BUTTON, 2, 0, 0, 0, 0), "unplugged with button 2 held: released first")
j.expect((DISCONNECTED, 0, 0, 0, 0, 0), "then Disconnected")
j.close()

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
