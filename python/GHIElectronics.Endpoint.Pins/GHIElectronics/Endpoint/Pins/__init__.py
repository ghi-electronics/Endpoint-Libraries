"""GHI Electronics Endpoint board pin constants, bus tables, and board
support — one wheel for every Endpoint board (EPM815, EPDomino24, EPN256,
EPN128). On import, reads ChipDetect and registers the matching board's
BoardSupport with Core, so the chip-neutral controllers (I2cController,
SpiController, ...) dispatch correctly.

Mirrors .NET's GHIElectronics.Endpoint.Pins.

Usage:

    import GHIElectronics.Endpoint.Pins                    # registers the board
    from GHIElectronics.Endpoint.Core import I2cController
    from GHIElectronics.Endpoint.Pins import EPDomino24

    I2cController.Initialize(EPDomino24.I2c.I2c1)

Raspberry Pi Zero 2 W is NOT in this package - the Pi is not an Endpoint
board; use GHIElectronics.Endpoint.Drivers.RaspberryPi instead.
"""

from GHIElectronics.Endpoint.Core import BoardSupport
from GHIElectronics.Endpoint.Core._chip_detect import Chip, Current

from ._epm815 import EPM815
from ._epdomino24 import EPDomino24
from ._epn256 import EPN256
from ._epn128 import EPN128


class _EPM815BoardSupport:
    """Fills in Core.BoardSupport for the EPM815 module (and the EPDomino24
    carrier that sits on top of it - same chip underneath). Mirrors .NET's
    EPM815BoardSupport : IBoardSupport.
    """

    Name = "EPM815"

    def GpioChipId(self, pin): return EPM815.Gpio.GetChipId(pin)
    def GpioLineId(self, pin): return EPM815.Gpio.GetLineId(pin)
    def GpioName(self, pin): return EPM815.Gpio.Name(pin)

    def I2cInitialize(self, bus, frequency): EPM815.I2c.Initialize(bus, frequency)
    def I2cUnInitialize(self, bus): EPM815.I2c.UnInitialize(bus)
    def I2cGetFrequency(self, bus): return EPM815.I2c.GetFrequency(bus)

    def SpiInitialize(self, bus, bufferSize): EPM815.Spi.Initialize(bus, bufferSize)
    def SpiUnInitialize(self, bus): EPM815.Spi.UnInitialize(bus)

    def PwmInitialize(self, pin): EPM815.Pwm.Initialize(pin)
    def PwmUnInitialize(self, pin): EPM815.Pwm.UnInitialize(pin)
    def PwmGetChipId(self, pin): return EPM815.Pwm.GetChipId(pin)
    def PwmGetChannelId(self, pin): return EPM815.Pwm.GetChannelId(pin)

    def CanInitialize(self, port, bitrate): EPM815.Can.Initialize(port, bitrate)
    def CanUnInitialize(self, port): EPM815.Can.UnInitialize(port)

    def SerialPortInitialize(self, portName, enableHardwareFlowControl):
        EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl)
    def SerialPortUnInitialize(self, portName): EPM815.SerialPort.UnInitialize(portName)


class _EPN256BoardSupport:
    """Fills in Core.BoardSupport for the EPN256 board. Mirrors .NET's
    EPN256BoardSupport : IBoardSupport.
    """

    Name = "EPN256"

    def GpioChipId(self, pin): return EPN256.Gpio.GetChipId(pin)
    def GpioLineId(self, pin): return EPN256.Gpio.GetLineId(pin)
    def GpioName(self, pin): return EPN256.Gpio.Name(pin)

    def I2cInitialize(self, bus, frequency): EPN256.I2c.Initialize(bus, frequency)
    def I2cUnInitialize(self, bus): EPN256.I2c.UnInitialize(bus)
    def I2cGetFrequency(self, bus): return EPN256.I2c.GetFrequency(bus)

    def SpiInitialize(self, bus, bufferSize): EPN256.Spi.Initialize(bus)
    def SpiUnInitialize(self, bus): EPN256.Spi.UnInitialize(bus)

    def PwmInitialize(self, pin): EPN256.Pwm.Initialize(pin)
    def PwmUnInitialize(self, pin): EPN256.Pwm.UnInitialize(pin)
    def PwmGetChipId(self, pin): return EPN256.Pwm.GetChipId(pin)
    def PwmGetChannelId(self, pin): return EPN256.Pwm.GetChannelId(pin)

    def CanInitialize(self, port, bitrate): EPN256.Can.Initialize(port, bitrate)
    def CanUnInitialize(self, port): EPN256.Can.UnInitialize(port)

    def SerialPortInitialize(self, portName, enableHardwareFlowControl):
        EPN256.SerialPort.Initialize(portName, enableHardwareFlowControl)
    def SerialPortUnInitialize(self, portName): EPN256.SerialPort.UnInitialize(portName)


class _EPN128BoardSupport:
    """Fills in Core.BoardSupport for the EPN128 (Chili Pro) board. EPN128
    does not bond out SPI or EPWM pins, so those entry points raise.
    Mirrors .NET's EPN128BoardSupport : IBoardSupport.
    """

    Name = "EPN128"

    def GpioChipId(self, pin): return EPN128.Gpio.GetChipId(pin)
    def GpioLineId(self, pin): return EPN128.Gpio.GetLineId(pin)
    def GpioName(self, pin): return EPN128.Gpio.Name(pin)

    def I2cInitialize(self, bus, frequency): EPN128.I2c.Initialize(bus, frequency)
    def I2cUnInitialize(self, bus): EPN128.I2c.UnInitialize(bus)
    def I2cGetFrequency(self, bus): return EPN128.I2c.GetFrequency(bus)

    def SpiInitialize(self, bus, bufferSize):
        raise NotImplementedError("EPN128 (MA35D0 Chili Pro) does not bond out SPI pins.")
    def SpiUnInitialize(self, bus): pass

    def PwmInitialize(self, pin):
        raise NotImplementedError("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")
    def PwmUnInitialize(self, pin): pass
    def PwmGetChipId(self, pin):
        raise NotImplementedError("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")
    def PwmGetChannelId(self, pin):
        raise NotImplementedError("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")

    def CanInitialize(self, port, bitrate): EPN128.Can.Initialize(port, bitrate)
    def CanUnInitialize(self, port): EPN128.Can.UnInitialize(port)

    def SerialPortInitialize(self, portName, enableHardwareFlowControl):
        EPN128.SerialPort.Initialize(portName, enableHardwareFlowControl)
    def SerialPortUnInitialize(self, portName): EPN128.SerialPort.UnInitialize(portName)


# Pick the matching board's BoardSupport from ChipDetect.Current. Runs once
# per process, at first `import GHIElectronics.Endpoint.Pins`. Mirrors .NET's
# [ModuleInitializer] in GHIElectronics.Endpoint.Pins.ModuleInit.
if Current == Chip.Epm815:
    BoardSupport.Register(_EPM815BoardSupport())
elif Current == Chip.Epn256:
    BoardSupport.Register(_EPN256BoardSupport())
elif Current == Chip.Epn128:
    BoardSupport.Register(_EPN128BoardSupport())
# Chip.Unknown: don't register. BoardSupport.Current() throws on first access
# with a clear "no board detected" message.


__all__ = ["EPM815", "EPDomino24", "EPN256", "EPN128"]
