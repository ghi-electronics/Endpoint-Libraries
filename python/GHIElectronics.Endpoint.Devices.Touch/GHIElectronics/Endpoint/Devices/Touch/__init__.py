"""Touchscreens on Endpoint.

Touch is abstract: subclass it for your panel's chip (I2C, SPI, custom).
Interrupt-driven drivers are first class - open a GPIO line via
GHIElectronics.Endpoint.Devices.Gpio and attach to its ValueChanged event.

The library ships one concrete subclass, InputTouch, that wraps a kernel
/dev/input/eventN. USB HID touchscreens show up this way automatically.

    from GHIElectronics.Endpoint.Devices.Touch import Touch

    class MyFt5x06(Touch):
        # ... open I2C, pulse reset, attach to interrupt GPIO ...
        @property
        def Width(self): return 800
        @property
        def Height(self): return 480
        @property
        def Connected(self): return True
        def Dispose(self): ...

    t = MyFt5x06(i2c_bus=6, address=0x38, reset_pin=\"PF2\", int_pin=\"PF12\")
    t.OnChangedEvent += lambda s, e: print(e.Points)
"""

import abc
import ctypes
import sys
from enum import IntEnum

from GHIElectronics.Endpoint.Core._event import _EventProperty
from GHIElectronics.Endpoint.Core._native import TouchCallback, native


class TouchEventType(IntEnum):
    """How the Points changed between frames."""
    Pressed = 0
    Released = 1
    Moved = 2


class TouchPoint:
    """One finger on the touchscreen."""
    __slots__ = ("Id", "X", "Y")

    def __init__(self, id_, x, y):
        self.Id = id_
        self.X = x
        self.Y = y

    def __repr__(self):
        return f"TouchPoint(Id={self.Id}, X={self.X}, Y={self.Y})"


class TouchEventArgs:
    def __init__(self, eventType, points):
        self.EventType = eventType
        self.Points = points


class Touch(abc.ABC):
    """A touchscreen on the board. Abstract: subclass for custom chips, use
    GetDefault() for a kernel /dev/input/eventN device (USB touchscreens)."""

    OnChangedEvent = _EventProperty("OnChangedEvent")

    @property
    @abc.abstractmethod
    def Width(self):
        """Visible width of the touchscreen area, in pixels."""

    @property
    @abc.abstractmethod
    def Height(self):
        """Visible height of the touchscreen area, in pixels."""

    @property
    @abc.abstractmethod
    def Connected(self):
        """True while the touchscreen is physically present and responding."""

    @abc.abstractmethod
    def Dispose(self):
        """Release the touchscreen's resources (I2C handle, GPIO, etc.)."""

    def _RaiseChanged(self, args):
        """Subclasses call this to emit a touch event. Exceptions in handlers
        are printed, never raised back out of the device thread."""
        try:
            handler = self.OnChangedEvent
            if handler is not None:
                handler(self, args)
        except Exception as e:
            print(f"Unhandled exception in a Touch event: {e}", file=sys.stderr)

    @staticmethod
    def GetDefault():
        """The first kernel-exposed touchscreen the library can find at
        /dev/input/eventN, or None if none is present. For USB HID
        touchscreens; the on-board panel's custom chip is the customer's
        own subclass and does not come through here."""
        return InputTouch._find_default()


class InputTouch(Touch):
    """Touchscreen backed by a kernel input device at /dev/input/eventN."""

    def __init__(self, deviceName):
        self.DeviceName = deviceName
        self._handle = native.out_handle()
        self._callback = TouchCallback(self._OnChanged)
        native.touch_open(deviceName.encode(), self._callback, None, native.ref(self._handle))
        v, p, c, w, h = (native.out_int() for _ in range(5))
        native.touch_info(self._handle, native.ref(v), native.ref(p), native.ref(c),
                          native.ref(w), native.ref(h))
        self.VendorId = v.value
        self.ProductId = p.value
        self._connected = c.value != 0
        self._width = w.value
        self._height = h.value
        native.keep_alive(self)

    @property
    def Width(self):
        return self._width

    @property
    def Height(self):
        return self._height

    @property
    def Connected(self):
        return self._connected

    @classmethod
    def _find_default(cls):
        buffer = ctypes.create_string_buffer(64)
        native.touch_find_default(buffer, len(buffer))
        path = buffer.value.decode("ascii")
        return cls(path) if path else None

    def Dispose(self):
        if self._handle:
            native.touch_close(self._handle)
            self._handle = None
            native.let_go(self)

    def _OnChanged(self, _context, eventType, pointCount, ids, xs, ys):
        points = [TouchPoint(ids[i], xs[i], ys[i]) for i in range(pointCount)]
        self._RaiseChanged(TouchEventArgs(TouchEventType(eventType), points))
