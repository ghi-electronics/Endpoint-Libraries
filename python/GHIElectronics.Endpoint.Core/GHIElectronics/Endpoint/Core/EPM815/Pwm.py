import os
import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio
from enum import IntEnum



class Pin(IntEnum):
    # controller1
    PA8 = Gpio.Pin.PA8

    PE11 = Gpio.Pin.PE11
    PA10 = Gpio.Pin.PA10
    PA11 = Gpio.Pin.PA11

    # controller2
    PA15 = Gpio.Pin.PA15
    PB3 = Gpio.Pin.PB3
    PA3 = Gpio.Pin.PA3

    # controller3
    PC6 = Gpio.Pin.PC6
    PB5 = Gpio.Pin.PB5
    PB0 = Gpio.Pin.PB0

    # controller4
    PD12 = Gpio.Pin.PD12
    PB7 = Gpio.Pin.PB7
    PD14 = Gpio.Pin.PD14
    PD15 = Gpio.Pin.PD15

    # controller5
    PA0 = Gpio.Pin.PA0
    PH11 = Gpio.Pin.PH11
    PH12 = Gpio.Pin.PH12
    PI0 = Gpio.Pin.PI0

    # controller8
    PI5 = Gpio.Pin.PI5
    PI6 = Gpio.Pin.PI6
    PI7 = Gpio.Pin.PI7
    PI2 = Gpio.Pin.PI2

    # controller12
    PH6 = Gpio.Pin.PH6
    PH9 = Gpio.Pin.PH9

    # controller13
    PA6 = Gpio.Pin.PA6

    # controller14
    PF9 = Gpio.Pin.PF9

    # controller15
    PE5 = Gpio.Pin.PE5
    PE6 = Gpio.Pin.PE6

    # Controller16
    PB8 = Gpio.Pin.PB8

    # Controller17
    PB9 = Gpio.Pin.PB9

 

class Controller1:
    PA8 = (0x0000 << 16) | (Gpio.Pin.PA8 << 8) | (int(Gpio.Alternate.AF1 << 0))
    PE11 = (0x0001 << 16) | (Gpio.Pin.PE11 << 8) | (int(Gpio.Alternate.AF1 << 0))
    PA10 = (0x0002 << 16) | (Gpio.Pin.PA10 << 8) | (int(Gpio.Alternate.AF1 << 0))
    PA11 = (0x0003 << 16) | (Gpio.Pin.PA11 << 8) | (int(Gpio.Alternate.AF1 << 0))


class Controller2 :
    PA15 = (0x0100 << 16) | (Gpio.Pin.PA15 << 8) | (int(Gpio.Alternate.AF1 << 0))
    PB3 = (0x0101 << 16) | (Gpio.Pin.PB3 << 8) | (int(Gpio.Alternate.AF1 << 0))
    PA3 = (0x0103 << 16) | (Gpio.Pin.PA3 << 8) | (int(Gpio.Alternate.AF1 << 0))


