"""Touchscreens on Endpoint, built-in (configured in display.json) or USB
(hot-plugged, via UsbHostController as DeviceType.Touch). Multi-touch first
class; single-touch devices simply report one point in each event."""

import ctypes
from enum import IntEnum

from GHIElectronics.Endpoint.Core._event import _EventProperty
from GHIElectronics.Endpoint.Core._native import TouchCallback, native


class TouchEventType(IntEnum):
    """How the Points changed between frames."""
    Pressed = 0   # first finger down (points went from 0 to one or more)
    Released = 1  # last finger up (points went from one or more to 0)
    Moved = 2     # a point moved, or a finger landed/lifted while others remain


class TouchPoint:
    """One finger on the touchscreen."""
    __slots__ = ("Id", "X", "Y")

    def __init__(self, id_, x, y):
        self.Id = id_   # tracking ID, stable per finger across moves within one gesture
        self.X = x
        self.Y = y

    def __repr__(self):
        return f"TouchPoint(Id={self.Id}, X={self.X}, Y={self.Y})"


class TouchEventArgs:
    def __init__(self, eventType, points):
        self.EventType = eventType
        self.Points = points   # list of TouchPoint; empty for Released


class Touch:
    """A touchscreen.

    Covers both the built-in display's touch (configured in display.json; use
    ``Touch.GetDefault()``) and USB touchscreens (hot-plugged, discovered via
    UsbHostController as DeviceType.Touch; construct from ``e.DeviceName``).
    Multi-touch is first class; single-touch devices simply report one point
    in each event.

        touch = Touch.GetDefault()   # None if no built-in touch
        if touch is not None:
            def changed(sender, e):
                for p in e.Points:
                    print(f"{p.Id}: ({p.X},{p.Y})")
            touch.OnChangedEvent += changed
    """

    OnChangedEvent = _EventProperty("OnChangedEvent")

    def __init__(self, deviceName):
        self.DeviceName = deviceName
        self._handle = native.out_handle()
        self._callback = TouchCallback(self._OnChanged)   # keep alive
        native.touch_open(deviceName.encode(), self._callback, None, native.ref(self._handle))
        v, p, c, w, h = (native.out_int() for _ in range(5))
        native.touch_info(self._handle, native.ref(v), native.ref(p), native.ref(c),
                          native.ref(w), native.ref(h))
        self.VendorId = v.value
        self.ProductId = p.value
        self.Connected = c.value != 0
        self.Width = w.value
        self.Height = h.value
        native.keep_alive(self)  # events reach us even without a program reference

    @classmethod
    def GetDefault(cls):
        """The built-in touchscreen configured in /config/display.json, or
        None if no built-in touch is wired up. USB touchscreens come through
        UsbHostController, not this method."""
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
        try:
            points = [TouchPoint(ids[i], xs[i], ys[i]) for i in range(pointCount)]
            handler = self.OnChangedEvent
            if handler is not None:
                handler(self, TouchEventArgs(TouchEventType(eventType), points))
        except Exception as e:
            import sys
            print(f"Unhandled exception in a Touch event: {e}", file=sys.stderr)
