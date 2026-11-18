"""GHI Electronics Endpoint chip-neutral runtime. The same shape as .NET's
GHIElectronics.Endpoint.Core: chip-neutral controllers (I2cController,
SpiController, ...) that dispatch through BoardSupport.Current, plus the
error types raised by the Endpoint packages.

Per-chip pin tables, bus constants, and board classes live in the Pins.*
packages. Import one of them in your program:

    import GHIElectronics.Endpoint.Pins.EPDomino24     # registers EPDomino24
    from GHIElectronics.Endpoint.Core import I2cController
    from GHIElectronics.Endpoint.Pins.EPDomino24 import EPDomino24

    I2cController.Initialize(EPDomino24.I2c.I2c1)
"""

from ._board_support import BoardSupport, Pinmux
from ._connection import DeviceConnectionStatus
from ._errors import (
    ArgumentException,
    ArgumentOutOfRangeException,
    InvalidOperationException,
    IOException,
    NotImplementedException,
    NotSupportedException,
    ObjectDisposedException,
    TimeoutException,
)
from ._i2c_controller import I2cController
from ._spi_controller import SpiController
from ._pwm_controller import PwmController
from ._serial_port_controller import SerialPortController
from ._can_controller import CanController

__all__ = [
    "BoardSupport",
    "Pinmux",
    "DeviceConnectionStatus",
    "ArgumentException",
    "ArgumentOutOfRangeException",
    "InvalidOperationException",
    "IOException",
    "NotImplementedException",
    "NotSupportedException",
    "ObjectDisposedException",
    "TimeoutException",
    "I2cController",
    "SpiController",
    "PwmController",
    "SerialPortController",
    "CanController",
]
