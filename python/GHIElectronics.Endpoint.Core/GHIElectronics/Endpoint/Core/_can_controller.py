"""Chip-neutral CAN controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.CanController: dispatches through
BoardSupport.Current, which the active Pins.* package registered at import.
"""

from GHIElectronics.Endpoint.Core._board_support import BoardSupport


class CanController:
    """Chip-neutral entry point for CAN-bus setup. Call once before using
    the CAN device package: it muxes the TX/RX pins and brings the canN
    network interface up at the chosen bitrate."""

    @staticmethod
    def Initialize(port, bitrate=500_000):
        """Muxes the CAN pins and configures the bitrate."""
        BoardSupport.Current().CanInitialize(port, bitrate)

    @staticmethod
    def UnInitialize(port):
        """Takes the interface down and releases its pins."""
        BoardSupport.Current().CanUnInitialize(port)
