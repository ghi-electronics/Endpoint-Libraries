"""SD cards and USB drives of Endpoint, with the same API as .NET's
GHIElectronics.Endpoint.Devices.Storage:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Storage import DeviceConnectionStatus, FileSystem, StorageController

    usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage)

    def changed(sender, e):
        if e.DeviceStatus == DeviceConnectionStatus.Connected:
            drive = FileSystem.Mount(e.Hdc)              # /media/usb0/, usb1, ...
            with open(drive.Name + "log.txt", "w") as f:
                f.write("Hello")
            FileSystem.Flush(e.Hdc)                      # before the drive is pulled out

    usb.OnConnectionChangedEvent += changed
    usb.Enable()   # the drives already plugged in are reported too

The events come on the library's storage thread. A drive that is pulled out
is unmounted by the library. The SD card is /media/sd/; USB drives get the
first free /media/usbN/, as Windows gives the first free drive letter.
"""

import ctypes
import pathlib
from enum import IntEnum

from GHIElectronics.Endpoint.Core._errors import IOException
from GHIElectronics.Endpoint.Core._event import Event
from GHIElectronics.Endpoint.Core._native import StorageCallback, native

__all__ = ["DeviceConnectionEventArgs", "DeviceConnectionStatus", "DriveInfo", "DriveType", "FileSystem",
           "StorageController"]


class DeviceConnectionStatus(IntEnum):
    Disconnected = 0
    Connected = 1
    Bad = 2
    """Plugged in, but with no file system the board reads (format it as FAT32 or exFAT)"""


class DriveType(IntEnum):
    """The same values as .NET's System.IO.DriveType"""

    Unknown = 0
    NoRootDirectory = 1
    Removable = 2
    Fixed = 3
    Network = 4
    CDRom = 5
    Ram = 6


class DeviceConnectionEventArgs:
    def __init__(self, deviceStatus, hdc, volumeLabel):
        self.DeviceStatus = deviceStatus
        self.Hdc = hdc
        """The drive, for FileSystem.Mount, Flush and Unmount, until it is pulled out"""
        self.VolumeLabel = volumeLabel
        """The drive's name ("LOGGER"), empty if it has none"""

    def __repr__(self):
        return f"DeviceConnectionEventArgs({self.DeviceStatus.name}, Hdc={self.Hdc}, VolumeLabel={self.VolumeLabel!r})"


class DriveInfo:
    """A drive's folder and its sizes, as .NET's System.IO.DriveInfo. The drive's
    label is DeviceConnectionEventArgs.VolumeLabel."""

    def __init__(self, driveName):
        self._name = str(driveName)

    @property
    def Name(self):
        """The drive's folder, "/media/usb0/" """
        return self._name

    @property
    def RootDirectory(self):
        return pathlib.Path(self._name)

    @property
    def IsReady(self):
        return self._get(False)["ready"]

    @property
    def DriveFormat(self):
        """"vfat", "exfat", "ntfs" or "ext4" """
        return self._get(True)["format"]

    @property
    def DriveType(self):
        return DriveType(self._get(False)["type"])

    @property
    def TotalSize(self):
        return self._get(True)["total"]

    @property
    def TotalFreeSpace(self):
        return self._get(True)["free"]

    @property
    def AvailableFreeSpace(self):
        return self._get(True)["available"]

    def _get(self, must_be_ready):
        fmt = ctypes.create_string_buffer(64)
        kind, ready = native.out_int(), native.out_int()
        total, free, available = native.out_int64(), native.out_int64(), native.out_int64()
        native.driveinfo_get(self._name.encode(), fmt, len(fmt), native.ref(kind), native.ref(ready),
                             native.ref(total), native.ref(free), native.ref(available))
        if must_be_ready and not ready.value:
            raise IOException(f"The drive {self._name} is not ready")
        return {"format": fmt.value.decode(), "type": kind.value, "ready": bool(ready.value),
                "total": total.value, "free": free.value, "available": available.value}

    def __fspath__(self):
        return self._name

    def __repr__(self):
        return f"DriveInfo({self._name!r})"


class StorageController:
    @staticmethod
    def FromName(name):
        """EPM815.StorageController.SdCard or EPM815.StorageController.UsbHostMassStorage"""
        return StorageController(name)

    def __init__(self, name):
        self._name = name
        self._event = Event()
        self._handle = None
        self._callback = StorageCallback(self._changed)  # kept: the library calls it
        handle = native.out_handle()
        native.storage_create(name.encode(), self._callback, None, native.ref(handle))
        self._handle = handle

    @property
    def Name(self):
        return self._name

    @property
    def OnConnectionChangedEvent(self):
        """A drive connected or disconnected: handler(sender, e). Add with +=, remove with -="""
        return self._event

    @OnConnectionChangedEvent.setter
    def OnConnectionChangedEvent(self, value):
        if value is not self._event:
            raise TypeError("Use += and -= to add and remove an event handler")

    def _h(self):
        if self._handle is None:
            raise native.disposed("StorageController")
        return self._handle

    def Enable(self):
        """Starts reporting drives: first the ones already plugged in"""
        native.storage_enable(self._h())

    def Disable(self):
        native.storage_disable(self._h())

    def Dispose(self):
        """Stops the events (one being delivered finishes first)"""
        if self._handle is not None:
            native.storage_dispose(self._handle)
            self._handle = None

    def _changed(self, context, hdc, status, label):
        self._event(self, DeviceConnectionEventArgs(DeviceConnectionStatus(status), hdc,
                                                    label.decode() if label else ""))

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        self.Dispose()


class FileSystem:
    @staticmethod
    def Mount(hdc):
        """Mounts the drive (if it is not yet) and gives its folder, a DriveInfo"""
        name = ctypes.create_string_buffer(256)
        native.storage_mount(hdc, name, len(name))
        return DriveInfo(name.value.decode())

    @staticmethod
    def Flush(hdc):
        """Writes everything to the drive: call it before the drive is pulled out"""
        native.storage_flush(hdc)

    @staticmethod
    def Unmount(hdc):
        native.storage_unmount(hdc)
