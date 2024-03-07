from enum import Enum
from enum import IntEnum

import GHIElectronics.Endpoint.Core.EPM815.Register as Register

MAX_PORT = 10
MAX_PIN_PERPORT = 16

class Alternate(IntEnum):
    AF0=0
    AF1=1
    AF2=2
    AF3=3
    AF4=4
    AF5=5
    AF6=6
    AF7=7
    AF8=8
    AF9=9
    AF10=10
    AF11=11
    AF12=12
    AF13=13
    AF14=14
    AF15=15
    NONE = -1

class Moder(IntEnum):
    Input = 0
    Output = 1
    Alternate = 2
    Analog = 3
    NONE = -1

class OutputType(IntEnum):
    PushPull = 0
    OpenDrain = 1

class Pull(IntEnum):
    NONE = 0
    Up = 1
    Down = 2

class Speed(IntEnum):
    Low = 0,
    Medium = 1,
    High = 2,
    VeryHigh = 3,

class Speed(IntEnum):
    Rising = 1,
    Falling = 2,

class Pin(IntEnum):
    #  <summary>GPIO pin.</summary>
    PA0 = 0
    #  <summary>GPIO pin.</summary>
    PA1 = 1
    #  <summary>GPIO pin.</summary>
    PA2 = 2
    #  <summary>GPIO pin.</summary>
    PA3 = 3
    #  <summary>GPIO pin.</summary>
    PA4 = 4
    #  <summary>GPIO pin.</summary>
    PA5 = 5
    #  <summary>GPIO pin.</summary>
    PA6 = 6
    #  <summary>GPIO pin.</summary>
    PA7 = 7
    #  <summary>GPIO pin.</summary>
    PA8 = 8
    #  <summary>GPIO pin.</summary>
    PA9 = 9
    #  <summary>GPIO pin.</summary>
    PA10 = 10
    #  <summary>GPIO pin.</summary>
    PA11 = 11
    #  <summary>GPIO pin.</summary>
    PA12 = 12
    #  <summary>GPIO pin.</summary>
    PA13 = 13
    #  <summary>GPIO pin.</summary>
    PA14 = 14
    #  <summary>GPIO pin.</summary>
    PA15 = 15
    #  <summary>GPIO pin.</summary>
    PB0 = 0 + 16
    #  <summary>GPIO pin.</summary>
    PB1 = 1 + 16
    #  <summary>GPIO pin.</summary>
    PB2 = 2 + 16
    #  <summary>GPIO pin.</summary>
    PB3 = 3 + 16
    #  <summary>GPIO pin.</summary>
    PB4 = 4 + 16
    #  <summary>GPIO pin.</summary>
    PB5 = 5 + 16
    #  <summary>GPIO pin.</summary>
    PB6 = 6 + 16
    #  <summary>GPIO pin.</summary>
    PB7 = 7 + 16
    #  <summary>GPIO pin.</summary>
    PB8 = 8 + 16
    #  <summary>GPIO pin.</summary>
    PB9 = 9 + 16
    #  <summary>GPIO pin.</summary>
    PB10 = 10 + 16
    #  <summary>GPIO pin.</summary>
    PB11 = 11 + 16
    #  <summary>GPIO pin.</summary>
    PB12 = 12 + 16
    #  <summary>GPIO pin.</summary>
    PB13 = 13 + 16
    #  <summary>GPIO pin.</summary>
    PB14 = 14 + 16
    #  <summary>GPIO pin.</summary>
    PB15 = 15 + 16
    #  <summary>GPIO pin.</summary>
    PC0 = 0 + 32
    #  <summary>GPIO pin.</summary>
    PC1 = 1 + 32
    #  <summary>GPIO pin.</summary>
    PC2 = 2 + 32
    #  <summary>GPIO pin.</summary>
    PC3 = 3 + 32
    #  <summary>GPIO pin.</summary>
    PC4 = 4 + 32
    #  <summary>GPIO pin.</summary>
    PC5 = 5 + 32
    #  <summary>GPIO pin.</summary>
    PC6 = 6 + 32
    #  <summary>GPIO pin.</summary>
    PC7 = 7 + 32
    #  <summary>GPIO pin.</summary>
    PC8 = 8 + 32
    #  <summary>GPIO pin.</summary>
    PC9 = 9 + 32
    #  <summary>GPIO pin.</summary>
    PC10 = 10 + 32
    #  <summary>GPIO pin.</summary>
    PC11 = 11 + 32
    #  <summary>GPIO pin.</summary>
    PC12 = 12 + 32
    #  <summary>GPIO pin.</summary>
    PC13 = 13 + 32
    #  <summary>GPIO pin.</summary>
    PC14 = 14 + 32
    #  <summary>GPIO pin.</summary>
    PC15 = 15 + 32
    #  <summary>GPIO pin.</summary>
    PD0 = 0 + 48
    #  <summary>GPIO pin.</summary>
    PD1 = 1 + 48
    #  <summary>GPIO pin.</summary>
    PD2 = 2 + 48
    #  <summary>GPIO pin.</summary>
    PD3 = 3 + 48
    #  <summary>GPIO pin.</summary>
    PD4 = 4 + 48
    #  <summary>GPIO pin.</summary>
    PD5 = 5 + 48
    #  <summary>GPIO pin.</summary>
    PD6 = 6 + 48
    #  <summary>GPIO pin.</summary>
    PD7 = 7 + 48
    #  <summary>GPIO pin.</summary>
    PD8 = 8 + 48
    #  <summary>GPIO pin.</summary>
    PD9 = 9 + 48
    #  <summary>GPIO pin.</summary>
    PD10 = 10 + 48
    #  <summary>GPIO pin.</summary>
    PD11 = 11 + 48
    #  <summary>GPIO pin.</summary>
    PD12 = 12 + 48
    #  <summary>GPIO pin.</summary>
    PD13 = 13 + 48
    #  <summary>GPIO pin.</summary>
    PD14 = 14 + 48
    #  <summary>GPIO pin.</summary>
    PD15 = 15 + 48
    #  <summary>GPIO pin.</summary>
    PE0 = 0 + 64
    #  <summary>GPIO pin.</summary>
    PE1 = 1 + 64
    #  <summary>GPIO pin.</summary>
    PE2 = 2 + 64
    #  <summary>GPIO pin.</summary>
    PE3 = 3 + 64
    #  <summary>GPIO pin.</summary>
    PE4 = 4 + 64
    #  <summary>GPIO pin.</summary>
    PE5 = 5 + 64
    #  <summary>GPIO pin.</summary>
    PE6 = 6 + 64
    #  <summary>GPIO pin.</summary>
    PE7 = 7 + 64
    #  <summary>GPIO pin.</summary>
    PE8 = 8 + 64
    #  <summary>GPIO pin.</summary>
    PE9 = 9 + 64
    #  <summary>GPIO pin.</summary>
    PE10 = 10 + 64
    #  <summary>GPIO pin.</summary>
    PE11 = 11 + 64
    #  <summary>GPIO pin.</summary>
    PE12 = 12 + 64
    #  <summary>GPIO pin.</summary>
    PE13 = 13 + 64
    #  <summary>GPIO pin.</summary>
    PE14 = 14 + 64
    #  <summary>GPIO pin.</summary>
    PE15 = 15 + 64
    #  <summary>GPIO pin.</summary>
    PF0 = 0 + 80
    #  <summary>GPIO pin.</summary>
    PF1 = 1 + 80
    #  <summary>GPIO pin.</summary>
    PF2 = 2 + 80
    #  <summary>GPIO pin.</summary>
    PF3 = 3 + 80
    #  <summary>GPIO pin.</summary>
    PF4 = 4 + 80
    #  <summary>GPIO pin.</summary>
    PF5 = 5 + 80
    #  <summary>GPIO pin.</summary>
    PF6 = 6 + 80
    #  <summary>GPIO pin.</summary>
    PF7 = 7 + 80
    #  <summary>GPIO pin.</summary>
    PF8 = 8 + 80
    #  <summary>GPIO pin.</summary>
    PF9 = 9 + 80
    #  <summary>GPIO pin.</summary>
    PF10 = 10 + 80
    #  <summary>GPIO pin.</summary>
    PF11 = 11 + 80
    #  <summary>GPIO pin.</summary>
    PF12 = 12 + 80
    #  <summary>GPIO pin.</summary>
    PF13 = 13 + 80
    #  <summary>GPIO pin.</summary>
    PF14 = 14 + 80
    #  <summary>GPIO pin.</summary>
    PF15 = 15 + 80
    #  <summary>GPIO pin.</summary>
    PG0 = 0 + 96
    #  <summary>GPIO pin.</summary>
    PG1 = 1 + 96
    #  <summary>GPIO pin.</summary>
    PG2 = 2 + 96
    #  <summary>GPIO pin.</summary>
    PG3 = 3 + 96
    #  <summary>GPIO pin.</summary>
    PG4 = 4 + 96
    #  <summary>GPIO pin.</summary>
    PG5 = 5 + 96
    #  <summary>GPIO pin.</summary>
    PG6 = 6 + 96
    #  <summary>GPIO pin.</summary>
    PG7 = 7 + 96
    #  <summary>GPIO pin.</summary>
    PG8 = 8 + 96
    #  <summary>GPIO pin.</summary>
    PG9 = 9 + 96
    #  <summary>GPIO pin.</summary>
    PG10 = 10 + 96
    #  <summary>GPIO pin.</summary>
    PG11 = 11 + 96
    #  <summary>GPIO pin.</summary>
    PG12 = 12 + 96
    #  <summary>GPIO pin.</summary>
    PG13 = 13 + 96
    #  <summary>GPIO pin.</summary>
    PG14 = 14 + 96
    #  <summary>GPIO pin.</summary>
    PG15 = 15 + 96
    #  <summary>GPIO pin.</summary>
    PH0 = 0 + 112
    #  <summary>GPIO pin.</summary>
    PH1 = 1 + 112
    #  <summary>GPIO pin.</summary>
    PH2 = 2 + 112
    #  <summary>GPIO pin.</summary>
    PH3 = 3 + 112
    #  <summary>GPIO pin.</summary>
    PH4 = 4 + 112
    #  <summary>GPIO pin.</summary>
    PH5 = 5 + 112
    #  <summary>GPIO pin.</summary>
    PH6 = 6 + 112
    #  <summary>GPIO pin.</summary>
    PH7 = 7 + 112
    #  <summary>GPIO pin.</summary>
    PH8 = 8 + 112
    #  <summary>GPIO pin.</summary>
    PH9 = 9 + 112
    #  <summary>GPIO pin.</summary>
    PH10 = 10 + 112
    #  <summary>GPIO pin.</summary>
    PH11 = 11 + 112
    #  <summary>GPIO pin.</summary>
    PH12 = 12 + 112
    #  <summary>GPIO pin.</summary>
    PH13 = 13 + 112
    #  <summary>GPIO pin.</summary>
    PH14 = 14 + 112
    #  <summary>GPIO pin.</summary>
    PH15 = 15 + 112
    #  <summary>GPIO pin.</summary>
    PI0 = 0 + 128
    #  <summary>GPIO pin.</summary>
    PI1 = 1 + 128
    #  <summary>GPIO pin.</summary>
    PI2 = 2 + 128
    #  <summary>GPIO pin.</summary>
    PI3 = 3 + 128
    #  <summary>GPIO pin.</summary>
    PI4 = 4 + 128
    #  <summary>GPIO pin.</summary>
    PI5 = 5 + 128
    #  <summary>GPIO pin.</summary>
    PI6 = 6 + 128
    #  <summary>GPIO pin.</summary>
    PI7 = 7 + 128
    #  <summary>GPIO pin.</summary>
    PI8 = 8 + 128
    #  <summary>GPIO pin.</summary>
    PI9 = 9 + 128
    #  <summary>GPIO pin.</summary>
    PI10 = 10 + 128
    #  <summary>GPIO pin.</summary>
    PI11 = 11 + 128
    #  <summary>GPIO pin.</summary>
    PI12 = 12 + 128
    #  <summary>GPIO pin.</summary>
    PI13 = 13 + 128
    #  <summary>GPIO pin.</summary>
    PI14 = 14 + 128
    #  <summary>GPIO pin.</summary>
    PI15 = 15 + 128
    #  <summary>GPIO pin.</summary>
    PJ0 = 0 + 144
    #  <summary>GPIO pin.</summary>
    PJ1 = 1 + 144
    #  <summary>GPIO pin.</summary>
    PJ2 = 2 + 144
    #  <summary>GPIO pin.</summary>
    PJ3 = 3 + 144
    #  <summary>GPIO pin.</summary>
    PJ4 = 4 + 144
    #  <summary>GPIO pin.</summary>
    PJ5 = 5 + 144
    #  <summary>GPIO pin.</summary>
    PJ6 = 6 + 144
    #  <summary>GPIO pin.</summary>
    PJ7 = 7 + 144
    #  <summary>GPIO pin.</summary>
    PJ8 = 8 + 144
    #  <summary>GPIO pin.</summary>
    PJ9 = 9 + 144
    #  <summary>GPIO pin.</summary>
    PJ10 = 10 + 144
    #  <summary>GPIO pin.</summary>
    PJ11 = 11 + 144
    #  <summary>GPIO pin.</summary>
    PJ12 = 12 + 144
    #  <summary>GPIO pin.</summary>
    PJ13 = 13 + 144
    #  <summary>GPIO pin.</summary>
    PJ14 = 14 + 144
    #  <summary>GPIO pin.</summary>
    PJ15 = 15 + 144
    #  <summary>GPIO pin.</summary>
    PK0 = 0 + 160
    #  <summary>GPIO pin.</summary>
    PK1 = 1 + 160
    #  <summary>GPIO pin.</summary>
    PK2 = 2 + 160
    #  <summary>GPIO pin.</summary>
    PK3 = 3 + 160
    #  <summary>GPIO pin.</summary>
    PK4 = 4 + 160
    #  <summary>GPIO pin.</summary>
    PK5 = 5 + 160
    #  <summary>GPIO pin.</summary>
    PK6 = 6 + 160
    #  <summary>GPIO pin.</summary>
    PK7 = 7 + 160
    #  <summary>GPIO pin.</summary>
    PK8 = 8 + 160
    #  <summary>GPIO pin.</summary>
    PK9 = 9 + 160
    #  <summary>GPIO pin.</summary>
    PK10 = 10 + 160
    #  <summary>GPIO pin.</summary>
    PK11 = 11 + 160
    #  <summary>GPIO pin.</summary>
    PK12 = 12 + 160
    #  <summary>GPIO pin.</summary>
    PK13 = 13 + 160
    #  <summary>GPIO pin.</summary>
    PK14 = 14 + 160
    #  <summary>GPIO pin.</summary>
    PK15 = 15 + 160
    #  <summary>GPIO pin.</summary>
    PZ0 = 0 + 144
    #  <summary>GPIO pin.</summary>
    PZ1 = 1 + 144
    #  <summary>GPIO pin.</summary>
    PZ2 = 2 + 144
    #  <summary>GPIO pin.</summary>
    PZ3 = 3 + 144
    #  <summary>GPIO pin.</summary>
    PZ4 = 4 + 144
    #  <summary>GPIO pin.</summary>
    PZ5 = 5 + 144
    #  <summary>GPIO pin.</summary>
    PZ6 = 6 + 144
    #  <summary>GPIO pin.</summary>
    PZ7 = 7 + 144
    #  <summary>GPIO pin.</summary>
    NONE = -1

