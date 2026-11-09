"""Chip-neutral CAN controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.CanController: dispatches to the right chip
helper based on the board detected at startup.

Usage:

    from GHIElectronics.Endpoint.Core import CanController, EPN256
    CanController.Initialize(EPN256.Can.Can0, 500_000)
"""

from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip
from GHIElectronics.Endpoint.Core._epm815 import EPM815
from GHIElectronics.Endpoint.Core._epn128 import EPN128
from GHIElectronics.Endpoint.Core._epn256 import EPN256


class CanController:
    """Chip-neutral entry point for CAN-bus setup. Call once before using
    the CAN device package: it muxes the TX/RX pins and brings the canN
    network interface up at the chosen bitrate."""

    @staticmethod
    def Initialize(port, bitrate=500_000):
        """Muxes the CAN pins and configures the bitrate."""
        if Current == Chip.Epn128:
            EPN128.Can.Initialize(port, bitrate)
        elif Current == Chip.Epn256:
            EPN256.Can.Initialize(port, bitrate)
        else:
            EPM815.Can.Initialize(port, bitrate)

    @staticmethod
    def UnInitialize(port):
        """Takes the interface down and releases its pins."""
        if Current == Chip.Epn128:
            EPN128.Can.UnInitialize(port)
        elif Current == Chip.Epn256:
            EPN256.Can.UnInitialize(port)
        else:
            EPM815.Can.UnInitialize(port)
