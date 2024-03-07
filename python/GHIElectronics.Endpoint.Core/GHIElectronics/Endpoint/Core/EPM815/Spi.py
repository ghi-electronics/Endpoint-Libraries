import os
import subprocess
import struct
import time
import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio   


Spi1 = 0
Spi4 = 1
Spi5 = 2

__InitializedList = []

__MOSI_ID = 0
__MISO_ID = 1
__CLK_ID = 2
__MOSI_ALT =3
__MISO_ALT = 4
__CLK_ALT = 5

__PinSettings = [struct.pack('llllll',Gpio.Pin.PZ2,Gpio.Pin.PZ1,Gpio.Pin.PZ0,Gpio.Alternate.AF5, Gpio.Alternate.AF5, Gpio.Alternate.AF5),
               struct.pack('llllll',Gpio.Pin.PE14,Gpio.Pin.PE13,Gpio.Pin.PE12,Gpio.Alternate.AF5, Gpio.Alternate.AF5, Gpio.Alternate.AF5),
               struct.pack('llllll', Gpio.Pin.PF9,Gpio.Pin.PF8,Gpio.Pin.PF7,Gpio.Alternate.AF5, Gpio.Alternate.AF5, Gpio.Alternate.AF5 ),               
            ]

def Initialize(port: int):
    if (port != Spi1 and port != Spi4 and  port != Spi5) :
        raise Exception("Invalid Spi port.")
    
    if (port in __InitializedList):
        return
    
    pinConfig = struct.unpack('llllll', __PinSettings[port])

    if Gpio.IsPinReserved(pinConfig[__MOSI_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__MOSI_ID])
    

    if Gpio.IsPinReserved(pinConfig[__MISO_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__MISO_ID])

    if Gpio.IsPinReserved(pinConfig[__CLK_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__CLK_ID])

    Gpio.SetModer(pinConfig[__MOSI_ID], Gpio.Moder.Alternate)
    Gpio.SetModer(pinConfig[__MISO_ID], Gpio.Moder.Alternate)
    Gpio.SetModer(pinConfig[__CLK_ID], Gpio.Moder.Alternate)

    Gpio.SetAlternate(pinConfig[__MOSI_ID], pinConfig[__MOSI_ALT])
    Gpio.SetAlternate(pinConfig[__MISO_ID], pinConfig[__MISO_ALT])
    Gpio.SetAlternate(pinConfig[__CLK_ID], pinConfig[__CLK_ALT])

    Gpio.PinReserve(pinConfig[__MOSI_ID])
    Gpio.PinReserve(pinConfig[__MISO_ID])
    Gpio.PinReserve(pinConfig[__CLK_ID])

    if (os.path.exists("/sys/class/spidev")):
        return
    
    subprocess.run(["modprobe", "spidev"]) 

    while (True):
        if (os.path.exists("/sys/class/spidev")):
            break

        time.sleep(0.1)

    __InitializedList.append(port)

    return

def UnInitialize(port: int):
    if (port != Spi1 and port != Spi4 and  port != Spi5) :
        raise Exception("Invalid Spi port.")
    
    if (port in __InitializedList):
        pinConfig = struct.unpack('llllll', __PinSettings[port])
        Gpio.SetModer(pinConfig[__MOSI_ID], Gpio.Moder.Input)
        Gpio.SetModer(pinConfig[__MISO_ID], Gpio.Moder.Input)
        Gpio.SetModer(pinConfig[__CLK_ID], Gpio.Moder.Input)

        Gpio.PinRelease(pinConfig[__MOSI_ID])
        Gpio.PinRelease(pinConfig[__MISO_ID])
        Gpio.PinRelease(pinConfig[__CLK_ID])

        __InitializedList.remove(port)

    return