_RCC_MP_AHB4ENSETR = 0x50000A28

_RCC_MP_AHB5ENSETR = 0x50000210

_GPIO_BASE_REG = 0x50002000
_GPIOZ_BASE_REG = 0x54004000

_GPIO_MODER_REG_OFFSET = 0x00000000
_GPIO_OTYPER_REG_OFFSET = 0x00000004
_GPIO_OSPEEDR_REG_OFFSET = 0x00000008
_GPIO_PUPDR_REG_OFFSET = 0x0000000C
_GPIO_AFRL_REG_OFFSET = 0x00000020
_GPIO_AFRH_REG_OFFSET = 0x00000024

_Pins = [0] * 10
def ThrowExceptionPinNotInRange(pin_id: int):
    raise Exception(f'{pin_id} is out of cpu supported range.')

def ThrowExceptionPinInUsed(pin_id: int):
    raise Exception(f'{pin_id} is already in used.')

def SetModer(pin_id: int, moder: Moder):
    port_id = int(pin_id / 16)
    port_base = _GPIO_BASE_REG + port_id * 0x1000

    pin_id = pin_id % 16

    if (port_id == 9) :
        port_base = _GPIOZ_BASE_REG
        port_id = 0
        Register.Write(_RCC_MP_AHB5ENSETR, 1 << port_id)
    
    else:     
        
        Register.Write(_RCC_MP_AHB4ENSETR, 1 << port_id)
    

    value = Register.Read(port_base + _GPIO_MODER_REG_OFFSET)

    clear = 3 << (pin_id * 2) 

    value &= ~clear

    set = moder << (pin_id * 2)

    value |= set

    Register.Write(port_base + _GPIO_MODER_REG_OFFSET, value)

