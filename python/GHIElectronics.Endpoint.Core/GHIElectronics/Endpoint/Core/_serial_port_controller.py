"""Chip-neutral serial-port controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.SerialPortController: dispatches to the right
chip helper based on the board detected at startup.

Usage:

    from GHIElectronics.Endpoint.Core import SerialPortController, EPN256
    SerialPortController.Initialize(EPN256.SerialPort.Uart2)
"""

from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip
from GHIElectronics.Endpoint.Core._epm815 import EPM815
from GHIElectronics.Endpoint.Core._epn128 import EPN128
from GHIElectronics.Endpoint.Core._epn256 import EPN256


class SerialPortController:
    """Chip-neutral entry point for UART / serial-port setup. Call once before
    using a serial port: it muxes the TX/RX pins (plus RTS/CTS when flow
    control is on) and makes /dev/ttyS* usable."""

    @staticmethod
    def Initialize(portName, enableHardwareFlowControl=False):
        """Muxes the port's pins. When enableHardwareFlowControl is set,
        RTS and (where bonded out) CTS are muxed too."""
        if Current == Chip.Epn128:
            EPN128.SerialPort.Initialize(portName, enableHardwareFlowControl)
        elif Current == Chip.Epn256:
            EPN256.SerialPort.Initialize(portName, enableHardwareFlowControl)
        else:
            EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl)

    @staticmethod
    def UnInitialize(portName):
        """Releases the port's pins back to free GPIO."""
        if Current == Chip.Epn128:
            EPN128.SerialPort.UnInitialize(portName)
        elif Current == Chip.Epn256:
            EPN256.SerialPort.UnInitialize(portName)
        else:
            EPM815.SerialPort.UnInitialize(portName)
