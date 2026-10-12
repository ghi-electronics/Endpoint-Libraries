"""The watchdog of Endpoint, with the same API as TinyCLR (and the .NET
GHIElectronics.Endpoint.Devices.Watchdog):

    from datetime import timedelta
    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Watchdog import WatchdogController

    watchdog = WatchdogController.FromName(EPM815.Watchdog.Id)
    watchdog.Enable(timedelta(seconds=10))
    while True:
        do_work()
        watchdog.Reset()

The chip's watchdog cannot be stopped once it runs: Disable hands it to the
system, which then keeps it fed. If the program ends or crashes without
Disable, the board restarts after the timeout, also when debugging stops.
"""

import datetime

from GHIElectronics.Endpoint.Core._errors import ArgumentException
from GHIElectronics.Endpoint.Core._native import native

__all__ = ["WatchdogController"]

_ID = "EPM815.Watchdog"


class WatchdogController:
    def __init__(self):
        handle = native.out_handle()
        native.watchdog_create(native.ref(handle))
        self._handle = handle

    @staticmethod
    def GetDefault():
        return WatchdogController()

    @staticmethod
    def FromName(name):
        if name != _ID:
            raise ArgumentException(f'There is no watchdog named "{name}": use EPM815.Watchdog.Id')
        return WatchdogController()

    def _h(self):
        if self._handle is None:
            raise native.disposed("WatchdogController")
        return self._handle

    @property
    def MaxTimeout(self):
        """131 seconds (4096 x 1024 ticks of the chip's 32 kHz clock)"""
        milliseconds = native.out_int()
        native.watchdog_max_timeout(native.ref(milliseconds))
        return datetime.timedelta(milliseconds=milliseconds.value)

    @property
    def IsEnabled(self):
        enabled = native.out_int()
        native.watchdog_is_enabled(self._h(), native.ref(enabled))
        return bool(enabled.value)

    def Enable(self, timeout):
        """Starts the watchdog, or changes its timeout (a timedelta, or seconds).
        The system counts whole seconds: the timeout is rounded up."""
        if isinstance(timeout, datetime.timedelta):
            timeout = timeout.total_seconds()
        native.watchdog_enable(self._h(), max(0, int(round(timeout * 1000))))

    def Reset(self):
        """Starts the timeout again: call it more often than the timeout"""
        native.watchdog_reset(self._h())

    def Disable(self):
        """Stops watching this program: the system keeps the watchdog fed from now on"""
        native.watchdog_disable(self._h())

    def Dispose(self):
        """An enabled watchdog stays enabled: call Disable first to avoid a restart"""
        if self._handle is not None:
            native.watchdog_dispose(self._handle)
            self._handle = None

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        try:
            self.Dispose()
        except Exception:
            pass
