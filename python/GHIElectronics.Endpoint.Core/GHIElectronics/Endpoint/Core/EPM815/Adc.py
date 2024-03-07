import os
import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio
from enum import IntEnum

import struct


_initializedList = []

class Pin(IntEnum):
    # controller1
    ANA0 = 0x0000_FFFF
    ANA1 = 0x0001_FFFF
    PF11 = 0x0002_005B
    PA6 = 0x0003_0006
    PF12 = 0x0006_005C
    PB0 = 0x0009_0010
    PC0 = 0x000A_0020
    PC3 = 0x000D_0023
    PA3 = 0x000F_0003
    PA0 = 0x0010_0000
    PA4 = 0x0012_0004
    PA5 = 0x0013_0005
    PF13 = 0x0102_005D
    PF14 = 0x0106_005E



InitializeCount = 0

class AdcController(object):
    

    def __init__(self, adcPin: int): 

        if (adcPin < 255) :
            pin = self.__GetPinEncodeFromPin(adcPin)

            if (pin == Gpio.Pin.NONE):
                raise Exception("Adc pin invalid")
        else :
            pin = adcPin

        self.controllerId = (pin >> 24) & 0xFF
        self.channelId = (pin >> 16) & 0xFF
        self.pinId = (pin>> 0) & 0xFFFF

        self.__Acquire()

        self.path = f"/sys/bus/iio/devices/iio:device{self.controllerId}/in_voltage{self.channelId}_raw"

        if os.path.exists(self.path):
            self.fd =  open(self.path, 'r')
        else :
            self.__Release()
            raise Exception("Could not create Adc device")
        
    def ReadRaw(self):       
        self.fd.seek(0,0) 
        buf_read = self.fd.readline()

        val = int(buf_read)

        return val
    
    def Read(self)-> float:

        v = self.ReadRaw()

        return float(v * 3.3 / 65535)
    
    
    def __Acquire(self):
        global InitializeCount

        if (InitializeCount ==0):
            self.__LoadResources()
        
        InitializeCount +=1

    def __Release(self):
        global InitializeCount

        InitializeCount -=1

        if (InitializeCount ==0):
            self.__UnLoadResources()
        
        


    def __LoadResources(self):
        if (self.controllerId == 0 and (self.channelId < 2)):
            return # ANA0 and ANA1 are special, no pin
        
        if (self.pinId >= 0 and self.pinId < 255) :
            Gpio.SetModer(self.pinId, Gpio.Moder.Analog)

            Gpio.PinReserve(self.pinId)

    def __UnLoadResources(self):
        if (self.controllerId == 0 and (self.channelId < 2)):
            return # ANA0 and ANA1 are special, no pin
        
        if (self.pinId >= 0 and self.pinId < 255) :
            Gpio.SetModer(self.pinId, Gpio.Moder.Input)

            Gpio.PinRelease(self.pinId)

    def __GetPinEncodeFromPin(self, pin: int)->int:
        match(pin):
            case Gpio.Pin.PF11: return Pin.PF11
            case Gpio.Pin.PA6: return Pin.PA6
            case Gpio.Pin.PF12: return Pin.PF12
            case Gpio.Pin.PB0: return Pin.PB0
            case Gpio.Pin.PC0: return Pin.PC0
            case Gpio.Pin.PC3: return Pin.PC3
            case Gpio.Pin.PA3: return Pin.PA3
            case Gpio.Pin.PA0: return Pin.PA0
            case Gpio.Pin.PA4: return Pin.PA4
            case Gpio.Pin.PA5: return Pin.PA5
            case Gpio.Pin.PF13: return Pin.PF13
            case Gpio.Pin.PF14: return Pin.PF14

        return Gpio.Pin.NONE

    @property
    def ResolutionInBits(self) -> int:
        return 16



   

