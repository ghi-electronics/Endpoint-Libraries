# Board test of the Python Mouse, Keyboard and Joystick, with the virtual
# devices of native/tests/virtual_input.py (copy it next to this file; needs
# modprobe uinput). Run as root on the board with the packages on the path.
import glob
import os
import sys
import threading

import virtual_input
from GHIElectronics.Endpoint.Core import ArgumentException, ObjectDisposedException
from GHIElectronics.Endpoint.Devices.UsbHost import (ButtonState, DeviceType, HatSwitchDirection, Joystick, Key,
                                                     Keyboard, KeyState, Mouse, Position)

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what, flush=True)
    if not ok:
        failures += 1


def find(name):
    for folder in glob.glob("/sys/class/input/event*"):
        try:
            with open(folder + "/device/name") as f:
                if f.read().strip() == name:
                    return "/dev/input/" + os.path.basename(folder)
        except OSError:
            pass
    return None


feeder = threading.Thread(target=virtual_input.feed, daemon=True)
feeder.start()
names = {}
for _ in range(100):
    names = {kind: find(f"endpoint feed {kind}") for kind in ("mouse", "keyboard", "gamepad")}
    if all(names.values()):
        break
    threading.Event().wait(0.05)
if not all(names.values()):
    sys.exit("FAIL  no virtual devices")

log = {"mouse": [], "keyboard": [], "gamepad": []}
unplugged = threading.Semaphore(0)


def add(device, line):
    log[device].append(line)
    if line == "Disconnected":
        unplugged.release()


try:
    Keyboard(names["mouse"])
    check(False, "a mouse opened as a Keyboard: no exception")
except ArgumentException as e:
    check(True, f"a mouse opened as a Keyboard: {type(e).__name__}: {e}")

mouse = Mouse(names["mouse"])
mouse.SetCursorBounds(Position(0, 0), Position(799, 479))
mouse.CursorMoved += lambda s, e: add("mouse", f"CursorMoved {e.NewPosition.X},{e.NewPosition.Y} delta {e.Delta.X},{e.Delta.Y}")
mouse.ButtonChanged += lambda s, e: add("mouse", f"ButtonChanged {e.Which.name} {e.State.name}")
mouse.WheelMoved += lambda s, e: add("mouse", f"WheelMoved {e.NewPosition} delta {e.Delta}")
mouse.Disconnected += lambda s, e: add("mouse", "Disconnected")
check(mouse.VendorId == 0x1234 and mouse.ProductId == 0x5678 and mouse.Connected and mouse.Type == DeviceType.Mouse,
      f"{mouse!r}: {mouse.VendorId:04X}:{mouse.ProductId:04X}, Connected")
try:
    mouse.CursorMoved = None
    check(False, "= instead of +=: no exception")
except TypeError as e:
    check(True, f"= instead of +=: {e}")

keyboard = Keyboard(names["keyboard"])
keyboard.KeyDown += lambda s, e: add("keyboard", f"KeyDown {e.Which.name} {ord(e.ASCII)}")
keyboard.KeyUp += lambda s, e: add("keyboard", f"KeyUp {e.Which.name} {ord(e.ASCII)}")
keyboard.CharDown += lambda s, e: add("keyboard", f"CharDown {e.Which.name} {ord(e.ASCII)}")
keyboard.CharUp += lambda s, e: add("keyboard", f"CharUp {e.Which.name} {ord(e.ASCII)}")
keyboard.Disconnected += lambda s, e: add("keyboard", "Disconnected")

# Not kept in a variable: it must still give its events (alive until disposed)
def open_joystick():
    joystick = Joystick(names["gamepad"])
    joystick.CursorMoved += lambda s, e: add("gamepad", f"CursorMoved {e.Which.name} {e.NewPosition.X},{e.NewPosition.Y} "
                                                        f"delta {e.Delta.X},{e.Delta.Y}")
    joystick.ButtonChanged += lambda s, e: add("gamepad", f"ButtonChanged {e.Which} {e.State.name}")
    joystick.HatSwitchPressed += lambda s, e: add("gamepad", f"HatSwitchPressed {e.Direction.name}")
    joystick.Disconnected += lambda s, e: add("gamepad", "Disconnected")
    check(joystick.Capabilities == Joystick.DeviceCapabilities(0x3F), f"Joystick Capabilities: {joystick.Capabilities!r}")
    return joystick


joystick_id = id(open_joystick())
import gc
gc.collect()

for _ in range(3):
    check(unplugged.acquire(timeout=15), "unplugged (Disconnected)")
check(mouse.CursorPosition == Position(10, 5) and mouse.WheelPosition == 1 and
      mouse.LeftButtonState == ButtonState.Released, f"Mouse state: {mouse.CursorPosition}, wheel {mouse.WheelPosition}")
check(keyboard.IsKeyPressed(Key.A) == KeyState.Released, "IsKeyPressed(A): Released")
check(not mouse.Connected, "Connected is false")
for device, lines in log.items():
    wanted = virtual_input.EXPECTED[device]
    check(lines == wanted, f"{device}: {' | '.join(lines)}" + ("" if lines == wanted else f"\n      wanted: {' | '.join(wanted)}"))
mouse.Dispose()
keyboard.Dispose()
try:
    mouse.CursorPosition
    check(False, "CursorPosition after Dispose: no exception")
except ObjectDisposedException:
    check(True, "CursorPosition after Dispose: ObjectDisposedException")
feeder.join()

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