def SetAlternate(pin_id: int, alt: Alternate):
    port_id = int(pin_id / 16)
    port_base = _GPIO_BASE_REG + port_id * 0x1000

    pin_id = pin_id % 16

    if (port_id == 9) :
    
        port_base = _GPIOZ_BASE_REG
        port_id = 0
        Register.Write(_RCC_MP_AHB5ENSETR,(1 << port_id))
    
    else :
   
        Register.Write(_RCC_MP_AHB4ENSETR,(1 << port_id))
    
    id = pin_id

    if (pin_id > 7):
        id = pin_id - 8

    reg = port_base + _GPIO_AFRL_REG_OFFSET

    if (pin_id > 7): 

        reg = port_base + _GPIO_AFRH_REG_OFFSET

    value = Register.Read(reg)

    clear = 0x0F << (id * 4) 

    value &=~clear

    set =alt << (id * 4)

    value |= set

    Register.Write(reg, value)

def SetOutputType(pin_id: int, typeOutputType):
    port_id = int(pin_id / 16)
    port_base = _GPIO_BASE_REG + port_id * 0x1000

    pin_id = pin_id % 16

    if (port_id == 9) :
    
        port_base = _GPIOZ_BASE_REG
        port_id = 0
        Register.Write(_RCC_MP_AHB5ENSETR, (1 << port_id))
    
    else :
    
        Register.Write(_RCC_MP_AHB4ENSETR, (1 << port_id))
    

    value = Register.Read(port_base + _GPIO_OTYPER_REG_OFFSET)

    if (type == OutputType.PushPull):
        value &= ~(1 << pin_id)
    
    else :
        value |= (1 << pin_id)
    
    Register.Write(port_base + _GPIO_OTYPER_REG_OFFSET, value)

