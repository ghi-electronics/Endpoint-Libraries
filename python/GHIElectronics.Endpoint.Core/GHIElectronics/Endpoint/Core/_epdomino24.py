"""EP-Domino24 Rev B: a board variant of the EPM815 module with a 40-pin
header, two mikroBUS sockets and a handful of onboard components. Same
chip and firmware as EPM815; this class is a thin re-naming layer that
lists only the peripherals and pins actually bonded on the board, so
application code reads what a user looking at the EP-Domino24 silkscreen
expects."""

from ._epm815 import EPM815


class EPDomino24:
    """EP-Domino24 Rev B (EPM815-based) board"""

    # ---------------------------------------------------------------
    # Onboard components
    # ---------------------------------------------------------------

    Led = EPM815.Gpio.Pin.PC0
    """User LED on PC0"""
    Buzzer = EPM815.Gpio.Pin.PF9
    """Piezo buzzer on PF9 (also PWM-capable via TIM14 CH1)"""
    ButtonF1 = EPM815.Gpio.Pin.PF3
    """Function button F1 on PF3"""
    ButtonF2 = EPM815.Gpio.Pin.PH2
    """Function button F2 on PH2"""
    ButtonF3 = EPM815.Gpio.Pin.PH3
    """Function button F3 on PH3"""
    Backlight = EPM815.Gpio.Pin.PD14
    """Panel backlight on PD14 (also PWM-capable via TIM4 CH3)"""

    # ---------------------------------------------------------------
    # GPIO pins exposed on EP-Domino24's headers (40-pin header + two
    # mikroBUS sockets + the five onboard component pins).
    # ---------------------------------------------------------------

    class Gpio:
        """GPIO pins exposed on EP-Domino24."""

        @staticmethod
        def GetChipId(pin):
            """Linux gpiochip number for the pin. Delegates to
            EPM815.Gpio.GetChipId because EP-Domino24 is an EPM815 board."""
            return EPM815.Gpio.GetChipId(pin)

        @staticmethod
        def GetLineId(pin):
            """Line number within the gpiochip (0 to 15). Pairs with
            GetChipId. Delegates to EPM815.Gpio.GetLineId."""
            return EPM815.Gpio.GetLineId(pin)

        class Pin:
            """Pins bonded on EP-Domino24. Subset of EPM815.Gpio.Pin."""
            PA0 = EPM815.Gpio.Pin.PA0
            PA3 = EPM815.Gpio.Pin.PA3
            PA4 = EPM815.Gpio.Pin.PA4
            PA5 = EPM815.Gpio.Pin.PA5
            PA6 = EPM815.Gpio.Pin.PA6
            PA10 = EPM815.Gpio.Pin.PA10
            PA12 = EPM815.Gpio.Pin.PA12
            PA13 = EPM815.Gpio.Pin.PA13
            PA14 = EPM815.Gpio.Pin.PA14
            PA15 = EPM815.Gpio.Pin.PA15
            PB6 = EPM815.Gpio.Pin.PB6
            PB9 = EPM815.Gpio.Pin.PB9
            PB11 = EPM815.Gpio.Pin.PB11   # on-board native display FT5xx6 touch IRQ (falling edge)
            PB12 = EPM815.Gpio.Pin.PB12
            PB13 = EPM815.Gpio.Pin.PB13
            PC0 = EPM815.Gpio.Pin.PC0
            PC3 = EPM815.Gpio.Pin.PC3
            PD0 = EPM815.Gpio.Pin.PD0
            PD1 = EPM815.Gpio.Pin.PD1
            PD3 = EPM815.Gpio.Pin.PD3
            PD4 = EPM815.Gpio.Pin.PD4
            PD6 = EPM815.Gpio.Pin.PD6
            PD8 = EPM815.Gpio.Pin.PD8
            PD9 = EPM815.Gpio.Pin.PD9
            PD11 = EPM815.Gpio.Pin.PD11
            PD12 = EPM815.Gpio.Pin.PD12
            PD13 = EPM815.Gpio.Pin.PD13
            PD15 = EPM815.Gpio.Pin.PD15
            PE0 = EPM815.Gpio.Pin.PE0
            PE1 = EPM815.Gpio.Pin.PE1
            PE4 = EPM815.Gpio.Pin.PE4
            PE5 = EPM815.Gpio.Pin.PE5
            PE8 = EPM815.Gpio.Pin.PE8
            PE9 = EPM815.Gpio.Pin.PE9
            PE10 = EPM815.Gpio.Pin.PE10
            PE12 = EPM815.Gpio.Pin.PE12
            PE13 = EPM815.Gpio.Pin.PE13
            PE14 = EPM815.Gpio.Pin.PE14
            PF2 = EPM815.Gpio.Pin.PF2    # on-board native display FT5xx6 touch reset (active-low pulse)
            PF3 = EPM815.Gpio.Pin.PF3
            PF4 = EPM815.Gpio.Pin.PF4
            PF5 = EPM815.Gpio.Pin.PF5
            PF6 = EPM815.Gpio.Pin.PF6
            PF8 = EPM815.Gpio.Pin.PF8
            PF9 = EPM815.Gpio.Pin.PF9
            PF11 = EPM815.Gpio.Pin.PF11
            PF12 = EPM815.Gpio.Pin.PF12
            PF13 = EPM815.Gpio.Pin.PF13
            PF14 = EPM815.Gpio.Pin.PF14
            PF15 = EPM815.Gpio.Pin.PF15
            PG0 = EPM815.Gpio.Pin.PG0
            PG9 = EPM815.Gpio.Pin.PG9
            PG10 = EPM815.Gpio.Pin.PG10
            PH2 = EPM815.Gpio.Pin.PH2
            PH3 = EPM815.Gpio.Pin.PH3
            PH9 = EPM815.Gpio.Pin.PH9
            PH10 = EPM815.Gpio.Pin.PH10
            PH12 = EPM815.Gpio.Pin.PH12
            PI4 = EPM815.Gpio.Pin.PI4
            PZ0 = EPM815.Gpio.Pin.PZ0
            PZ1 = EPM815.Gpio.Pin.PZ1
            PZ2 = EPM815.Gpio.Pin.PZ2
            PZ3 = EPM815.Gpio.Pin.PZ3
            PZ6 = EPM815.Gpio.Pin.PZ6
            PZ7 = EPM815.Gpio.Pin.PZ7

    # ---------------------------------------------------------------
    # Peripherals bonded on EP-Domino24. A mikroBUS slot is just a
    # header: the peripheral it maps to is called out in the comment.
    # ---------------------------------------------------------------

    class I2c:
        """I2C buses bonded on EP-Domino24."""
        I2c1 = EPM815.I2c.I2c1  # PD12 (SCL), PD13 (SDA); 40-pin header
        I2c4 = EPM815.I2c.I2c4  # PB6 (SCL), PF15 (SDA); mikroBUS #2
        I2c5 = EPM815.I2c.I2c5  # PZ4 (SCL), PZ5 (SDA); on-board native display FT5xx6 touch (not on a header)
        I2c6 = EPM815.I2c.I2c6  # PD1 (SCL), PD0 (SDA); mikroBUS #1

    class Spi:
        """SPI buses bonded on EP-Domino24."""
        Spi1 = EPM815.Spi.Spi1  # PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI); mikroBUS #2
        Spi4 = EPM815.Spi.Spi4  # PE12 (SCK), PE13 (MISO), PE14 (MOSI); mikroBUS #1

    class SerialPort:
        """Serial ports bonded on EP-Domino24."""
        Uart1 = EPM815.SerialPort.Uart1  # PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS); mikroBUS #1 CS/INT/RST
        Uart2 = EPM815.SerialPort.Uart2  # PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS); mikroBUS #1
        Uart3 = EPM815.SerialPort.Uart3  # PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS); mikroBUS #2
        Uart4 = EPM815.SerialPort.Uart4  # PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and CAN1)
        Uart5 = EPM815.SerialPort.Uart5  # PB13 (TX), PB12 (RX); 40-pin header (shares pins with CAN2)
        Uart7 = EPM815.SerialPort.Uart7  # PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS); 40-pin header
        Uart8 = EPM815.SerialPort.Uart8  # PE1 (TX), PE0 (RX); 40-pin header

    class Can:
        """CAN buses bonded on EP-Domino24."""
        Can1 = EPM815.Can.Can1  # FDCAN1: PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and UART4)
        Can2 = EPM815.Can.Can2  # FDCAN2: PB13 (TX), PB12 (RX); 40-pin header (shares pins with UART5)

    class Pwm:
        """PWM-capable pins bonded on EP-Domino24."""

        class Pin:
            """PWM pins. Subset of EPM815.Pwm.Pin."""
            PA0 = EPM815.Pwm.Pin.PA0    # TIM5 channel 1 - mikroBUS #2 PWM
            PA3 = EPM815.Pwm.Pin.PA3    # TIM2 channel 4 - mikroBUS #1 PWM
            PA6 = EPM815.Pwm.Pin.PA6    # TIM13 channel 1
            PA10 = EPM815.Pwm.Pin.PA10  # TIM1 channel 3
            PA15 = EPM815.Pwm.Pin.PA15  # TIM2 channel 1
            PB9 = EPM815.Pwm.Pin.PB9    # TIM17 channel 1
            PD12 = EPM815.Pwm.Pin.PD12  # TIM4 channel 1
            PD15 = EPM815.Pwm.Pin.PD15  # TIM4 channel 4
            PE5 = EPM815.Pwm.Pin.PE5    # TIM15 channel 1
            PF9 = EPM815.Pwm.Pin.PF9    # TIM14 channel 1 - the onboard buzzer
            PH9 = EPM815.Pwm.Pin.PH9    # TIM12 channel 2
            PH12 = EPM815.Pwm.Pin.PH12  # TIM5 channel 3

    class Adc:
        """Analog-input pins bonded on EP-Domino24."""

        class Pin:
            """ADC pins. Subset of EPM815.Adc.Pin."""
            PA0 = EPM815.Adc.Pin.PA0    # ADC1 channel 16
            PA3 = EPM815.Adc.Pin.PA3    # ADC1 channel 15
            PA4 = EPM815.Adc.Pin.PA4    # ADC1 channel 18 (shares pin with DAC channel 1)
            PA5 = EPM815.Adc.Pin.PA5    # ADC1 channel 19 (shares pin with DAC channel 2)
            PA6 = EPM815.Adc.Pin.PA6    # ADC1 channel 3
            PC3 = EPM815.Adc.Pin.PC3    # ADC1 channel 13 - mikroBUS #1 ADC
            PF11 = EPM815.Adc.Pin.PF11  # ADC1 channel 2
            PF12 = EPM815.Adc.Pin.PF12  # ADC1 channel 6
            PF13 = EPM815.Adc.Pin.PF13  # ADC2 channel 2
            PF14 = EPM815.Adc.Pin.PF14  # ADC2 channel 6 - mikroBUS #2 ADC

    class Dac:
        """DAC: two analog outputs on PA4 and PA5 (shared with ADC on the same pins)."""
        Id = EPM815.Dac.Id
        """The DAC's name, for DacController.FromName"""
        PA4 = EPM815.Dac.PA4
        """DAC channel 1, on PA4"""
        PA5 = EPM815.Dac.PA5
        """DAC channel 2, on PA5"""

    class Watchdog:
        """Watchdog - same as EPM815.Watchdog."""
        Id = EPM815.Watchdog.Id
        """The watchdog's name, for WatchdogController.FromName"""

    class StorageController:
        """Storage controllers bonded on EP-Domino24. Same strings as EPM815.StorageController."""
        SdCard = EPM815.StorageController.SdCard
        """The SD card slot (mounted on /media/sd/)"""
        UsbHostMassStorage = EPM815.StorageController.UsbHostMassStorage
        """The USB drives (mounted on /media/usb0/, usb1, ...: the first free number)"""
