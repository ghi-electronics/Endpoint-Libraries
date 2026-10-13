# Virtual keyboards, mice and joysticks for the board tests (the kernel's
# uinput: modprobe uinput). input_test.py uses the Virtual class; run as a
# program, it plays a fixed set of moves, keys and buttons that the .NET,
# Python and C++ tests of Mouse, Keyboard and Joystick check:
#
#     python3 virtual_input.py & <the test>
#
# It makes "endpoint feed mouse", "endpoint feed keyboard" and "endpoint feed
# gamepad", waits 3 s (the test opens them), plays, waits 1 s, unplugs them.
import fcntl
import glob
import os
import struct
import subprocess
import sys
import time

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



class Virtual:
    """A virtual input device (uinput)"""

    def __init__(self, name, keys=(), rel=(), abs=None, leds=()):
        self.fd = os.open("/dev/uinput", os.O_RDWR | os.O_NONBLOCK)
        if keys:
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_KEY)
            for key in keys:
                fcntl.ioctl(self.fd, UI_SET_KEYBIT, key)
        if rel:
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_REL)
            for code in rel:
                fcntl.ioctl(self.fd, UI_SET_RELBIT, code)
        absmin, absmax = [0] * 64, [0] * 64
        if abs:
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_ABS)
            for code, (low, high) in abs.items():
                fcntl.ioctl(self.fd, UI_SET_ABSBIT, code)
                absmin[code], absmax[code] = low, high
        if leds:
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_LED)
            for led in leds:
                fcntl.ioctl(self.fd, UI_SET_LEDBIT, led)
        # struct uinput_user_dev: name[80], input_id (bus, vendor, product, version), ff_effects_max, abs arrays
        device = struct.pack("80sHHHHi", name.encode(), 3, 0x1234, 0x5678, 1, 0)
        device += struct.pack("64i", *absmax) + struct.pack("64i", *absmin) + bytes(4 * 64 * 2)
        os.write(self.fd, device)
        fcntl.ioctl(self.fd, UI_DEV_CREATE)
        self.node = None
        for _ in range(100):
            for folder in glob.glob("/sys/devices/virtual/input/input*"):
                try:
                    if open(folder + "/name").read().strip() == name:
                        events = glob.glob(folder + "/event*")
                        if events:
                            self.node = "/dev/input/" + os.path.basename(events[0])
                except OSError:
                    pass
            if self.node and os.path.exists(self.node):
                break
            time.sleep(0.02)
        time.sleep(0.1)

    def send(self, *events):
        """(type, code, value)..., then a report"""
        data = b"".join(struct.pack("IIHHi", 0, 0, t, c, v) for t, c, v in events)
        os.write(self.fd, data + struct.pack("IIHHi", 0, 0, EV_SYN, 0, 0))

    def read_leds(self):
        leds = {}
        try:
            while True:
                data = os.read(self.fd, 16)
                _, _, t, c, v = struct.unpack("IIHHi", data)
                if t == EV_LED:
                    leds[c] = v
        except BlockingIOError:
            pass
        return leds

    def destroy(self):
        fcntl.ioctl(self.fd, UI_DEV_DESTROY)
        os.close(self.fd)


def feed():
    subprocess.run(["modprobe", "uinput"], check=False)
    mouse = Virtual("endpoint feed mouse", keys=(BTN_LEFT, BTN_RIGHT, BTN_MIDDLE), rel=(REL_X, REL_Y, REL_WHEEL))
    keyboard = Virtual("endpoint feed keyboard", keys=range(1, 120), leds=(0, 1, 2))
    joystick = Virtual("endpoint feed gamepad", keys=(BTN_SOUTH, BTN_EAST),
                       abs={ABS_X: (0, 255), ABS_Y: (0, 255), ABS_RX: (-32768, 32767), ABS_RY: (-32768, 32767),
                            ABS_HAT0X: (-1, 1), ABS_HAT0Y: (-1, 1)})
    print("virtual devices:", mouse.node, keyboard.node, joystick.node, flush=True)
    time.sleep(3)
    pause = 0.05
    for events in [[(EV_REL, REL_X, 10), (EV_REL, REL_Y, 5)], [(EV_KEY, BTN_LEFT, 1)], [(EV_KEY, BTN_LEFT, 0)],
                   [(EV_REL, REL_WHEEL, 1)]]:
        mouse.send(*events)
        time.sleep(pause)
    for events in [[(EV_KEY, KEY_LEFTSHIFT, 1)], [(EV_KEY, KEY_A, 1)], [(EV_KEY, KEY_A, 0)],
                   [(EV_KEY, KEY_LEFTSHIFT, 0)]]:
        keyboard.send(*events)
        time.sleep(pause)
    for events in [[(EV_ABS, ABS_X, 255)], [(EV_KEY, BTN_SOUTH, 1)], [(EV_KEY, BTN_SOUTH, 0)], [(EV_ABS, ABS_HAT0X, 1)]]:
        joystick.send(*events)
        time.sleep(pause)
    time.sleep(1)
    for device in (mouse, keyboard, joystick):
        device.destroy()
    print("virtual devices unplugged", flush=True)


# What a test of Mouse, Keyboard and Joystick must get from feed(), one line each
EXPECTED = {
    "mouse": ["CursorMoved 10,5 delta 10,5", "ButtonChanged Left Pressed", "ButtonChanged Left Released",
              "WheelMoved 1 delta 1", "Disconnected"],
    "keyboard": ["KeyDown LeftShift 0", "KeyDown A 65", "CharDown A 65", "KeyUp A 65", "CharUp A 65",
                 "KeyUp LeftShift 0", "Disconnected"],
    "gamepad": ["CursorMoved Cursor1 512,-512 delta 1024,0", "ButtonChanged 0 Pressed", "ButtonChanged 0 Released",
                "HatSwitchPressed Right", "Disconnected"],
}

if __name__ == "__main__":
    feed()
