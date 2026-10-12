"""I2C for Endpoint, with the same API as .NET's System.Device.I2c:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.I2c import I2cDevice, I2cConnectionSettings

    EPM815.I2c.Initialize(EPM815.I2c.I2c1)
    device = I2cDevice.Create(I2cConnectionSettings(EPM815.I2c.I2c1, 0x50))
    data = bytearray(2)
    device.WriteRead(bytes([0x00]), data)

Buffers to read into are bytearrays (filled in place, as .NET's Span<byte>).
"""

from GHIElectronics.Endpoint.Core._native import native

__all__ = ["I2cDevice", "I2cConnectionSettings"]


class I2cConnectionSettings:
    def __init__(self, busId, deviceAddress):
        self.BusId = busId
        self.DeviceAddress = deviceAddress

    def __repr__(self):
        return f"I2cConnectionSettings({self.BusId}, 0x{self.DeviceAddress:02X})"


class I2cDevice:
    """A device on an I2C bus (call EPM815.I2c.Initialize for the bus first)"""

    def __init__(self, settings):
        handle = native.out_handle()
        native.i2cdevice_create(settings.BusId, settings.DeviceAddress, native.ref(handle))
        self._handle = handle
        self.ConnectionSettings = settings

    @staticmethod
    def Create(settings):
        return I2cDevice(settings)

    def _h(self):
        if self._handle is None:
            raise native.disposed("I2cDevice")
        return self._handle

    def Read(self, buffer):
        """Reads len(buffer) bytes into the bytearray buffer"""
        pointer, length = native.writable(buffer)
        native.i2cdevice_read(self._h(), pointer, length)

    def ReadByte(self):
        buffer = bytearray(1)
        self.Read(buffer)
        return buffer[0]

    def Write(self, buffer):
        pointer, length, _keep = native.readable(buffer)
        native.i2cdevice_write(self._h(), pointer, length)

    def WriteByte(self, value):
        self.Write(bytes([value]))

    def WriteRead(self, writeBuffer, readBuffer):
        """Writes, then reads into readBuffer with a repeated start (no stop in between)"""
        write, write_length, _keep = native.readable(writeBuffer)
        read, read_length = native.writable(readBuffer)
        native.i2cdevice_write_read(self._h(), write, write_length, read, read_length)

    def Dispose(self):
        if self._handle is not None:
            native.i2cdevice_dispose(self._handle)
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
