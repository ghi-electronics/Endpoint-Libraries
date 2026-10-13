"""USB keyboards, mice and joysticks with TinyCLR's API, and printers. Each is
opened with the DeviceName its UsbHostController event gives
("/dev/input/event2", "/dev/usb/lp0"); its events come on the device's own
thread; it stays alive until it is disposed."""

from enum import IntFlag
import ctypes

from GHIElectronics.Endpoint.Core._event import Event
from GHIElectronics.Endpoint.Core._native import InputCallback, UnpluggedCallback, native

from ._types import ButtonState, Buttons, Cursor, DeviceType, HatSwitchDirection, Key, KeyState, Position

# The libendpoint events (see Input::Event in libendpoint)
_KEY, _MOUSE_BUTTON, _MOUSE_MOVE, _WHEEL, _JOY_BUTTON, _JOY_MOVE, _HAT, _DISCONNECTED = range(1, 9)


class _EventProperty:
    """An event: handlers are added with += and removed with -= (handler(sender, e))"""

    def __set_name__(self, owner, name):
        self.attribute = "_event_" + name

    def __get__(self, target, owner=None):
        return self if target is None else target.__dict__[self.attribute]

    def __set__(self, target, value):
        if value is not target.__dict__.get(self.attribute):
            raise TypeError("Use += and -= to add and remove an event handler")


class _EventArgs:
    def __init__(self, **values):
        self.__dict__.update(values)

    def __repr__(self):
        values = ", ".join(f"{name}={value!r}" for name, value in self.__dict__.items())
        return f"{type(self).__name__}({values})"


def _key(value):
    try:
        return Key(value)
    except ValueError:
        return value  # a key TinyCLR has no name for: its USB key code


class BaseDevice:
    """A USB device the program uses: Keyboard, Mouse, Joystick or Printer"""

    Disconnected = _EventProperty()
    """The device was unplugged: handler(sender, e)"""

    def __init__(self, deviceName, deviceType):
        # The events exist before the device can raise them
        for klass in type(self).__mro__:
            for value in vars(klass).values():
                if isinstance(value, _EventProperty):
                    self.__dict__[value.attribute] = Event()
        self._handle = None
        self._printer = deviceType == DeviceType.Printer
        handle = native.out_handle()
        vendor, product, connected, capabilities = (native.out_int() for _ in range(4))
        # The callbacks are kept: the library calls them
        if self._printer:
            self._callback = UnpluggedCallback(lambda context: self.Disconnected(self, None))
            native.printer_open(str(deviceName).encode(), self._callback, None, native.ref(handle))
            native.printer_info(handle, native.ref(vendor), native.ref(product), native.ref(connected))
        else:
            self._callback = InputCallback(self._changed)
            native.input_open(str(deviceName).encode(), int(deviceType), self._callback, None, native.ref(handle))
            native.input_info(handle, native.ref(vendor), native.ref(product), native.ref(connected),
                              native.ref(capabilities))
        self._handle = handle
        native.keep_alive(self)
        self._deviceName = str(deviceName)
        self._type = deviceType
        self._vendorId = vendor.value
        self._productId = product.value
        self._capabilities = capabilities.value

    @property
    def DeviceName(self):
        """What the device was opened with: "/dev/input/event2", "/dev/usb/lp0" """
        return self._deviceName

    @property
    def Type(self):
        return self._type

    @property
    def VendorId(self):
        return self._vendorId

    @property
    def ProductId(self):
        return self._productId

    @property
    def Connected(self):
        if self._handle is None:
            return False
        values = [native.out_int() for _ in range(4)]
        if self._printer:
            native.printer_info(self._handle, *[native.ref(v) for v in values[:3]])
        else:
            native.input_info(self._handle, *[native.ref(v) for v in values])
        return values[2].value != 0

    def Dispose(self):
        """Stops the device's events (one being delivered finishes first)"""
        if self._handle is not None:
            (native.printer_close if self._printer else native.input_close)(self._handle)
            self._handle = None
            native.let_go(self)

    def _h(self):
        if self._handle is None:
            raise native.disposed(type(self).__name__)
        return self._handle

    def _changed(self, context, event, a, b, c, d, e):
        if event == _DISCONNECTED:
            self.Disconnected(self, None)
        else:
            self._on_event(event, a, b, c, d, e)

    def _on_event(self, event, a, b, c, d, e):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __repr__(self):
        return f"{type(self).__name__}({self._deviceName!r})"


