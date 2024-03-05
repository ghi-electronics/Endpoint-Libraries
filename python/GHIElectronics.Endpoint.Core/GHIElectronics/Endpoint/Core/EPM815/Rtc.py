from enum import IntEnum
from datetime import datetime
import subprocess
import GHIElectronics.Endpoint.Core.EPM815.Register as Register
   
import struct


class BatteryChargeMode(IntEnum):    
    NONE = 0
    FAST = 1
    SLOW = 2

class RtcController(object):
    __InitializeCount = 0

    __PWR_CR3 = 0x50001000 + 0x0C

    def __init__(self): 
        self.__Acquire()

    def __Acquire(self):
        if RtcController.__InitializeCount == 0:
            self.__LoadResources()

        RtcController.__InitializeCount +=1

    def __Release(self):
        RtcController.__InitializeCount -=1

        if RtcController.__InitializeCount == 0:
            self.__UnLoadResources()

    def __LoadResources(sefl):
        return
    
    def __UnLoadResources(sefl):
        return
    
    def EnableChargeMode(self, mode: BatteryChargeMode):
        read = Register.Read(RtcController.__PWR_CR3)

        match(mode):
            case BatteryChargeMode.NONE:
                read &= ~(1 << 8)
                Register.Write(RtcController.__PWR_CR3, read)

            case BatteryChargeMode.FAST:
                read |= (3 << 8)
                Register.Write(RtcController.__PWR_CR3, read)

            case BatteryChargeMode.SLOW:
                read |= (1 << 8)
                read &= ~(1 << 9)
                Register.Write(RtcController.__PWR_CR3, read)

    def GetSystemTime(self)->datetime:
        #dt = subprocess.check_output(["date"]) 
        return datetime.now()
    
    def SetSystemTime(self, dt: datetime):

        arg = "-s "

        arg += str(dt.year) + "."
        arg += str(dt.month) + "."
        arg += str(dt.day)

        arg += "-"
        arg += str(dt.hour) + ":"
        arg += str(dt.minute) + ":"
        arg += str(dt.second)

        subprocess.run(["date", arg])


    def __SetTime(self, dt: datetime):
        # Update system time
        self.SetSystemTime(dt)

        # Update rtc time
        arg = "-w"
        subprocess.run(["hwclock", arg])

    def __ReadTime(self)->datetime:
        months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
        hwclock = subprocess.check_output(["hwclock"]) 

        elements = hwclock.split()
        id = 0

        month = -1
        id +=1

        # month
        i = 0
        for m in months:
            if m.find((elements[id].decode("utf-8"))) != -1:
                month = i
                month +=1
                break
            i += 1

        # day of month
        id +=1

        if not elements[id]:
            id+=1

        dayofmonth = int(elements[id])

        # hour:min:sec
        id+=1

        elements2 = (elements[id].decode("utf-8")).split(':')
        hour = int(elements2[0])
        minute = int(elements2[1])
        second = int(elements2[2])

        # year
        id+=1
        year = int(elements[id])

        dt = datetime(year, month, dayofmonth, hour, minute, second)

        return dt
    
    @property
    def Now(self)->datetime:
        return self.__ReadTime()
    

    @Now.setter
    def Now(self, dt: datetime) -> None:
        self.__SetTime(dt)

        



        
                