class Controller3 :
    PC6 = (0x0200 << 16) | (Gpio.Pin.PC6 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PB5 = (0x0201 << 16) | (Gpio.Pin.PB5 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PB0 = (0x0202 << 16) | (Gpio.Pin.PB0 << 8) | (int(Gpio.Alternate.AF2 << 0))


class Controller4:
    PD12 = (0x0300 << 16) | (Gpio.Pin.PD12 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PB7 = (0x0301 << 16) | (Gpio.Pin.PB7 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PD14 = (0x0302 << 16) | (Gpio.Pin.PD14 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PD15 = (0x0303 << 16) | (Gpio.Pin.PD15 << 8) | (int(Gpio.Alternate.AF2 << 0))


class Controller5:
    PA0 = (0x0400 << 16) | (Gpio.Pin.PA0 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PH11 = (0x0401 << 16) | (Gpio.Pin.PH11 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PH12 = (0x0402 << 16) | (Gpio.Pin.PH12 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PI0 = (0x0403 << 16) | (Gpio.Pin.PI0 << 8) | (int(Gpio.Alternate.AF2 << 0))


class Controller8:
    PI5 = (0x0700 << 16) | (Gpio.Pin.PI5 << 8) | (int(Gpio.Alternate.AF3 << 0))
    PI6 = (0x0701 << 16) | (Gpio.Pin.PI6 << 8) | (int(Gpio.Alternate.AF3 << 0))
    PI7 = (0x0702 << 16) | (Gpio.Pin.PI7 << 8) | (int(Gpio.Alternate.AF3 << 0))
    PI2 = (0x0703 << 16) | (Gpio.Pin.PI2 << 8) | (int(Gpio.Alternate.AF3 << 0))


class Controller12:
    PH6 = (0x0B00 << 16) | (Gpio.Pin.PH6 << 8) | (int(Gpio.Alternate.AF2 << 0))
    PH9 = (0x0B01 << 16) | (Gpio.Pin.PH9 << 8) | (int(Gpio.Alternate.AF2 << 0))


class Controller13:
    PA6 = (0x0C00 << 16) | (Gpio.Pin.PA6 << 8) | (int(Gpio.Alternate.AF9 << 0))


class Controller14:
    PF9 = (0x0D00 << 16) | (Gpio.Pin.PF9 << 8) | (int(Gpio.Alternate.AF9 << 0))


class Controller15:
    PE5 = (0x0E00 << 16) | (Gpio.Pin.PE5 << 8) | (int(Gpio.Alternate.AF4 << 0))
    PE6 = (0x0E01 << 16) | (Gpio.Pin.PE6 << 8) | (int(Gpio.Alternate.AF4 << 0))


class Controller16:
    PB8 = (0x0F00 << 16) | (Gpio.Pin.PB8 << 8) | (int(Gpio.Alternate.AF1 << 0))


class Controller17:
    PB9 = (0x1000 << 16) | (Gpio.Pin.PB9 << 8) | (int(Gpio.Alternate.AF1 << 0))




class PwmController(object):
    __InitializedList = []

    def __init__(self, pin: int): 
        self.__Initialize(pin)
        self.frequency = 0
        self.dutycycle = 0
        self.pin = pin
        self._channelId =self.__GetChannelId(pin)
        self._chipId =  self.__GetChipId(pin)
        self.base = '/sys/class/pwm/pwmchip{:d}'.format(self._chipId)
        self.path = self.base + '/pwm{:d}'.format(self._channelId)

        if not os.path.isdir(self.base):
            raise Exception('Invalid chip Id ' + self.base)
        
        if not os.path.isdir(self.path):
            with open(self.base + '/export', 'w') as f:
                f.write('{:d}'.format(self._channelId))
                
        
    def __del__(self):
        self.enable = False
        self.inversed = False

        if os.path.isdir(self.path):
            with open(self.base + '/unexport', 'w') as f:
                f.write('{:d}'.format(self._channelId))   

        self.__UnInitialize(self.pin) 

    @property
    def Frequency(self)->int:
       
        return self.frequency
    
    @Frequency.setter
    def Frequency(self, freq: int) -> None:

        self.frequency = freq

        period = 0

        if (self.frequency > 0):
            period = int(1000000000/self.frequency)

        with open(self.path + '/period', 'w') as f:
            f.write('{:d}'.format(period))

    

    @property
    def DutyCycle(self) -> int:        
       
        return self.dutycycle

    @DutyCycle.setter
    def DutyCycle(self, value: int) -> None:
        
        if (self.dutycycle > 1):
            raise Exception("DutyCycle must be in range [0..1]")
        self.dutycycle = value

    

    def Start(self) -> None:

        if (self.frequency >  0):
            period = int(1000000000/self.frequency)
        else :
            period = 0
            
        duty =  int(period * self.dutycycle)
        

        with open(self.path + '/duty_cycle', 'w') as f:
            f.write('{:d}'.format(duty))   

        with open(self.path + '/enable', 'w') as f:
            f.write('1')

    def Stop(self) -> None:
        with open(self.path + '/enable', 'w') as f:
            f.write('0')
           

    @property
    def Inversed(self) -> bool:
        """normal polarity or inversed, boolean"""
        with open(self.path + '/polarity', 'r') as f:
            value = f.readline().strip()

        return True if value == 'inversed' else False

    @Inversed.setter
    def Inversed(self, value: bool) -> None:
        with open(self.path + '/polarity', 'w') as f:
            if value:
                f.write('inversed')
            else:
                f.write('normal')

    def __GetChipId(self, pin: int) -> int:
        pwm_pin = self.__GetPinEncodeFromPin(pin)

        controllerId = (pwm_pin >> 24) & 0xFF

        return self.__ToActualController(controllerId)

    def __GetChannelId(self, pin: int) -> int:
        pwm_pin = self.__GetPinEncodeFromPin(pin)

        channelId = (pwm_pin >> 16) & 0xFF

        return channelId

    def __Initialize(self, pin: int):
        if (pin in PwmController.__InitializedList):
            return
        
        if Gpio.IsPinReserved(pin):
            Gpio.ThrowExceptionPinInUsed(pin)

        pwm_pin = self.__GetPinEncodeFromPin(pin)

        if (pwm_pin == Gpio.Pin.NONE):
            raise Exception(f"Pin {pin} does not support pwm.")
        
        pinId = (pwm_pin >> 8) & 0xFF
        alternateId = (pwm_pin >> 0) & 0xFF

        Gpio.SetModer(pinId, Gpio.Moder.Alternate)
        Gpio.SetAlternate(pinId, Gpio.Alternate(alternateId))

        Gpio.PinReserve(pinId)

        PwmController.__InitializedList.append(pin)
        


        
    def __UnInitialize(self, pin: int):
        if (pin in PwmController.__InitializedList):
            pwm_pin = self.__GetPinEncodeFromPin(pin)

            if (pwm_pin == Gpio.Pin.NONE):
                raise Exception(f"Pin {pin} does not support pwm.")
        
            pinId = (pwm_pin >> 8) & 0xFF
            alternateId = (pwm_pin >> 0) & 0xFF

            Gpio.SetModer(pinId, Gpio.Moder.Input)

            Gpio.PinRelease(pinId)

            PwmController.__InitializedList.remove(pin)


        return
    
    def __ToActualController(self, id: int) -> int:
        match id:
            case (1):
                return 0
            
            case (2):
                return 4
            
            case (3):
                return 8
            
            case (4):
                return 12
            
            case (11):
                return 16
            
            case (12):
                return 18
            
            case (13):
                return 19
            
            case (0):
                return 20
            
            case (7):
                return 24
            
            case (14):
                return 28
            
            case (15):
                return 30
            
            case (16):
                return 31  

        raise Exception("Invalid PWM controller")     

    def __GetPinEncodeFromPin(self, pin: int) -> int:
        match (pin) :
            # controller1
            case Gpio.Pin.PA8: return Controller1.PA8
            case Gpio.Pin.PE11: return Controller1.PE11
            case Gpio.Pin.PA10: return Controller1.PA10
            case Gpio.Pin.PA11: return Controller1.PA11

            # controller2
            case Gpio.Pin.PA15: return Controller2.PA15
            case Gpio.Pin.PB3: return Controller2.PB3
            case Gpio.Pin.PA3: return Controller2.PA3

            # controller3
            case Gpio.Pin.PC6: return Controller3.PC6
            case Gpio.Pin.PB5: return Controller3.PB5
            case Gpio.Pin.PB0: return Controller3.PB0

            # controller4
            case Gpio.Pin.PD12: return Controller4.PD12
            case Gpio.Pin.PB7: return Controller4.PB7
            case Gpio.Pin.PD14: return Controller4.PD14
            case Gpio.Pin.PD15: return Controller4.PD15

            # controller5
            case Gpio.Pin.PA0: return Controller5.PA0
            case Gpio.Pin.PH11: return Controller5.PH11
            case Gpio.Pin.PH12: return Controller5.PH12
            case Gpio.Pin.PI0: return Controller5.PI0

            # controller8
            case Gpio.Pin.PI5: return Controller8.PI5
            case Gpio.Pin.PI6: return Controller8.PI6
            case Gpio.Pin.PI7: return Controller8.PI7
            case Gpio.Pin.PI2: return Controller8.PI2

            # controller12
            case Gpio.Pin.PH6: return Controller12.PH6
            case Gpio.Pin.PH9: return Controller12.PH9

            # controller13
            case Gpio.Pin.PA6: return Controller13.PA6

            # controller14
            case Gpio.Pin.PF9: return Controller14.PF9

            # controller15
            case Gpio.Pin.PE5: return Controller15.PE5
            case Gpio.Pin.PE6: return Controller15.PE6

            # Controller16
            case Gpio.Pin.PB8: return Controller16.PB8

            # Controller17
            case Gpio.Pin.PB9: return Controller17.PB9

        return Gpio.Pin.NONE

