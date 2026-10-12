"""The analog outputs of Endpoint, with the same API as TinyCLR (and the
.NET GHIElectronics.Endpoint.Devices.Dac):

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Dac import DacController

    dac = DacController.FromName(EPM815.Dac.Id)
    output = dac.OpenChannel(EPM815.Dac.PA5)
    output.WriteValue(0.5)    # a float is a ratio: half of 3.3 V
    output.WriteValue(4095)   # an int is a raw value: 3.3 V

Opening a channel makes its pin an analog output; closing it switches the
output off and puts the pin back as it was.
"""

from GHIElectronics.Endpoint.Core._errors import ArgumentException
from GHIElectronics.Endpoint.Core._native import native

__all__ = ["DacController", "DacChannel"]

_ID = "EPM815.Dac"


class DacController:
    def __init__(self):
        self._channels = []

    @staticmethod
    def GetDefault():
        return DacController()

    @staticmethod
    def FromName(name):
        if name != _ID:
            raise ArgumentException(f'There is no DAC named "{name}": use EPM815.Dac.Id')
        return DacController()

    ChannelCount = 2
    """The number of channels in the chip (channel numbers start at 0)"""
    ResolutionInBits = 12
    MinValue = 0
    """The value for 0 V"""
    MaxValue = 4095
    """The value for the reference voltage (3.3 V)"""

    def OpenChannel(self, channelNumber):
        """EPM815.Dac.PA4 or EPM815.Dac.PA5"""
        channel = DacChannel(self, channelNumber)
        self._channels.append(channel)
        return channel

    def Dispose(self):
        """Nothing to release: each channel is released when it is disposed"""

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()


class DacChannel:
    def __init__(self, controller, channelNumber):
        handle = native.out_handle()
        native.dac_open_channel(channelNumber, native.ref(handle))
        self._handle = handle
        self.ChannelNumber = channelNumber
        self.Controller = controller

    def _h(self):
        if self._handle is None:
            raise native.disposed("DacChannel")
        return self._handle

    @property
    def LastWrittenValue(self):
        value = native.out_int()
        native.dac_last_written_value(self._h(), native.ref(value))
        return value.value

    def WriteValue(self, value):
        """An int is a raw value (MinValue to MaxValue); a float is a part of the
        range, 0.0 (0 V) to 1.0 (3.3 V): the two WriteValue of .NET"""
        if isinstance(value, float):
            value = int(value * (DacController.MaxValue - DacController.MinValue) + DacController.MinValue)
        native.dac_write_value(self._h(), int(value))

    def Dispose(self):
        """Switches the output off and puts the pin back as it was"""
        if self._handle is not None:
            native.dac_close_channel(self._handle)
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
