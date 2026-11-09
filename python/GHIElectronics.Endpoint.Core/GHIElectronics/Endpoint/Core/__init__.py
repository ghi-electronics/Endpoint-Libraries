"""GHI Electronics Endpoint: the chip modules' pins, buses and ports, and
the errors the Endpoint packages raise. The same as .NET's
GHIElectronics.Endpoint.Core.

    from GHIElectronics.Endpoint.Core import EPM815     # STM32MP15x
    from GHIElectronics.Endpoint.Core import EPN256     # Nuvoton MA35D1
"""

from ._connection import DeviceConnectionStatus
from ._epdomino24 import EPDomino24
from ._epm815 import EPM815
from ._epn128 import EPN128
from ._epn256 import EPN256
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
    "DeviceConnectionStatus",
    "EPDomino24",
    "EPM815",
    "EPN128",
    "EPN256",
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
