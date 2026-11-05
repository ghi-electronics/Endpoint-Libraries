namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// EP-Domino24 Rev B: a board variant of the EPM815 module with a 40-pin
    /// header, two mikroBUS sockets and a handful of onboard components.
    /// Same chip and firmware as <see cref="EPM815"/>; this class is a
    /// thin re-naming layer that lists only the peripherals and pins actually
    /// bonded on the board, so application code reads what a user looking at
    /// the EP-Domino24 silkscreen expects.
    /// </summary>
    /// <example>
    /// <code>
    /// using var gpio = new GpioController(new EPDomino24.Gpio.Driver());
    /// gpio.OpenPin(EPDomino24.Led, PinMode.Output);
    /// gpio.Write(EPDomino24.Led, PinValue.High);
    /// </code>
    /// </example>
    public static class EPDomino24 {
        // ---------------------------------------------------------------
        // Onboard components
        // ---------------------------------------------------------------

        /// <summary>User LED on PC0.</summary>
        public const int Led = EPM815.Gpio.Pin.PC0;
        /// <summary>Piezo buzzer on PF9 (also PWM-capable via TIM14 CH1).</summary>
        public const int Buzzer = EPM815.Gpio.Pin.PF9;
        /// <summary>Function button F1 on PF3.</summary>
        public const int ButtonF1 = EPM815.Gpio.Pin.PF3;
        /// <summary>Function button F2 on PH2.</summary>
        public const int ButtonF2 = EPM815.Gpio.Pin.PH2;
        /// <summary>Function button F3 on PH3.</summary>
        public const int ButtonF3 = EPM815.Gpio.Pin.PH3;

        // ---------------------------------------------------------------
        // GPIO pins exposed on EP-Domino24's headers (40-pin header + two
        // mikroBUS sockets + the five onboard component pins).
        // ---------------------------------------------------------------

        /// <summary>GPIO pins and the board's <see cref="Driver"/>.</summary>
        public static class Gpio {
            /// <summary>Pins bonded on EP-Domino24. Subset of <see cref="EPM815.Gpio.Pin"/>.</summary>
            public static class Pin {
                public const int PA0  = EPM815.Gpio.Pin.PA0;
                public const int PA3  = EPM815.Gpio.Pin.PA3;
                public const int PA4  = EPM815.Gpio.Pin.PA4;
                public const int PA5  = EPM815.Gpio.Pin.PA5;
                public const int PA6  = EPM815.Gpio.Pin.PA6;
                public const int PA10 = EPM815.Gpio.Pin.PA10;
                public const int PA12 = EPM815.Gpio.Pin.PA12;
                public const int PA13 = EPM815.Gpio.Pin.PA13;
                public const int PA14 = EPM815.Gpio.Pin.PA14;
                public const int PA15 = EPM815.Gpio.Pin.PA15;
                public const int PB6  = EPM815.Gpio.Pin.PB6;
                public const int PB9  = EPM815.Gpio.Pin.PB9;
                public const int PB12 = EPM815.Gpio.Pin.PB12;
                public const int PB13 = EPM815.Gpio.Pin.PB13;
                public const int PC0  = EPM815.Gpio.Pin.PC0;
                public const int PC3  = EPM815.Gpio.Pin.PC3;
                public const int PD0  = EPM815.Gpio.Pin.PD0;
                public const int PD1  = EPM815.Gpio.Pin.PD1;
                public const int PD3  = EPM815.Gpio.Pin.PD3;
                public const int PD4  = EPM815.Gpio.Pin.PD4;
                public const int PD6  = EPM815.Gpio.Pin.PD6;
                public const int PD8  = EPM815.Gpio.Pin.PD8;
                public const int PD9  = EPM815.Gpio.Pin.PD9;
                public const int PD11 = EPM815.Gpio.Pin.PD11;
                public const int PD12 = EPM815.Gpio.Pin.PD12;
                public const int PD13 = EPM815.Gpio.Pin.PD13;
                public const int PD15 = EPM815.Gpio.Pin.PD15;
                public const int PE0  = EPM815.Gpio.Pin.PE0;
                public const int PE1  = EPM815.Gpio.Pin.PE1;
                public const int PE4  = EPM815.Gpio.Pin.PE4;
                public const int PE5  = EPM815.Gpio.Pin.PE5;
                public const int PE8  = EPM815.Gpio.Pin.PE8;
                public const int PE9  = EPM815.Gpio.Pin.PE9;
                public const int PE10 = EPM815.Gpio.Pin.PE10;
                public const int PE12 = EPM815.Gpio.Pin.PE12;
                public const int PE13 = EPM815.Gpio.Pin.PE13;
                public const int PE14 = EPM815.Gpio.Pin.PE14;
                public const int PF3  = EPM815.Gpio.Pin.PF3;
                public const int PF4  = EPM815.Gpio.Pin.PF4;
                public const int PF5  = EPM815.Gpio.Pin.PF5;
                public const int PF6  = EPM815.Gpio.Pin.PF6;
                public const int PF8  = EPM815.Gpio.Pin.PF8;
                public const int PF9  = EPM815.Gpio.Pin.PF9;
                public const int PF11 = EPM815.Gpio.Pin.PF11;
                public const int PF12 = EPM815.Gpio.Pin.PF12;
                public const int PF13 = EPM815.Gpio.Pin.PF13;
                public const int PF14 = EPM815.Gpio.Pin.PF14;
                public const int PF15 = EPM815.Gpio.Pin.PF15;
                public const int PG0  = EPM815.Gpio.Pin.PG0;
                public const int PG9  = EPM815.Gpio.Pin.PG9;
                public const int PG10 = EPM815.Gpio.Pin.PG10;
                public const int PH2  = EPM815.Gpio.Pin.PH2;
                public const int PH3  = EPM815.Gpio.Pin.PH3;
                public const int PH9  = EPM815.Gpio.Pin.PH9;
                public const int PH10 = EPM815.Gpio.Pin.PH10;
                public const int PH12 = EPM815.Gpio.Pin.PH12;
                public const int PI4  = EPM815.Gpio.Pin.PI4;
                public const int PZ0  = EPM815.Gpio.Pin.PZ0;
                public const int PZ1  = EPM815.Gpio.Pin.PZ1;
                public const int PZ2  = EPM815.Gpio.Pin.PZ2;
                public const int PZ3  = EPM815.Gpio.Pin.PZ3;
                public const int PZ6  = EPM815.Gpio.Pin.PZ6;
                public const int PZ7  = EPM815.Gpio.Pin.PZ7;
            }

            /// <summary>Same driver as <see cref="EPM815.Gpio.Driver"/>, named for the board.</summary>
            public class Driver : EPM815.Gpio.Driver { }
        }

        // ---------------------------------------------------------------
        // Peripherals bonded on EP-Domino24. A mikroBUS slot is just a
        // header: the peripheral it maps to is called out in the comment.
        // ---------------------------------------------------------------

        /// <summary>I²C buses bonded on EP-Domino24.</summary>
        public static class I2c {
            /// <summary>I2C1: PD12 (SCL), PD13 (SDA); 40-pin header.</summary>
            public const int I2c1 = EPM815.I2c.I2c1;
            /// <summary>I2C4: PB6 (SCL), PF15 (SDA); mikroBUS #2.</summary>
            public const int I2c4 = EPM815.I2c.I2c4;
            /// <summary>I2C6: PD1 (SCL), PD0 (SDA); mikroBUS #1.</summary>
            public const int I2c6 = EPM815.I2c.I2c6;
        }

        /// <summary>SPI buses bonded on EP-Domino24.</summary>
        public static class Spi {
            /// <summary>SPI1: PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI); mikroBUS #2.</summary>
            public const int Spi1 = EPM815.Spi.Spi1;
            /// <summary>SPI4: PE12 (SCK), PE13 (MISO), PE14 (MOSI); mikroBUS #1.</summary>
            public const int Spi4 = EPM815.Spi.Spi4;
        }

        /// <summary>Serial ports bonded on EP-Domino24.</summary>
        public static class SerialPort {
            /// <summary>UART1: PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS); mikroBUS #1 CS/INT/RST pins.</summary>
            public const string Uart1 = EPM815.SerialPort.Uart1;
            /// <summary>UART2: PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS); mikroBUS #1.</summary>
            public const string Uart2 = EPM815.SerialPort.Uart2;
            /// <summary>UART3: PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS); mikroBUS #2.</summary>
            public const string Uart3 = EPM815.SerialPort.Uart3;
            /// <summary>UART4: PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and CAN1).</summary>
            public const string Uart4 = EPM815.SerialPort.Uart4;
            /// <summary>UART5: PB13 (TX), PB12 (RX); 40-pin header (shares pins with CAN2).</summary>
            public const string Uart5 = EPM815.SerialPort.Uart5;
            /// <summary>UART7: PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS); 40-pin header.</summary>
            public const string Uart7 = EPM815.SerialPort.Uart7;
            /// <summary>UART8: PE1 (TX), PE0 (RX); 40-pin header.</summary>
            public const string Uart8 = EPM815.SerialPort.Uart8;
        }

        /// <summary>CAN buses bonded on EP-Domino24.</summary>
        public static class Can {
            /// <summary>FDCAN1: PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and UART4).</summary>
            public const string Can1 = EPM815.Can.Can1;
            /// <summary>FDCAN2: PB13 (TX), PB12 (RX); 40-pin header (shares pins with UART5).</summary>
            public const string Can2 = EPM815.Can.Can2;
        }

        /// <summary>PWM-capable pins bonded on EP-Domino24.</summary>
        public static class Pwm {
            /// <summary>PWM pins. Subset of <see cref="EPM815.Pwm.Pin"/>.</summary>
            public static class Pin {
                /// <summary>TIM5 channel 1 — mikroBUS #2 PWM.</summary>
                public const int PA0  = EPM815.Pwm.Pin.PA0;
                /// <summary>TIM2 channel 4 — mikroBUS #1 PWM.</summary>
                public const int PA3  = EPM815.Pwm.Pin.PA3;
                /// <summary>TIM13 channel 1.</summary>
                public const int PA6  = EPM815.Pwm.Pin.PA6;
                /// <summary>TIM1 channel 3.</summary>
                public const int PA10 = EPM815.Pwm.Pin.PA10;
                /// <summary>TIM2 channel 1.</summary>
                public const int PA15 = EPM815.Pwm.Pin.PA15;
                /// <summary>TIM17 channel 1.</summary>
                public const int PB9  = EPM815.Pwm.Pin.PB9;
                /// <summary>TIM4 channel 1.</summary>
                public const int PD12 = EPM815.Pwm.Pin.PD12;
                /// <summary>TIM4 channel 4.</summary>
                public const int PD15 = EPM815.Pwm.Pin.PD15;
                /// <summary>TIM15 channel 1.</summary>
                public const int PE5  = EPM815.Pwm.Pin.PE5;
                /// <summary>TIM14 channel 1 — the onboard buzzer.</summary>
                public const int PF9  = EPM815.Pwm.Pin.PF9;
                /// <summary>TIM12 channel 2.</summary>
                public const int PH9  = EPM815.Pwm.Pin.PH9;
                /// <summary>TIM5 channel 3.</summary>
                public const int PH12 = EPM815.Pwm.Pin.PH12;
            }
        }

        /// <summary>Analog-input pins bonded on EP-Domino24.</summary>
        public static class Adc {
            /// <summary>ADC pins. Subset of <see cref="EPM815.Adc.Pin"/>.</summary>
            public static class Pin {
                /// <summary>ADC1 channel 16.</summary>
                public const int PA0  = EPM815.Adc.Pin.PA0;
                /// <summary>ADC1 channel 15.</summary>
                public const int PA3  = EPM815.Adc.Pin.PA3;
                /// <summary>ADC1 channel 18 (shares pin with DAC channel 1).</summary>
                public const int PA4  = EPM815.Adc.Pin.PA4;
                /// <summary>ADC1 channel 19 (shares pin with DAC channel 2).</summary>
                public const int PA5  = EPM815.Adc.Pin.PA5;
                /// <summary>ADC1 channel 3.</summary>
                public const int PA6  = EPM815.Adc.Pin.PA6;
                /// <summary>ADC1 channel 13 — mikroBUS #1 ADC.</summary>
                public const int PC3  = EPM815.Adc.Pin.PC3;
                /// <summary>ADC1 channel 2.</summary>
                public const int PF11 = EPM815.Adc.Pin.PF11;
                /// <summary>ADC1 channel 6.</summary>
                public const int PF12 = EPM815.Adc.Pin.PF12;
                /// <summary>ADC2 channel 2.</summary>
                public const int PF13 = EPM815.Adc.Pin.PF13;
                /// <summary>ADC2 channel 6 — mikroBUS #2 ADC.</summary>
                public const int PF14 = EPM815.Adc.Pin.PF14;
            }
        }

        /// <summary>DAC: two analog outputs on PA4 and PA5 (shared with ADC on the same pins).</summary>
        public static class Dac {
            /// <summary>Name for <c>DacController.FromName</c>.</summary>
            public const string Id = EPM815.Dac.Id;
            /// <summary>DAC channel 1, on PA4.</summary>
            public const int PA4 = EPM815.Dac.PA4;
            /// <summary>DAC channel 2, on PA5.</summary>
            public const int PA5 = EPM815.Dac.PA5;
        }

        /// <summary>Watchdog — same as <see cref="EPM815.Watchdog"/>.</summary>
        public static class Watchdog {
            /// <summary>Name for <c>WatchdogController.FromName</c>.</summary>
            public const string Id = EPM815.Watchdog.Id;
        }
    }
}
