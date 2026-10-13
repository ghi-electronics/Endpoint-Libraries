"""Whether a device is connected, for the events of StorageController and
UsbHostController (the same as .NET's GHIElectronics.Endpoint.Core.DeviceConnectionStatus)"""

from enum import IntEnum


class DeviceConnectionStatus(IntEnum):
    Disconnected = 0
    Connected = 1
    Bad = 2
    """A drive with no file system the board reads (format it as FAT32 or exFAT)"""
