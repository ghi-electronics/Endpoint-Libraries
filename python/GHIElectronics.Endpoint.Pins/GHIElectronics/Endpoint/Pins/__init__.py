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

Raspberry Pi Zero 2 W is NOT in this package — the Pi is not an Endpoint
board; use GHIElectronics.Endpoint.Drivers.RaspberryPi instead.
"""

from GHIElectronics.Endpoint.Core import BoardSupport
from GHIElectronics.Endpoint.Core._chip_detect import Chip, Current

from ._epm815 import EPM815
from ._epdomino24 import EPDomino24

# EPN256 + EPN128 will be added here in Phase 2 (same pattern).


class _EPM815BoardSupport:
    """Fills in Core.BoardSupport for the EPM815 module (and the EPDomino24
    carrier that sits on top of it — same chip underneath). Thin pass-through
    to EPM815.* static methods; mirrors .NET's EPM815BoardSupport : IBoardSupport.
    """

    Name = "EPM815"

    def I2cInitialize(self, bus, frequency):
        EPM815.I2c.Initialize(bus, frequency)

    def I2cUnInitialize(self, bus):
        EPM815.I2c.UnInitialize(bus)

    def I2cGetFrequency(self, bus):
        return EPM815.I2c.GetFrequency(bus)

    def SpiInitialize(self, bus, bufferSize):
        EPM815.Spi.Initialize(bus, bufferSize)

    def SpiUnInitialize(self, bus):
        EPM815.Spi.UnInitialize(bus)

    def PwmInitialize(self, pin):
        EPM815.Pwm.Initialize(pin)

    def PwmUnInitialize(self, pin):
        EPM815.Pwm.UnInitialize(pin)

    def PwmGetChipId(self, pin):
        return EPM815.Pwm.GetChipId(pin)

    def PwmGetChannelId(self, pin):
        return EPM815.Pwm.GetChannelId(pin)

    def CanInitialize(self, port, bitrate):
        EPM815.Can.Initialize(port, bitrate)

    def CanUnInitialize(self, port):
        EPM815.Can.UnInitialize(port)

    def SerialPortInitialize(self, portName, enableHardwareFlowControl):
        EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl)

    def SerialPortUnInitialize(self, portName):
        EPM815.SerialPort.UnInitialize(portName)


# Pick the matching board's BoardSupport from ChipDetect.Current. Runs once
# per process, at first `import GHIElectronics.Endpoint.Pins`. Mirrors .NET's
# [ModuleInitializer] in GHIElectronics.Endpoint.Pins.ModuleInit.
if Current == Chip.Epm815:
    BoardSupport.Register(_EPM815BoardSupport())
# elif Current == Chip.Epn256: BoardSupport.Register(_EPN256BoardSupport())   # Phase 2
# elif Current == Chip.Epn128: BoardSupport.Register(_EPN128BoardSupport())   # Phase 2
# else Chip.Unknown: don't register. BoardSupport.Current() throws on first
# access with a clear "no board detected" message.


__all__ = ["EPM815", "EPDomino24"]
