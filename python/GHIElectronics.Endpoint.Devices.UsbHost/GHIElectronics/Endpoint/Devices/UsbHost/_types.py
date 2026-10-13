"""The types of TinyCLR's USB host (GHIElectronics.TinyCLR.Devices.Usb): the
same names and values (Key is the USB key code). Python's None is a keyword:
Buttons.None_, Key.None_, HatSwitchDirection.None_."""

from enum import IntEnum, IntFlag


class DeviceType(IntEnum):
    """What a device gives, and its DeviceName"""

    Unknown = 0
    """No driver on the board: the program talks to it with pyusb ("/dev/bus/usb/001/005"):
    usb.core.find(idVendor=e.VendorId, idProduct=e.ProductId)"""
    Hub = 1
    """"/dev/bus/usb/001/002" """
    HID = 2
    """A HID device that is no keyboard, mouse or joystick ("/dev/hidraw0"): use the hid
    package (hidapi), hid.Device(vid=e.VendorId, pid=e.ProductId)"""
    Mouse = 3
    """"/dev/input/event2": open it with Mouse(e.DeviceName)"""
    Keyboard = 4
    """"/dev/input/event1": open it with Keyboard(e.DeviceName)"""
    Joystick = 5
    """Joysticks and gamepads ("/dev/input/event3"): open it with Joystick(e.DeviceName)"""
    MassStorage = 6
    """A drive: use StorageController (no name)"""
    Printer = 7
    """"/dev/usb/lp0": open it with Printer(e.DeviceName)"""
    Serial = 8
    """A serial port, for pyserial ("/dev/ttyUSB0", "/dev/ttyACM0")"""
    Webcam = 9
    """"/dev/video0" """
    Audio = 10
    """A sound card: speaker, microphone ("hw:1")"""
    Network = 11
    """An Ethernet, WiFi or cellular adapter: the network interface ("eth1", "wlan1")"""
    Bluetooth = 12
    """"hci0" """


class Buttons(IntFlag):
    """The mouse buttons"""

    None_ = 0x00
    Left = 0x01
    Right = 0x02
    Middle = 0x04
    Extended1 = 0x08
    Extended2 = 0x10


class ButtonState(IntEnum):
    Released = 0
    Pressed = 1


class Cursor(IntEnum):
    """A joystick's sticks: Cursor1 (left), Cursor2 (right)"""

    Cursor1 = 0
    Cursor2 = 1


class KeyState(IntEnum):
    Released = 2
    Pressed = 1


class HatSwitchDirection(IntEnum):
    Up = 0
    UpRight = 1
    Right = 2
    DownRight = 3
    Down = 4
    DownLeft = 5
    Left = 6
    UpLeft = 7
    None_ = 8


class Key(IntEnum):
    """The keys on the keyboard: USB key codes. D0 to D9 are the number keys. A key
    TinyCLR has no name for (F13, media keys) comes as its USB key code (an int)."""

    None_ = 0
    A = 4
    B = 5
    C = 6
    D = 7
    E = 8
    F = 9
    G = 10
    H = 11
    I = 12
    J = 13
    K = 14
    L = 15
    M = 16
    N = 17
    O = 18
    P = 19
    Q = 20
    R = 21
    S = 22
    T = 23
    U = 24
    V = 25
    W = 26
    X = 27
    Y = 28
    Z = 29
    D1 = 30
    D2 = 31
    D3 = 32
    D4 = 33
    D5 = 34
    D6 = 35
    D7 = 36
    D8 = 37
    D9 = 38
    D0 = 39
    Enter = 40
    Escape = 41
    BackSpace = 42
    Tab = 43
    Space = 44
    Substract = 45
    Equal = 46
    OpenBrackets = 47
    CloseBrackets = 48
    Backslash = 49
    NonUS = 50
    Semicolon = 51
    Quotes = 52
    GraveAccent = 53
    Comma = 54
    Period = 55
    Divide = 56
    CapsLock = 57
    F1 = 58
    F2 = 59
    F3 = 60
    F4 = 61
    F5 = 62
    F6 = 63
    F7 = 64
    F8 = 65
    F9 = 66
    F10 = 67
    F11 = 68
    F12 = 69
    PrintScreen = 70
    ScrollLock = 71
    Pause = 72
    Insert = 73
    Home = 74
    PageUp = 75
    Delete = 76
    End = 77
    PageDown = 78
    RightArrow = 79
    LeftArrow = 80
    DownArrow = 81
    UpArrow = 82
    NumLock = 83
    KeypadDivide = 84
    KeypadMultiply = 85
    KeypadSubstract = 86
    KeypadAdd = 87
    KeypadEnter = 88
    Keypad1 = 89
    Keypad2 = 90
    Keypad3 = 91
    Keypad4 = 92
    Keypad5 = 93
    Keypad6 = 94
    Keypad7 = 95
    Keypad8 = 96
    Keypad9 = 97
    Keypad0 = 98
    KeypadDelete = 99
    NonUS2 = 100
    Application = 101
    LeftCtrl = 0xE0
    LeftShift = 0xE1
    LeftAlt = 0xE2
    LeftGUI = 0xE3
    RightCtrl = 0xE4
    RightShift = 0xE5
    RightAlt = 0xE6
    RightGUI = 0xE7


class Position:
    """A position: X, Y (and Z)"""

    def __init__(self, X=0, Y=0, Z=0):
        self.X = X
        self.Y = Y
        self.Z = Z

    def __sub__(self, other):
        return Position(self.X - other.X, self.Y - other.Y, self.Z - other.Z)

    def __eq__(self, other):
        return isinstance(other, Position) and (self.X, self.Y, self.Z) == (other.X, other.Y, other.Z)

    def __hash__(self):
        return hash((self.X, self.Y, self.Z))

    def __str__(self):
        return f"({self.X}, {self.Y}, {self.Z})"

    def __repr__(self):
        return f"Position({self.X}, {self.Y}, {self.Z})"
