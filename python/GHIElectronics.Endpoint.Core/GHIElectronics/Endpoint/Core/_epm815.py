"""The EPM815 module's pins, buses and ports, the same as .NET's
GHIElectronics.Endpoint.Core.EPM815. Initialize connects a bus's or port's
pins (and sets its speed); then the Endpoint packages, pyserial or
python-can use it."""

from ._native import native


def _name(pin):
    if 0 <= pin <= 151:
        port = pin // 16
        return f"P{'Z' if port == 9 else chr(ord('A') + port)}{pin % 16}"
    return f"#{pin}"


class EPM815:
    """The EPM815 (STM32MP15x) module"""

    class Gpio:
        """GPIO pins, for GHIElectronics.Endpoint.Devices.Gpio's GpioController"""

        class Pin:
            """The EPM815's pins: port * 16 + line, ports A to I, then Z"""
            PA0 = 0
            PA1 = 1
            PA2 = 2
            PA3 = 3
            PA4 = 4
            PA5 = 5
            PA6 = 6
            PA7 = 7
            PA8 = 8
            PA9 = 9
            PA10 = 10
            PA11 = 11
            PA12 = 12
            PA13 = 13
            PA14 = 14
            PA15 = 15
            PB0 = 16
            PB1 = 17
            PB2 = 18
            PB3 = 19
            PB4 = 20
            PB5 = 21
            PB6 = 22
            PB7 = 23
            PB8 = 24
            PB9 = 25
            PB10 = 26
            PB11 = 27
            PB12 = 28
            PB13 = 29
            PB14 = 30
            PB15 = 31
            PC0 = 32
            PC1 = 33
            PC2 = 34
            PC3 = 35
            PC4 = 36
            PC5 = 37
            PC6 = 38
            PC7 = 39
            PC8 = 40
            PC9 = 41
            PC10 = 42
            PC11 = 43
            PC12 = 44
            PC13 = 45
            PC14 = 46
            PC15 = 47
            PD0 = 48
            PD1 = 49
            PD2 = 50
            PD3 = 51
            PD4 = 52
            PD5 = 53
            PD6 = 54
            PD7 = 55
            PD8 = 56
            PD9 = 57
            PD10 = 58
            PD11 = 59
            PD12 = 60
            PD13 = 61
            PD14 = 62
            PD15 = 63
            PE0 = 64
            PE1 = 65
            PE2 = 66
            PE3 = 67
            PE4 = 68
            PE5 = 69
            PE6 = 70
            PE7 = 71
            PE8 = 72
            PE9 = 73
            PE10 = 74
            PE11 = 75
            PE12 = 76
            PE13 = 77
            PE14 = 78
            PE15 = 79
            PF0 = 80
            PF1 = 81
            PF2 = 82
            PF3 = 83
            PF4 = 84
            PF5 = 85
            PF6 = 86
            PF7 = 87
            PF8 = 88
            PF9 = 89
            PF10 = 90
            PF11 = 91
            PF12 = 92
            PF13 = 93
            PF14 = 94
            PF15 = 95
            PG0 = 96
            PG1 = 97
            PG2 = 98
            PG3 = 99
            PG4 = 100
            PG5 = 101
            PG6 = 102
            PG7 = 103
            PG8 = 104
            PG9 = 105
            PG10 = 106
            PG11 = 107
            PG12 = 108
            PG13 = 109
            PG14 = 110
            PG15 = 111
            PH0 = 112
            PH1 = 113
            PH2 = 114
            PH3 = 115
            PH4 = 116
            PH5 = 117
            PH6 = 118
            PH7 = 119
            PH8 = 120
            PH9 = 121
            PH10 = 122
            PH11 = 123
            PH12 = 124
            PH13 = 125
            PH14 = 126
            PH15 = 127
            PI0 = 128
            PI1 = 129
            PI2 = 130
            PI3 = 131
            PI4 = 132
            PI5 = 133
            PI6 = 134
            PI7 = 135
            PI8 = 136
            PI9 = 137
            PI10 = 138
            PI11 = 139
            PI12 = 140
            PI13 = 141
            PI14 = 142
            PI15 = 143
            PZ0 = 144
            PZ1 = 145
            PZ2 = 146
            PZ3 = 147
            PZ4 = 148
            PZ5 = 149
            PZ6 = 150
            PZ7 = 151
            NONE = -1
            """No pin"""

        @staticmethod
        def Name(pin):
            """The pin's name, for example "PC0\""""
            return _name(pin)

    class I2c:
        """The I2C buses, for GHIElectronics.Endpoint.Devices.I2c:

            EPM815.I2c.Initialize(EPM815.I2c.I2c1)
            device = I2cDevice.Create(I2cConnectionSettings(EPM815.I2c.I2c1, 0x50))
        """

        I2c1 = 0
        """I2C1: PD12 (SCL), PD13 (SDA)"""
        I2c5 = 1
        """I2C5: PZ4 (SCL), PZ5 (SDA)"""
        I2c4 = 2
        """I2C4: PB6 (SCL), PF15 (SDA)"""
        I2c6 = 3
        """I2C6: PD1 (SCL), PD0 (SDA)"""

        DefaultFrequency = 400_000
        """The speed when none is given: 400 kHz (fast mode)"""
        MinFrequency = 10_000
        MaxFrequency = 1_000_000
        """1 MHz (fast mode plus)"""

        @staticmethod
        def Initialize(bus, frequency=400_000):
            """Connects the bus's pins (open drain, internal pull-ups on) and sets its speed"""
            native.i2c_initialize(bus, frequency)

        @staticmethod
        def UnInitialize(bus):
            """Gives the bus's pins back, as they were before Initialize"""
            native.i2c_uninitialize(bus)

        @staticmethod
        def GetFrequency(bus):
            """The bus speed in use, in Hz"""
            frequency = native.out_int()
            native.i2c_get_frequency(bus, native.ref(frequency))
            return frequency.value

    class Spi:
        """The SPI buses, for GHIElectronics.Endpoint.Devices.Spi:

            EPM815.Spi.Initialize(EPM815.Spi.Spi4)
            device = SpiDevice.Create(SpiConnectionSettings(EPM815.Spi.Spi4, 0))

        Chip select is not driven by the bus: use any GPIO pin."""

        Spi1 = 0
        """SPI1: PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI)"""
        Spi4 = 1
        """SPI4: PE12 (SCK), PE13 (MISO), PE14 (MOSI)"""
        Spi5 = 2
        """SPI5: PF7 (SCK), PF8 (MISO), PF9 (MOSI)"""

        DefaultBufferSize = 4096
        """The largest transfer when none is given"""

        @staticmethod
        def Initialize(bus, bufferSize=4096):
            """Connects the bus's pins and loads the SPI driver if needed.
            bufferSize is the largest transfer: the first Initialize after boot decides it."""
            native.spi_initialize(bus, bufferSize)

        @staticmethod
        def UnInitialize(bus):
            """Gives the bus's pins back, as they were before Initialize"""
            native.spi_uninitialize(bus)

    class Pwm:
        """PWM pins, for GHIElectronics.Endpoint.Devices.Pwm:

            EPM815.Pwm.Initialize(EPM815.Pwm.Pin.PA8)
            pwm = PwmChannel.Create(EPM815.Pwm.GetChipId(EPM815.Pwm.Pin.PA8),
                                    EPM815.Pwm.GetChannelId(EPM815.Pwm.Pin.PA8), 1000, 0.5)

        Channels of one timer share its frequency."""

        class Pin:
            """The pins with PWM (the timer and channel are in the comments)"""
            PA8 = 8       # TIM1 channel 1
            PE11 = 75     # TIM1 channel 2
            PA10 = 10     # TIM1 channel 3
            PA11 = 11     # TIM1 channel 4
            PA15 = 15     # TIM2 channel 1
            PB3 = 19      # TIM2 channel 2
            PA3 = 3       # TIM2 channel 4
            PC6 = 38      # TIM3 channel 1
            PB5 = 21      # TIM3 channel 2
            PB0 = 16      # TIM3 channel 3
            PD12 = 60     # TIM4 channel 1
            PB7 = 23      # TIM4 channel 2
            PD14 = 62     # TIM4 channel 3
            PD15 = 63     # TIM4 channel 4
            PA0 = 0       # TIM5 channel 1
            PH11 = 123    # TIM5 channel 2
            PH12 = 124    # TIM5 channel 3
            PI0 = 128     # TIM5 channel 4
            PI5 = 133     # TIM8 channel 1
            PI6 = 134     # TIM8 channel 2
            PI7 = 135     # TIM8 channel 3
            PI2 = 130     # TIM8 channel 4
            PH6 = 118     # TIM12 channel 1
            PH9 = 121     # TIM12 channel 2
            PA6 = 6       # TIM13 channel 1
            PF9 = 89      # TIM14 channel 1
            PE5 = 69      # TIM15 channel 1
            PE6 = 70      # TIM15 channel 2
            PB8 = 24      # TIM16 channel 1
            PB9 = 25      # TIM17 channel 1

        @staticmethod
        def Initialize(pin):
            """Connects the pin to its timer"""
            native.pwm_initialize(pin)

        @staticmethod
        def UnInitialize(pin):
            """Gives the pin back, as it was before Initialize"""
            native.pwm_uninitialize(pin)

        @staticmethod
        def GetChipId(pin):
            """The PWM chip of the pin's timer, for PwmChannel.Create"""
            chip = native.out_int()
            native.pwm_get_chip_id(pin, native.ref(chip))
            return chip.value

        @staticmethod
        def GetChannelId(pin):
            """The pin's channel in its PWM chip, for PwmChannel.Create"""
            channel = native.out_int()
            native.pwm_get_channel_id(pin, native.ref(channel))
            return channel.value

    class SerialPort:
        """The serial ports (UARTs), for pyserial:

            EPM815.SerialPort.Initialize(EPM815.SerialPort.Uart4)
            port = serial.Serial(EPM815.SerialPort.Uart4, 115200)
        """

        Uart1 = "/dev/ttySTM0"
        """UART1: PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS)"""
        Uart2 = "/dev/ttySTM1"
        """UART2: PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS)"""
        Uart3 = "/dev/ttySTM2"
        """UART3: PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS)"""
        Uart4 = "/dev/ttySTM3"
        """UART4: PD1 (TX), PD0 (RX)"""
        Uart5 = "/dev/ttySTM4"
        """UART5: PB13 (TX), PB12 (RX)"""
        Uart7 = "/dev/ttySTM6"
        """UART7: PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS)"""
        Uart8 = "/dev/ttySTM7"
        """UART8: PE1 (TX), PE0 (RX)"""

        @staticmethod
        def Initialize(portName, enableHardwareFlowControl=False):
            """Connects the port's TX and RX pins, and RTS and CTS for hardware flow control"""
            native.serialport_initialize(portName.encode(), int(enableHardwareFlowControl))

        @staticmethod
        def UnInitialize(portName):
            """Gives the port's pins back, as they were before Initialize"""
            native.serialport_uninitialize(portName.encode())

    class Can:
        """The CAN ports, for python-can:

            EPM815.Can.Initialize(EPM815.Can.Can1, 500_000)
            bus = can.Bus(interface="socketcan", channel=EPM815.Can.Can1)

        A port that goes bus-off after too many errors restarts by itself after 100 ms."""

        Can1 = "can0"
        """FDCAN1: PD1 (TX), PD0 (RX)"""
        Can2 = "can1"
        """FDCAN2: PB13 (TX), PB12 (RX)"""

        MaxBitrate = 1_000_000
        """The highest bitrate (1 Mbit/s)"""

        @staticmethod
        def Initialize(port, bitrate):
            """Connects the port's pins, sets its bitrate and starts it"""
            native.can_initialize(port.encode(), bitrate)

        @staticmethod
        def UnInitialize(port):
            """Stops the port and gives its pins back, as they were before Initialize"""
            native.can_uninitialize(port.encode())

    class Adc:
        """The analog inputs, for GHIElectronics.Endpoint.Devices.Adc's AdcController"""

        class Pin:
            """The pins with an analog input (the converter and channel are in the comments)"""
            ANA0 = 0x1000  # a dedicated analog input, ADC1 channel 0
            ANA1 = 0x1001  # a dedicated analog input, ADC1 channel 1
            PF11 = 91      # ADC1 channel 2
            PA6 = 6        # ADC1 channel 3
            PF12 = 92      # ADC1 channel 6
            PB0 = 16       # ADC1 channel 9
            PC0 = 32       # ADC1 channel 10
            PC3 = 35       # ADC1 channel 13
            PA3 = 3        # ADC1 channel 15
            PA0 = 0        # ADC1 channel 16
            PA4 = 4        # ADC1 channel 18
            PA5 = 5        # ADC1 channel 19
            PF13 = 93      # ADC2 channel 2
            PF14 = 94      # ADC2 channel 6

    class Dac:
        """The analog outputs, for GHIElectronics.Endpoint.Devices.Dac (the same API as TinyCLR):

            dac = DacController.FromName(EPM815.Dac.Id)
            output = dac.OpenChannel(EPM815.Dac.PA5)
            output.WriteValue(0.5)    # half of 3.3 V
        """

        Id = "EPM815.Dac"
        """The DAC's name, for DacController.FromName"""
        PA4 = 0
        """DAC channel 1, on PA4"""
        PA5 = 1
        """DAC channel 2, on PA5"""

    class Watchdog:
        """The watchdog, for GHIElectronics.Endpoint.Devices.Watchdog (the same API as TinyCLR)"""

        Id = "EPM815.Watchdog"
        """The watchdog's name, for WatchdogController.FromName"""

    class StorageController:
        """SD cards and USB drives, for GHIElectronics.Endpoint.Devices.Storage:

            usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage)
        """

        SdCard = "EPM815.StorageController.SdCard"
        """The SD card slot (mounted on /media/sd/)"""
        UsbHostMassStorage = "EPM815.StorageController.UsbHostMassStorage"
        """The USB drives (mounted on /media/usb0/, usb1, ...: the first free number)"""
