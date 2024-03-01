import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio
   
import struct

I2c1 = 0
I2c4 = 2
I2c5 = 1
I2c6 = 3

_initializedList = []
_sda_id = 0
_scl_id = 1
_sda_alt = 2
_scl_alt = 3

PinSettings = [struct.pack('llll',Gpio.Pin.PD13,Gpio.Pin.PD12,Gpio.Alternate.AF5, Gpio.Alternate.AF5),
               struct.pack('llll',Gpio.Pin.PZ5 , Gpio.Pin.PZ4 , Gpio.Alternate.AF4,  Gpio.Alternate.AF4),
               struct.pack('llll', Gpio.Pin.PF15,  Gpio.Pin.PB6 ,  Gpio.Alternate.AF4 ,   Gpio.Alternate.AF6 ),
               struct.pack('llll', Gpio.Pin.PD0, Gpio.Pin.PD1 , Gpio.Alternate.AF2 ,   Gpio.Alternate.AF2 ),
            ]

def Initialize(port: int, frequency_hz: int):
    if (port < I2c1 or port > I2c6) :
        raise Exception("Invalid I2c port.")
    

    if (port in _initializedList):
        return
    
    pinConfig = struct.unpack('llll', PinSettings[port])

    if Gpio.IsPinReserved(pinConfig[_scl_id]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[_scl_id])
    

    if Gpio.IsPinReserved(pinConfig[_sda_id]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[_sda_id])


    Gpio.SetModer(pinConfig[_scl_id], Gpio.Moder.Alternate)
    Gpio.SetModer(pinConfig[_sda_id], Gpio.Moder.Alternate)

    Gpio.SetAlternate(pinConfig[_scl_id], pinConfig[_scl_alt])
    Gpio.SetAlternate(pinConfig[_sda_id], pinConfig[_sda_alt])


    Gpio.SetPull(pinConfig[_scl_id], Gpio.Pull.Up)
    Gpio.SetPull(pinConfig[_sda_id], Gpio.Pull.Up)

    Gpio.SetOutputType(pinConfig[_scl_id], Gpio.OutputType.OpenDrain)
    Gpio.SetOutputType(pinConfig[_sda_id], Gpio.OutputType.OpenDrain)

    Gpio.PinReserve(pinConfig[_scl_id])
    Gpio.PinReserve(pinConfig[_sda_id])
    
    _initializedList.append(port)
    return

def UnInitialize(port: int):
    return