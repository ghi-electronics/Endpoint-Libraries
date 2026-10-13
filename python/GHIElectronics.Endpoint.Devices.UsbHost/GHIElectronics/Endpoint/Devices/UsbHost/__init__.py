"""USB devices of Endpoint, with the same API as .NET's
GHIElectronics.Endpoint.Devices.UsbHost:

    import serial
    from GHIElectronics.Endpoint.Core import DeviceConnectionStatus
    from GHIElectronics.Endpoint.Devices.UsbHost import DeviceType, UsbHostController

    usb = UsbHostController.GetDefault()

    def changed(sender, e):
        if e.DeviceStatus == DeviceConnectionStatus.Connected and e.Type == DeviceType.Serial:
            port = serial.Serial(e.DeviceName, 115200)   # "/dev/ttyUSB0"

    usb.OnConnectionChangedEvent += changed
    usb.Enable()   # the devices already plugged in are reported too

One event for each thing a device gives: a keyboard with a touchpad gives
Keyboard and Mouse, a webcam gives Webcam and Audio (its microphone).
DeviceName is what to open it with (see DeviceType). The events come on the
library's USB thread. USB drives come as MassStorage: use StorageController
(GHIElectronics.Endpoint.Devices.Storage) for them.
"""

from enum import IntEnum

from GHIElectronics.Endpoint.Core import DeviceConnectionStatus
from GHIElectronics.Endpoint.Core._event import Event
from GHIElectronics.Endpoint.Core._native import UsbHostCallback, native

__all__ = ["DeviceConnectionStatus", "DeviceType", "UsbDeviceConnectionEventArgs", "UsbHostController"]


class DeviceType(IntEnum):
    """What a device gives, and its DeviceName"""

    Unknown = 0
    """No driver on the board: the program talks to it ("/dev/bus/usb/001/005", for pyusb)"""
    Hub = 1
    """"/dev/bus/usb/001/002" """
    HID = 2
    """A HID device that is no keyboard, mouse or joystick ("/dev/hidraw0")"""
    Mouse = 3
    """"/dev/input/event2" """
    Keyboard = 4
    """"/dev/input/event1" """
    Joystick = 5
    """Joysticks and gamepads ("/dev/input/event3")"""
    MassStorage = 6
    """A drive: use StorageController (no name)"""
    Printer = 7
    """"/dev/usb/lp0" """
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


class UsbDeviceConnectionEventArgs:
    def __init__(self, deviceStatus, id, type, vendorId, productId, deviceName, manufacturer, product, serialNumber):
        self.DeviceStatus = deviceStatus
        self.Id = id
        """The device's number: the same for all its events, a new one when it is plugged in again"""
        self.Type = type
        self.VendorId = vendorId
        self.ProductId = productId
        self.DeviceName = deviceName
        """What to open: "/dev/ttyUSB0", "eth1"... (see DeviceType)"""
        self.Manufacturer = manufacturer
        """The device's own names ("Logitech", "USB Receiver"), empty if it has none"""
        self.Product = product
        self.SerialNumber = serialNumber

    def __repr__(self):
        return (f"UsbDeviceConnectionEventArgs({self.DeviceStatus.name}, Id={self.Id}, Type={self.Type.name}, "
                f"DeviceName={self.DeviceName!r}, VendorId=0x{self.VendorId:04X}, ProductId=0x{self.ProductId:04X}, "
                f"Manufacturer={self.Manufacturer!r}, Product={self.Product!r})")


def _text(value):
    return value.decode(errors="replace") if value else ""


def _type(value):
    try:
        return DeviceType(value)
    except ValueError:
        return value  # a type a newer libendpoint knows


class UsbHostController:
    @staticmethod
    def GetDefault():
        return UsbHostController()

    def __init__(self):
        self._event = Event()
        self._handle = None
        self._callback = UsbHostCallback(self._changed)  # kept: the library calls it
        handle = native.out_handle()
        native.usbhost_create(self._callback, None, native.ref(handle))
        self._handle = handle

    @property
    def OnConnectionChangedEvent(self):
        """A device connected or disconnected: handler(sender, e). Add with +=, remove with -="""
        return self._event

    @OnConnectionChangedEvent.setter
    def OnConnectionChangedEvent(self, value):
        if value is not self._event:
            raise TypeError("Use += and -= to add and remove an event handler")

    def _h(self):
        if self._handle is None:
            raise native.disposed("UsbHostController")
        return self._handle

    def Enable(self):
        """Starts reporting devices: first the ones already plugged in"""
        native.usbhost_enable(self._h())

    def Disable(self):
        native.usbhost_disable(self._h())

    def Dispose(self):
        """Stops the events (one being delivered finishes first)"""
        if self._handle is not None:
            native.usbhost_dispose(self._handle)
            self._handle = None

    def _changed(self, context, status, id, type, vendorId, productId, deviceName, manufacturer, product,
                 serialNumber):
        self._event(self, UsbDeviceConnectionEventArgs(DeviceConnectionStatus(status), id, _type(type), vendorId,
                                                       productId, _text(deviceName), _text(manufacturer),
                                                       _text(product), _text(serialNumber)))

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        self.Dispose()
