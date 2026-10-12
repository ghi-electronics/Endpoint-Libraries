"""The errors the Endpoint libraries raise: the same names as the .NET
exceptions, and also the matching Python ones (an ArgumentException is a
ValueError), so `except ValueError:` works too."""


class ArgumentException(ValueError):
    """An argument is not valid (.NET ArgumentException)"""


class ArgumentOutOfRangeException(ArgumentException):
    """An argument is outside its range (.NET ArgumentOutOfRangeException)"""


class InvalidOperationException(RuntimeError):
    """Not possible now, for example a pin in use (.NET InvalidOperationException)"""


class IOException(OSError):
    """The hardware or the system failed (.NET IOException)"""


class NotSupportedException(RuntimeError):
    """Not supported by the board (.NET NotSupportedException)"""


class ObjectDisposedException(InvalidOperationException):
    """Used after Dispose (.NET ObjectDisposedException)"""


class NotImplementedException(NotImplementedError):
    """Not implemented yet (.NET NotImplementedException)"""


class TimeoutException(TimeoutError):
    """The time ran out (.NET TimeoutException)"""


# libendpoint's error kinds
_KINDS = {
    1: ArgumentException,
    2: ArgumentOutOfRangeException,
    3: InvalidOperationException,
    4: IOException,
    5: NotSupportedException,
    6: ObjectDisposedException,
    7: NotImplementedException,
    8: TimeoutException,
}


def _exception(kind, message):
    return _KINDS.get(kind, IOException)(message)
