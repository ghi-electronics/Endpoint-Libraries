"""Chip-neutral I2C controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.I2cController: dispatches to the right chip
helper based on the board detected at startup.

Usage:

    from GHIElectronics.Endpoint.Core import I2cController, EPN256
    I2cController.Initialize(EPN256.I2c.I2c5)
"""

from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip
from GHIElectronics.Endpoint.Core._epm815 import EPM815
from GHIElectronics.Endpoint.Core._epn128 import EPN128
from GHIElectronics.Endpoint.Core._epn256 import EPN256


class I2cController:
    """Chip-neutral entry point for I2C setup. Call once before using an
    I2C device: it brings up the bus and muxes its pins. Dispatches to the
    right chip helper based on the board detected at startup."""

    @staticmethod
    def Initialize(bus, frequency=400_000):
        """Connects the bus's pins and sets its speed. Only the two pins of
        this bus are claimed; every other bus's pins stay free."""
        if Current == Chip.Epn128:
            EPN128.I2c.Initialize(bus, frequency)
        elif Current == Chip.Epn256:
            EPN256.I2c.Initialize(bus, frequency)
        else:
            EPM815.I2c.Initialize(bus, frequency)

    @staticmethod
    def UnInitialize(bus):
        """Releases the bus's pins back to free GPIO."""
        if Current == Chip.Epn128:
            EPN128.I2c.UnInitialize(bus)
        elif Current == Chip.Epn256:
            EPN256.I2c.UnInitialize(bus)
        else:
            EPM815.I2c.UnInitialize(bus)

    @staticmethod
    def GetFrequency(bus):
        """Current bus speed in Hz."""
        if Current == Chip.Epn128:
            return EPN128.I2c.GetFrequency(bus)
        if Current == Chip.Epn256:
            return EPN256.I2c.GetFrequency(bus)
        return EPM815.I2c.GetFrequency(bus)