class Mouse(BaseDevice):
    """A USB mouse. The cursor stays between 0 and 512 at first, as on TinyCLR:
    set it to the screen with SetCursorBounds.

        mouse = Mouse(e.DeviceName)     # from UsbHostController, DeviceType.Mouse
        mouse.SetCursorBounds(Position(0, 0), Position(799, 479))
        mouse.CursorMoved += lambda sender, a: print("Cursor at", a.NewPosition.X, a.NewPosition.Y)
        mouse.ButtonChanged += lambda sender, a: print(a.Which.name, a.State.name)
    """

    ButtonChanged = _EventProperty()
    """A button was pressed or released: handler(sender, e) with e.Which (Buttons), e.State (ButtonState)"""
    WheelMoved = _EventProperty()
    """The wheel moved: e.NewPosition, e.Delta"""
    CursorMoved = _EventProperty()
    """The cursor moved: e.NewPosition, e.Delta (Position)"""

    class ButtonChangedEventArgs(_EventArgs):
        pass

    class WheelMovedEventArgs(_EventArgs):
        pass

    class CursorMovedEventArgs(_EventArgs):
        pass

    def __init__(self, deviceName):
        super().__init__(deviceName, DeviceType.Mouse)

    def _state(self):
        values = [native.out_int() for _ in range(4)]
        native.input_mouse_state(self._h(), *[native.ref(v) for v in values])
        return [v.value for v in values]

    def _button(self, button):
        return ButtonState.Pressed if self._state()[3] & button else ButtonState.Released

    @property
    def LeftButtonState(self):
        return self._button(Buttons.Left)

    @property
    def RightButtonState(self):
        return self._button(Buttons.Right)

    @property
    def MiddleButtonState(self):
        return self._button(Buttons.Middle)

    @property
    def Extended1ButtonState(self):
        return self._button(Buttons.Extended1)

    @property
    def Extended2ButtonState(self):
        return self._button(Buttons.Extended2)

    @property
    def CursorPosition(self):
        x, y, _, _ = self._state()
        return Position(x, y)

    @property
    def WheelPosition(self):
        return self._state()[2]

    @property
    def AbsolutePosition(self):
        """False: a USB mouse moves relative"""
        return False

    def SetScale(self, scale):
        """Scales the x, y, and wheel delta values"""
        native.input_set_scale(self._h(), float(scale))

    def SetCursorBounds(self, minimum, maximum):
        """The cursor's smallest and largest Position"""
        native.input_set_cursor_bounds(self._h(), 0, minimum.X, maximum.X, minimum.Y, maximum.Y)

    def SetCursorPosition(self, newPosition):
        native.input_set_cursor_position(self._h(), newPosition.X, newPosition.Y)

    def _on_event(self, event, a, b, c, d, e):
        if event == _MOUSE_BUTTON:
            self.ButtonChanged(self, Mouse.ButtonChangedEventArgs(Which=Buttons(a), State=ButtonState(b)))
        elif event == _MOUSE_MOVE:
            self.CursorMoved(self, Mouse.CursorMovedEventArgs(NewPosition=Position(a, b), Delta=Position(c, d)))
        elif event == _WHEEL:
            self.WheelMoved(self, Mouse.WheelMovedEventArgs(NewPosition=a, Delta=b))


class Keyboard(BaseDevice):
    """A USB keyboard. CharDown and CharUp come only for keys with an ASCII
    character (a US keyboard; Ctrl+A to Ctrl+Z give 1 to 26). A key held down
    gives one KeyDown.

        keyboard = Keyboard(e.DeviceName)   # from UsbHostController, DeviceType.Keyboard
        keyboard.CharDown += lambda sender, a: print(a.ASCII, end="", flush=True)
    """

    KeyUp = _EventProperty()
    """A key was released: handler(sender, e) with e.Which (Key) and e.ASCII (a str, "\\0" if none)"""
    KeyDown = _EventProperty()
    """A key was pressed"""
    CharUp = _EventProperty()
    """A key with an ASCII character was released"""
    CharDown = _EventProperty()
    """A key with an ASCII character was pressed"""

    class KeyboardEventArgs(_EventArgs):
        pass

    def __init__(self, deviceName):
        super().__init__(deviceName, DeviceType.Keyboard)

    def IsKeyPressed(self, key):
        """The KeyState of a key"""
        state = native.out_int()
        native.input_key_state(self._h(), int(key), native.ref(state))
        return KeyState(state.value)

    def _on_event(self, event, a, b, c, d, e):
        if event != _KEY:
            return
        args = Keyboard.KeyboardEventArgs(Which=_key(a), ASCII=chr(c))
        if b == KeyState.Pressed:
            self.KeyDown(self, args)
            if c:
                self.CharDown(self, args)
        else:
            self.KeyUp(self, args)
            if c:
                self.CharUp(self, args)


