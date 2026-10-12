"""The real-time clock of Endpoint, with the same API as .NET IoT's RtcBase
(and the .NET GHIElectronics.Endpoint.Devices.Rtc), plus TinyCLR's IsValid
and SetChargeMode:

    from datetime import datetime, timezone
    from GHIElectronics.Endpoint.Devices.Rtc import RtcController

    rtc = RtcController()
    if not rtc.IsValid:
        rtc.DateTime = datetime.now(timezone.utc)   # set it once; a backup battery keeps it
    print(rtc.DateTime)

It counts UTC: at boot, the system time is set from it.
"""

import calendar
import datetime
from enum import IntEnum

from GHIElectronics.Endpoint.Core._errors import NotImplementedException
from GHIElectronics.Endpoint.Core._native import native

__all__ = ["RtcController", "BatteryChargeMode"]


class BatteryChargeMode(IntEnum):
    """How the backup battery or supercapacitor (VBAT) is charged.
    Python cannot name a member None: it is None_ here."""
    None_ = 0
    Fast = 1   # through 1.5 kOhm
    Slow = 2   # through 5 kOhm


def _read():
    seconds = native.out_int64()
    native.rtc_read_time(native.ref(seconds))
    return seconds.value


class RtcController:
    """The chip's RTC"""

    @property
    def DateTime(self):
        """The time as an aware datetime (.NET's DateTimeOffset), in UTC"""
        return datetime.datetime.fromtimestamp(_read(), tz=datetime.timezone.utc)

    @DateTime.setter
    def DateTime(self, value):
        if value.tzinfo is None:
            value = value.replace(tzinfo=datetime.timezone.utc)
        native.rtc_set_time(int(value.timestamp()))

    @property
    def RtcDateTime(self):
        """The RTC's own time (UTC) as a naive datetime"""
        return datetime.datetime.fromtimestamp(_read(), tz=datetime.timezone.utc).replace(tzinfo=None)

    @RtcDateTime.setter
    def RtcDateTime(self, value):
        if value.tzinfo is not None:
            value = value.astimezone(datetime.timezone.utc).replace(tzinfo=None)
        native.rtc_set_time(calendar.timegm(value.timetuple()))

    @property
    def LocalTimeZone(self):
        """The RTC counts UTC, as the system expects"""
        return datetime.timezone.utc

    @property
    def IsValid(self):
        """Whether the RTC has been set since it last lost power (without power
        it starts again at 2000-01-01)"""
        valid = native.out_int()
        native.rtc_is_valid(native.ref(valid))
        return bool(valid.value)

    def SetChargeMode(self, chargeMode):
        """Charges the backup battery or supercapacitor on VBAT, or stops charging"""
        native.rtc_set_charge_mode(int(chargeMode))

    # TODO: backup memory and Calibrate (TinyCLR API), as in .NET

    @property
    def BackupMemorySize(self):
        """Not implemented yet"""
        raise NotImplementedException("Not implemented")

    def WriteBackupMemory(self, sourceData, *offsets):
        """Not implemented yet"""
        raise NotImplementedException("Not implemented")

    def ReadBackupMemory(self, destinationData, *offsets):
        """Not implemented yet"""
        raise NotImplementedException("Not implemented")

    def Calibrate(self, pulse):
        """Not implemented yet"""
        raise NotImplementedException("Not implemented")

    def Dispose(self):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()
