"""Chip-neutral SPI controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.SpiController: dispatches to the right chip
helper based on the board detected at startup.

Usage:

    from GHIElectronics.Endpoint.Core import SpiController, EPN256
    SpiController.Initialize(EPN256.Spi.Spi0)
"""

from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip
from GHIElectronics.Endpoint.Core._epm815 import EPM815
from GHIElectronics.Endpoint.Core._epn256 import EPN256
from GHIElectronics.Endpoint.Core._errors import NotSupportedException


class SpiController:
    """Chip-neutral entry point for SPI setup. Call once before using an
    SPI device: it brings up the bus and muxes its pins."""

    @staticmethod
    def Initialize(bus, bufferSize=2048):
        """Connects the bus's SCK/MISO/MOSI pins. On EPM815, also sizes the
        kernel transfer buffer; on EPN256 bufferSize is accepted for signature
        parity and otherwise ignored (that chip uses a fixed DMA buffer)."""
        if Current == Chip.Epn128:
            raise NotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out SPI pins.")
        if Current == Chip.Epn256:
            EPN256.Spi.Initialize(bus)
        else:
            EPM815.Spi.Initialize(bus, bufferSize)

    @staticmethod
    def UnInitialize(bus):
        """Releases the bus's pins back to free GPIO."""
        if Current == Chip.Epn128:
            return
        if Current == Chip.Epn256:
            EPN256.Spi.UnInitialize(bus)
        else:
            EPM815.Spi.UnInitialize(bus)
