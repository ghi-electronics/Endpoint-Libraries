"""Chip-neutral SPI controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.SpiController: dispatches through
BoardSupport.Current, which the active Pins.* package registered at import.

Usage:

    import GHIElectronics.Endpoint.Pins.EPDomino24
    from GHIElectronics.Endpoint.Core import SpiController
    from GHIElectronics.Endpoint.Pins.EPDomino24 import EPDomino24

    SpiController.Initialize(EPDomino24.Spi.Spi4)
"""

from GHIElectronics.Endpoint.Core._board_support import BoardSupport


class SpiController:
    """Chip-neutral entry point for SPI setup. Call once before using an
    SPI device: it brings up the bus and muxes its pins."""

    @staticmethod
    def Initialize(bus, bufferSize=2048):
        """Connects the bus's SCK/MISO/MOSI pins. On EPM815, also sizes the
        kernel transfer buffer; on chips with a fixed DMA buffer, bufferSize
        is accepted for signature parity and otherwise ignored."""
        BoardSupport.Current().SpiInitialize(bus, bufferSize)

    @staticmethod
    def UnInitialize(bus):
        """Releases the bus's pins back to free GPIO."""
        BoardSupport.Current().SpiUnInitialize(bus)
