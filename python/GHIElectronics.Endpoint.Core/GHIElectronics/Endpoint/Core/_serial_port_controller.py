"""Chip-neutral serial-port controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.SerialPortController: dispatches through
BoardSupport.Current, which the active Pins.* package registered at import.
"""

from GHIElectronics.Endpoint.Core._board_support import BoardSupport


class SerialPortController:
    """Chip-neutral entry point for UART / serial-port setup. Call once
    before using a serial port: it muxes the TX/RX pins (plus RTS/CTS when
    flow control is on) and makes /dev/ttyS* usable."""

    @staticmethod
    def Initialize(portName, enableHardwareFlowControl=False):
        """Muxes the port's pins. When enableHardwareFlowControl is set,
        RTS and (where bonded out) CTS are muxed too."""
        BoardSupport.Current().SerialPortInitialize(portName, enableHardwareFlowControl)

    @staticmethod
    def UnInitialize(portName):
        """Releases the port's pins back to free GPIO."""
        BoardSupport.Current().SerialPortUnInitialize(portName)
