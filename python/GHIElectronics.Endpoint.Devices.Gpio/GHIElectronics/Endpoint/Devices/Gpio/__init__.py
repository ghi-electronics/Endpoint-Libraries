"""GPIO for Endpoint, with the same API as .NET's System.Device.Gpio:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Gpio import GpioController, PinMode, PinValue

    gpio = GpioController()
    gpio.OpenPin(EPM815.Gpio.Pin.PC0, PinMode.Output)
    gpio.Write(EPM815.Gpio.Pin.PC0, PinValue.High)

Pins are the EPM815.Gpio.Pin constants. Open pins are reserved, so the other
Endpoint libraries cannot take them by mistake.
"""

import datetime
import threading
from enum import IntEnum, IntFlag

from GHIElectronics.Endpoint.Core._native import GpioCallback, native

__all__ = ["GpioController", "GpioPin", "PinMode", "PinValue", "PinEventTypes",
           "PinValueChangedEventArgs", "WaitForEventResult"]


class PinMode(IntEnum):
    Input = 0
    Output = 1
    InputPullDown = 2
    InputPullUp = 3


class PinValue(IntEnum):
    Low = 0
    High = 1


class PinEventTypes(IntFlag):
    """Python cannot name a member None: it is None_ here"""
    None_ = 0
    Rising = 1
    Falling = 2


class PinValueChangedEventArgs:
    def __init__(self, changeType, pinNumber):
        self.ChangeType = PinEventTypes(changeType)
        self.PinNumber = pinNumber

    def __repr__(self):
        return f"PinValueChangedEventArgs({self.ChangeType!r}, {self.PinNumber})"


class WaitForEventResult:
    def __init__(self, eventTypes, timedOut):
        self.EventTypes = PinEventTypes(eventTypes)
        self.TimedOut = timedOut

    def __repr__(self):
        return f"WaitForEventResult(EventTypes={self.EventTypes!r}, TimedOut={self.TimedOut})"


def _milliseconds(timeout):
    if timeout is None:
        return -1
    if isinstance(timeout, datetime.timedelta):
        timeout = timeout.total_seconds()
    return max(0, int(timeout * 1000))


class GpioController:
    """GPIO on all of the EPM815's ports"""

    def __init__(self):
        handle = native.out_handle()
        native.gpio_create(native.ref(handle))
        self._handle = handle
        self._lock = threading.Lock()
        self._callbacks = {}  # (pin, callback) -> (id, ctypes callback)

    def _h(self):
        if self._handle is None:
            raise native.disposed("GpioController")
        return self._handle

    @property
    def PinCount(self):
        count = native.out_int()
        native.gpio_pin_count(self._h(), native.ref(count))
        return count.value

    def OpenPin(self, pinNumber, mode=None, initialValue=None):
        """Opens the pin, optionally with a mode (and an output's first value: no glitch)"""
        native.gpio_open_pin(self._h(), pinNumber, -1 if mode is None else int(mode),
                             -1 if initialValue is None else int(initialValue))
        return GpioPin(self, pinNumber)

    def ClosePin(self, pinNumber):
        native.gpio_close_pin(self._h(), pinNumber)
        with self._lock:
            for key in [k for k in self._callbacks if k[0] == pinNumber]:
                del self._callbacks[key]

    def IsPinOpen(self, pinNumber):
        is_open = native.out_int()
        native.gpio_is_pin_open(self._h(), pinNumber, native.ref(is_open))
        return bool(is_open.value)

    def SetPinMode(self, pinNumber, mode, initialValue=None):
        native.gpio_set_pin_mode(self._h(), pinNumber, int(mode), -1 if initialValue is None else int(initialValue))

    def GetPinMode(self, pinNumber):
        mode = native.out_int()
        native.gpio_get_pin_mode(self._h(), pinNumber, native.ref(mode))
        return PinMode(mode.value)

    def IsPinModeSupported(self, pinNumber, mode):
        supported = native.out_int()
        native.gpio_is_pin_mode_supported(self._h(), pinNumber, int(mode), native.ref(supported))
        return bool(supported.value)

    def Read(self, pinNumber):
        value = native.out_int()
        native.gpio_read(self._h(), pinNumber, native.ref(value))
        return PinValue(value.value)

    def Write(self, pinNumber, value):
        native.gpio_write(self._h(), pinNumber, int(bool(value)))

    def Toggle(self, pinNumber):
        self.Write(pinNumber, not self.Read(pinNumber))

    def WaitForEvent(self, pinNumber, eventTypes, timeout):
        """Waits for a pin change; timeout is a timedelta (or seconds; None waits forever)"""
        happened, timed_out = native.out_int(), native.out_int()
        native.gpio_wait_for_event(self._h(), pinNumber, int(eventTypes), _milliseconds(timeout),
                                   native.ref(happened), native.ref(timed_out))
        return WaitForEventResult(happened.value, bool(timed_out.value))

    def RegisterCallbackForPinValueChangedEvent(self, pinNumber, eventTypes, callback):
        """callback(sender, PinValueChangedEventArgs) runs on the controller's own thread"""
        def on_change(context, pin, change):
            callback(self, PinValueChangedEventArgs(change, pin))
        c_callback = GpioCallback(on_change)
        callback_id = native.out_int()
        native.gpio_add_callback(self._h(), pinNumber, int(eventTypes), c_callback, None, native.ref(callback_id))
        with self._lock:
            old = self._callbacks.pop((pinNumber, callback), None)
            self._callbacks[(pinNumber, callback)] = (callback_id.value, c_callback)
        if old:
            native.gpio_remove_callback(self._h(), old[0])

    def UnregisterCallbackForPinValueChangedEvent(self, pinNumber, callback):
        with self._lock:
            registered = self._callbacks.pop((pinNumber, callback), None)
        if registered:
            native.gpio_remove_callback(self._h(), registered[0])

    def Dispose(self):
        """Closes every pin and stops the controller"""
        if self._handle is not None:
            native.gpio_dispose(self._handle)
            self._handle = None
            self._callbacks.clear()

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        try:
            self.Dispose()
        except Exception:
            pass


class GpioPin:
    """An open pin, as returned by GpioController.OpenPin"""

    def __init__(self, controller, pinNumber):
        self._controller = controller
        self.PinNumber = pinNumber

    def GetPinMode(self):
        return self._controller.GetPinMode(self.PinNumber)

    def SetPinMode(self, mode):
        self._controller.SetPinMode(self.PinNumber, mode)

    def IsPinModeSupported(self, mode):
        return self._controller.IsPinModeSupported(self.PinNumber, mode)

    def Read(self):
        return self._controller.Read(self.PinNumber)

    def Write(self, value):
        self._controller.Write(self.PinNumber, value)

    def Toggle(self):
        self._controller.Toggle(self.PinNumber)

    def __repr__(self):
        return f"GpioPin({self.PinNumber})"
