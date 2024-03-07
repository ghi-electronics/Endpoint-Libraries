
import struct
import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio   


Uart1 = "/dev/ttySTM0"
Uart2 = "/dev/ttySTM1"
Uart3 = "/dev/ttySTM2"
Uart4 = "/dev/ttySTM3"
Uart5 = "/dev/ttySTM4"
Uart7 = "/dev/ttySTM6"
Uart8 = "/dev/ttySTM7"

__InitializedList = []

__TX_ID = 0
__RX_ID = 1
__RTS_ID = 2
__CTS_ID =3
__TX_ALT = 4
__RX_ALT = 5
__RTS_ALT = 6
__CTS_ALT = 7

__PinSettings = [struct.pack('llllllll',Gpio.Pin.PZ7,Gpio.Pin.PZ6,Gpio.Pin.PA12,Gpio.Pin.PZ3, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7),
                 struct.pack('llllllll',Gpio.Pin.PF5,Gpio.Pin.PD6,Gpio.Pin.PD4,Gpio.Pin.PD3, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7),
                 struct.pack('llllllll',Gpio.Pin.PD8,Gpio.Pin.PD9,Gpio.Pin.PD12,Gpio.Pin.PD11, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7),
                 struct.pack('llllllll',Gpio.Pin.PD1,Gpio.Pin.PD0,Gpio.Pin.NONE,Gpio.Pin.NONE, Gpio.Alternate.AF8, Gpio.Alternate.AF8, Gpio.Alternate.NONE, Gpio.Alternate.NONE),
                 struct.pack('llllllll',Gpio.Pin.PB13,Gpio.Pin.PB12,Gpio.Pin.NONE,Gpio.Pin.NONE, Gpio.Alternate.AF14, Gpio.Alternate.AF14, Gpio.Alternate.NONE, Gpio.Alternate.NONE),
                 struct.pack('llllllll',Gpio.Pin.NONE,Gpio.Pin.NONE,Gpio.Pin.NONE,Gpio.Pin.NONE, Gpio.Alternate.NONE, Gpio.Alternate.NONE, Gpio.Alternate.NONE, Gpio.Alternate.NONE),
                 struct.pack('llllllll',Gpio.Pin.PE8,Gpio.Pin.PF6,Gpio.Pin.PE9,Gpio.Pin.PE10, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7, Gpio.Alternate.AF7),
                 struct.pack('llllllll',Gpio.Pin.PE1,Gpio.Pin.PE0,Gpio.Pin.NONE,Gpio.Pin.NONE, Gpio.Alternate.AF8, Gpio.Alternate.AF8, Gpio.Alternate.NONE, Gpio.Alternate.NONE),
                        
            ]

def __GetPortIdFromName(name: str):
    match(name):
        case "/dev/ttySTM0": 
            return 0
        case "/dev/ttySTM1": 
            return 1
        case "/dev/ttySTM2":  
            return 2
        case "/dev/ttySTM3": 
            return 3
        case "/dev/ttySTM4": 
            return 4
        case "/dev/ttySTM6": 
            return 6
        case "/dev/ttySTM7":  
            return 7        
    raise Exception("Invalid SerialPort")


def Initialize(portname: str, enableHardwareFlowControl: bool):

    port = __GetPortIdFromName(portname)
    
    if (port in __InitializedList):
        return
    
    
    pinConfig = struct.unpack('llllllll', __PinSettings[port])

    if Gpio.IsPinReserved(pinConfig[__TX_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__TX_ID])
    

    if Gpio.IsPinReserved(pinConfig[__RX_ID]):
        Gpio.ThrowExceptionPinInUsed(pinConfig[__RX_ID])

    if (enableHardwareFlowControl):
        if pinConfig[__RTS_ID] == Gpio.Pin.NONE or pinConfig[__CTS_ID] == Gpio.Pin.NONE :
            raise Exception(f"Port {port} not support handshaking.")
        
        if Gpio.IsPinReserved(pinConfig[__RTS_ID]):
            Gpio.ThrowExceptionPinInUsed(pinConfig[__RTS_ID])

        if Gpio.IsPinReserved(pinConfig[__CTS_ID]):
            Gpio.ThrowExceptionPinInUsed(pinConfig[__CTS_ID])
    


    Gpio.SetModer(pinConfig[__TX_ID], Gpio.Moder.Alternate)
    Gpio.SetModer(pinConfig[__RX_ID], Gpio.Moder.Alternate)
    Gpio.SetAlternate(pinConfig[__TX_ID], pinConfig[__TX_ALT])
    Gpio.SetAlternate(pinConfig[__RX_ID], pinConfig[__RX_ALT])
    Gpio.PinReserve(pinConfig[__TX_ID])
    Gpio.PinReserve(pinConfig[__RX_ID])

    if (enableHardwareFlowControl):
        Gpio.SetModer(pinConfig[__RTS_ID], Gpio.Moder.Alternate)
        Gpio.SetModer(pinConfig[__CTS_ID], Gpio.Moder.Alternate)
        Gpio.SetAlternate(pinConfig[__RTS_ID], pinConfig[__RTS_ALT])
        Gpio.SetAlternate(pinConfig[__CTS_ID], pinConfig[__CTS_ALT])
        Gpio.PinReserve(pinConfig[__RTS_ID])
        Gpio.PinReserve(pinConfig[__CTS_ID])

    __InitializedList.append(port)

    return

def UnInitialize(portname: str, enableHardwareFlowControl: bool):
    port = __GetPortIdFromName(portname)
    
    if (port in __InitializedList):        
        pinConfig = struct.unpack('llllllll', __PinSettings[port])               
        
        Gpio.SetModer(pinConfig[__TX_ID], Gpio.Moder.Input)
        Gpio.SetModer(pinConfig[__RX_ID], Gpio.Moder.Input)
        Gpio.PinRelease(pinConfig[__TX_ID])
        Gpio.PinRelease(pinConfig[__RX_ID])

        if (enableHardwareFlowControl):
            Gpio.SetModer(pinConfig[__RTS_ID], Gpio.Moder.Input)
            Gpio.SetModer(pinConfig[__CTS_ID], Gpio.Moder.Input)
            Gpio.PinRelease(pinConfig[__RTS_ID])
            Gpio.PinRelease(pinConfig[__CTS_ID])



        __InitializedList.remove(port)

    return