class Joystick(BaseDevice):
    """A USB joystick or game pad. The sticks go from -512 to 512 at first, as on
    TinyCLR: Cursor1 is the left stick, Cursor2 the right one. Buttons are
    numbered from 0.

        joystick = Joystick(e.DeviceName)   # from UsbHostController, DeviceType.Joystick
        joystick.CursorMoved += lambda sender, a: print(a.Which.name, a.NewPosition)
        joystick.ButtonChanged += lambda sender, a: print("Button", a.Which, a.State.name)
    """

    ButtonChanged = _EventProperty()
    """A button was pressed or released: handler(sender, e) with e.Which (its number), e.State"""
    CursorMoved = _EventProperty()
    """A stick moved: e.Which (Cursor), e.NewPosition, e.Delta"""
    HatSwitchPressed = _EventProperty()
    """The hat switch moved: e.Direction (HatSwitchDirection)"""

    class DeviceCapabilities(IntFlag):
        CursorX = 0x01
        CursorY = 0x02
        Cursor2X = 0x04
        Cursor2Y = 0x08
        HatSwitch = 0x10
        Buttons = 0x20

    class ButtonChangedEventArgs(_EventArgs):
        pass

    class CursorMovedEventArgs(_EventArgs):
        pass

    class HatSwitchPressedEventArgs(_EventArgs):
        pass

    def __init__(self, deviceName):
        super().__init__(deviceName, DeviceType.Joystick)

    @property
    def Capabilities(self):
        return Joystick.DeviceCapabilities(self._capabilities)

    def _state(self, cursor):
        x, y, hat = native.out_int(), native.out_int(), native.out_int()
        buttons = native.out_uint32()
        native.input_joystick_state(self._h(), int(cursor), native.ref(x), native.ref(y), native.ref(hat),
                                    native.ref(buttons))
        return Position(x.value, y.value), HatSwitchDirection(hat.value), buttons.value

    @property
    def CursorPosition(self):
        return self._state(Cursor.Cursor1)[0]

    @property
    def Cursor2Position(self):
        return self._state(Cursor.Cursor2)[0]

    @property
    def CurrentHatSwitchDirection(self):
        return self._state(Cursor.Cursor1)[1]

    def SetCursorBounds(self, cursor, minX, maxX, minY, maxY):
        native.input_set_cursor_bounds(self._h(), int(cursor), minX, maxX, minY, maxY)

    def GetButtonState(self, buttonNumber):
        """The ButtonState of a button (0 to 31)"""
        if not 0 <= buttonNumber <= 31:
            from GHIElectronics.Endpoint.Core import ArgumentOutOfRangeException
            raise ArgumentOutOfRangeException("The button number must be 0 to 31")
        return ButtonState.Pressed if self._state(Cursor.Cursor1)[2] & (1 << buttonNumber) else ButtonState.Released

    def _on_event(self, event, a, b, c, d, e):
        if event == _JOY_BUTTON:
            self.ButtonChanged(self, Joystick.ButtonChangedEventArgs(Which=a, State=ButtonState(b)))
        elif event == _JOY_MOVE:
            self.CursorMoved(self, Joystick.CursorMovedEventArgs(Which=Cursor(a), NewPosition=Position(b, c),
                                                                 Delta=Position(d, e)))
        elif event == _HAT:
            self.HatSwitchPressed(self, Joystick.HatSwitchPressedEventArgs(Direction=HatSwitchDirection(a)))


class PrinterStatus(IntFlag):
    """What a printer reports about itself (Ready: nothing)"""

    Ready = 0
    PaperOut = 0x01
    Offline = 0x02
    Error = 0x04


class Printer(BaseDevice):
    """A USB printer: the bytes it understands (ESC/POS for receipt printers, ZPL or
    EPL for label printers, PCL or PostScript for office printers) go to it as
    they are.

        printer = Printer(e.DeviceName)     # from UsbHostController, DeviceType.Printer
        printer.Write("Hello\n\n\n")
        printer.Write(bytes([0x1D, 0x56, 0x00]))   # ESC/POS: cut the paper
    """

    def __init__(self, deviceName):
        super().__init__(deviceName, DeviceType.Printer)
        self.WriteTimeout = -1
        """How long Write waits for the printer to take the data, in milliseconds (-1: as long as it takes)"""

    @property
    def DeviceId(self):
        """The printer's IEEE 1284 Device ID: "MFG:EPSON;CMD:ESC/POS;MDL:TM-T20II;CLS:PRINTER;" """
        text = ctypes.create_string_buffer(1024)
        native.printer_device_id(self._h(), text, len(text))
        return text.value.decode(errors="replace")

    @property
    def Status(self):
        """PaperOut, Offline and Error, as the printer reports them (Ready: none)"""
        status = native.out_int()
        native.printer_status(self._h(), native.ref(status))
        return PrinterStatus(status.value)

    def Write(self, data):
        """Sends bytes (or text, as UTF-8) to the printer; all of them, or a TimeoutException
        after WriteTimeout"""
        if isinstance(data, str):
            data = data.encode()
        pointer, length, keep = native.readable(data)
        written = native.out_int()
        native.printer_write(self._h(), pointer, length, int(self.WriteTimeout), native.ref(written))
        return written.value

    def Read(self, buffer, timeout=-1):
        """Reads what the printer answers (to a status request) into a bytearray: its length,
        0 if nothing came in timeout milliseconds (-1: as long as it takes)"""
        pointer, size = native.writable(buffer)
        length = native.out_int()
        native.printer_read(self._h(), pointer, size, int(timeout), native.ref(length))
        return length.value
