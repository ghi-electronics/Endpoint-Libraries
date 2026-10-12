"""GHI Electronics Endpoint: the EPM815's pins, buses and ports, and the
errors the Endpoint packages raise. The same as .NET's
GHIElectronics.Endpoint.Core.

    from GHIElectronics.Endpoint.Core import EPM815
"""

from ._epm815 import EPM815
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

__all__ = [
    "EPM815",
    "ArgumentException",
    "ArgumentOutOfRangeException",
    "InvalidOperationException",
    "IOException",
    "NotImplementedException",
    "NotSupportedException",
    "ObjectDisposedException",
    "TimeoutException",
]
