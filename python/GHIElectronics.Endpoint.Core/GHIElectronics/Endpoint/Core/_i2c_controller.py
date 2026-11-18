"""Chip-neutral I2C controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.I2cController: dispatches through
BoardSupport.Current, which the active Pins.* package registered at import.

Usage:

    import GHIElectronics.Endpoint.Pins.EPDomino24       # registers the board
    from GHIElectronics.Endpoint.Core import I2cController
    from GHIElectronics.Endpoint.Pins.EPDomino24 import EPDomino24

    I2cController.Initialize(EPDomino24.I2c.I2c1)
"""

from GHIElectronics.Endpoint.Core._board_support import BoardSupport


class I2cController:
    """Chip-neutral entry point for I2C setup. Call once before using an
    I2C device: it brings up the bus and muxes its pins. Dispatches through
    the registered board (see Pins.* packages)."""

    @staticmethod
    def Initialize(bus, frequency=400_000):
        """Connects the bus's pins and sets its speed. Only the two pins of
        this bus are claimed; every other bus's pins stay free."""
        BoardSupport.Current().I2cInitialize(bus, frequency)

    @staticmethod
    def UnInitialize(bus):
        """Releases the bus's pins back to free GPIO."""
        BoardSupport.Current().I2cUnInitialize(bus)

    @staticmethod
    def GetFrequency(bus):
        """Current bus speed in Hz."""
        return BoardSupport.Current().I2cGetFrequency(bus)