def SetPull(pin_id: int, pull: Pull):
    port_id = int(pin_id / 16)
    port_base = _GPIO_BASE_REG + port_id * 0x1000

    if (port_id == 9):
        port_base = _GPIOZ_BASE_REG
        port_id = 0
        Register.Write(_RCC_MP_AHB5ENSETR, (1 << port_id))
    
    else:    
        Register.Write(_RCC_MP_AHB4ENSETR, (1 << port_id))
    

    pin_id = pin_id % 16

    value = Register.Read(port_base + _GPIO_PUPDR_REG_OFFSET)

    clear = 3 << (pin_id * 2) 

    value &= ~(clear) // clear

    set = pull << (pin_id * 2)

    value |= set

    Register.Write(port_base + _GPIO_PUPDR_REG_OFFSET, value)

def SetSpeed(pin_id : int , speed: Speed ):

    port_id = int(pin_id / 16)
    port_base = _GPIO_BASE_REG + port_id * 0x1000

    pin_id = pin_id % 16

    if (port_id == 9):
    
        port_base = _GPIOZ_BASE_REG
        port_id = 0
        Register.Write(_RCC_MP_AHB5ENSETR, (1 <<port_id))
    
    else :
    
        Register.Write(_RCC_MP_AHB4ENSETR, (1 <<port_id))
    

    value = Register.Read(port_base + _GPIO_OSPEEDR_REG_OFFSET)

    clear = 3 << (pin_id * 2) 

    value &= ~(clear) // clear

    set = speed << (pin_id * 2)

    value |= set

    Register.Write(port_base + _GPIO_OSPEEDR_REG_OFFSET, value)

def PinReserve(pin: int):
    pin_port = int(pin / 16)
    pin_num = int(pin % 16)

    if (pin_port >= MAX_PORT or pin_num >= MAX_PIN_PERPORT):
        ThrowExceptionPinNotInRange(pin)
    

    if ((_Pins[pin_port] & (1 << pin_num)) != 0):
        ThrowExceptionPinInUsed(pin)
    

    _Pins[pin_port] |= (1 << pin_num)
    
def PinRelease(pin: int):
    pin_port = int(pin / 16)
    pin_num = int(pin % 16)

    if (pin_port >= MAX_PORT or pin_num >= MAX_PIN_PERPORT):
        ThrowExceptionPinNotInRange(pin)

    _Pins[pin_port] &= ~(1 << pin_num)

def IsPinReserved(pin: int)->bool:
    pin_port = int(pin / 16)
    pin_num = int(pin % 16)

    if (pin_port >= MAX_PORT or pin_num >= MAX_PIN_PERPORT):
        ThrowExceptionPinNotInRange(pin)

    return (_Pins[pin_port] & (1 << pin_num)) != 0

def GetPort(pin: int):
    return pin/ MAX_PORT

def GetPin(pin: int):
    return pin % MAX_PORT
