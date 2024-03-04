import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio
   
import struct

I2c1 = 0
I2c4 = 2
I2c5 = 1
I2c6 = 3

__InitializedList = []
__SDA_ID = 0
__SCL_ID = 1
__SDA_ALT = 2
__SCL_ALT = 3

__PinSettings = [struct.pack('llll',Gpio.Pin.PD13,Gpio.Pin.PD12,Gpio.Alternate.AF5, Gpio.Alternate.AF5),
               struct.pack('llll',Gpio.Pin.PZ5 , Gpio.Pin.PZ4 , Gpio.Alternate.AF4,  Gpio.Alternate.AF4),
               struct.pack('llll', Gpio.Pin.PF15,  Gpio.Pin.PB6 ,  Gpio.Alternate.AF4 ,   Gpio.Alternate.AF6 ),
               struct.pack('llll', Gpio.Pin.PD0, Gpio.Pin.PD1 , Gpio.Alternate.AF2 ,   Gpio.Alternate.AF2 ),
            ]

def Initialize(port: int, frequency_hz: int):
    if (port < I2c1 or port > I2c6) :
        raise Exception("Invalid I2c port.")
    

    if (port in __InitializedList):
        return
    
    pinConfig = struct.unpack('llll', __PinSettings[port])

    if Gpio.IsPinReserved(pinConfig[__SCL_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__SCL_ID])
    

    if Gpio.IsPinReserved(pinConfig[__SDA_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__SDA_ID])


    Gpio.SetModer(pinConfig[__SCL_ID], Gpio.Moder.Alternate)
    Gpio.SetModer(pinConfig[__SDA_ID], Gpio.Moder.Alternate)

    Gpio.SetAlternate(pinConfig[__SCL_ID], pinConfig[__SCL_ALT])
    Gpio.SetAlternate(pinConfig[__SDA_ID], pinConfig[__SDA_ALT])


    Gpio.SetPull(pinConfig[__SCL_ID], Gpio.Pull.Up)
    Gpio.SetPull(pinConfig[__SDA_ID], Gpio.Pull.Up)

    Gpio.SetOutputType(pinConfig[__SCL_ID], Gpio.OutputType.OpenDrain)
    Gpio.SetOutputType(pinConfig[__SDA_ID], Gpio.OutputType.OpenDrain)

    Gpio.PinReserve(pinConfig[__SCL_ID])
    Gpio.PinReserve(pinConfig[__SDA_ID])
    
    __InitializedList.append(port)
    return

def UnInitialize(port: int):
    if (port < I2c1 or port > I2c6) :
        raise Exception("Invalid I2c port.")
    
    if (port in __InitializedList):
        pinConfig = struct.unpack('llll', __PinSettings[port])

        Gpio.PinRelease(pinConfig[__SCL_ID])
        Gpio.PinRelease(pinConfig[__SDA_ID])

        Gpio.SetModer(pinConfig[__SCL_ID], Gpio.Moder.Input)
        Gpio.SetModer(pinConfig[__SDA_ID], Gpio.Moder.Input)

        __InitializedList.remove(port)
    
    return