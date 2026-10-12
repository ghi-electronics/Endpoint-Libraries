"""SPI for Endpoint, with the same API as .NET's System.Device.Spi:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Spi import SpiDevice, SpiConnectionSettings, SpiMode

    EPM815.Spi.Initialize(EPM815.Spi.Spi4)
    settings = SpiConnectionSettings(EPM815.Spi.Spi4, 0)
    settings.ClockFrequency = 1_000_000
    device = SpiDevice.Create(settings)
    received = bytearray(4)
    device.TransferFullDuplex(bytes([1, 2, 3, 4]), received)

Chip select is not driven by the bus: use any GPIO pin.
"""

from enum import IntEnum

from GHIElectronics.Endpoint.Core._errors import ArgumentException
from GHIElectronics.Endpoint.Core._native import native

__all__ = ["SpiDevice", "SpiConnectionSettings", "SpiMode", "DataFlow"]


class SpiMode(IntEnum):
    Mode0 = 0
    Mode1 = 1
    Mode2 = 2
    Mode3 = 3


class DataFlow(IntEnum):
    MsbFirst = 0
    LsbFirst = 1


class SpiConnectionSettings:
    def __init__(self, busId, chipSelectLine=-1):
        self.BusId = busId
        self.ChipSelectLine = chipSelectLine
        self.Mode = SpiMode.Mode0
        self.DataBitLength = 8
        self.ClockFrequency = 500_000
        self.DataFlow = DataFlow.MsbFirst
        self.ChipSelectLineActiveState = 0  # PinValue.Low

    def __repr__(self):
        return (f"SpiConnectionSettings({self.BusId}, {self.ChipSelectLine}, Mode={self.Mode!r}, "
                f"ClockFrequency={self.ClockFrequency})")


class SpiDevice:
    """A device on an SPI bus (call EPM815.Spi.Initialize for the bus first)"""

    def __init__(self, settings):
        handle = native.out_handle()
        native.spidevice_create(settings.BusId, settings.ChipSelectLine, int(settings.Mode), settings.DataBitLength,
                                settings.ClockFrequency, int(settings.DataFlow),
                                int(settings.ChipSelectLineActiveState), native.ref(handle))
        self._handle = handle
        self.ConnectionSettings = settings

    @staticmethod
    def Create(settings):
        return SpiDevice(settings)

    def _h(self):
        if self._handle is None:
            raise native.disposed("SpiDevice")
        return self._handle

    def Read(self, buffer):
        """Reads len(buffer) bytes into the bytearray buffer (sends zeros)"""
        pointer, length = native.writable(buffer)
        native.spidevice_transfer(self._h(), None, pointer, length)

    def ReadByte(self):
        buffer = bytearray(1)
        self.Read(buffer)
        return buffer[0]

    def Write(self, buffer):
        pointer, length, _keep = native.readable(buffer)
        native.spidevice_transfer(self._h(), pointer, None, length)

    def WriteByte(self, value):
        self.Write(bytes([value]))

    def TransferFullDuplex(self, writeBuffer, readBuffer):
        """Sends writeBuffer while reading into readBuffer (the same length)"""
        write, write_length, _keep = native.readable(writeBuffer)
        read, read_length = native.writable(readBuffer)
        if write_length != read_length:
            raise ArgumentException("The write and read buffers must have the same length")
        native.spidevice_transfer(self._h(), write, read, read_length)

    def Dispose(self):
        if self._handle is not None:
            native.spidevice_dispose(self._handle)
            self._handle = None

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        try:
            self.Dispose()
        except Exception:
            pass
