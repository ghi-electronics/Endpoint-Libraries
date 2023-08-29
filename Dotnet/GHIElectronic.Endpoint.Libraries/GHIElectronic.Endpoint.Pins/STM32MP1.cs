using System.Diagnostics;
using GHIElectronic.Endpoint.Libraries;
using static GHIElectronic.Endpoint.Pins.STM32MP1;

namespace GHIElectronic.Endpoint.Pins {
    /// <summary>Board definition for the STM32H7.</summary>
    public static partial class STM32MP1 {
        public enum Alternate : int {
            AF0 = 0,
            AF1 = 1,
            AF2 = 2,
            AF3 = 3,
            AF4 = 4,
            AF5 = 5,
            AF6 = 6,
            AF7 = 7,
            AF8 = 8,
            AF9 = 9,
            AF10 = 10,
            AF11 = 11,
            AF12 = 12,
            AF13 = 13,
            AF14 = 14,
            AF15 = 15,
            NONE = -1
        }

        public enum Port : uint {
            A = 0,
            B = 1,
            C = 2,
            D = 3,
            E = 4,
            F = 5,
            G = 6,
            H = 7,
            I = 8,          
            Z = 9,
        }

        public enum Moder : int {
            Input = 0,
            Gpio = 1,
            Alternate = 2,
            Analog = 3,
            NONE = -1,
        }

        public enum OutputType : int {
            PushPull = 0,
            OpenDrain = 1,
        }

        public enum Pull : int {
            None = 0,
            Up = 1,
            Down = 2,
        }

        public enum Speed : int {
            Low = 0,
            Medium = 1,
            High = 2,
            VeryHigh = 3,
        }

        /// <summary>GPIO pin definitions.</summary>
        public static class GpioPin {
            /// <summary>GPIO pin.</summary>
            public const int PA0 = 0;
            /// <summary>GPIO pin.</summary>
            public const int PA1 = 1;
            /// <summary>GPIO pin.</summary>
            public const int PA2 = 2;
            /// <summary>GPIO pin.</summary>
            public const int PA3 = 3;
            /// <summary>GPIO pin.</summary>
            public const int PA4 = 4;
            /// <summary>GPIO pin.</summary>
            public const int PA5 = 5;
            /// <summary>GPIO pin.</summary>
            public const int PA6 = 6;
            /// <summary>GPIO pin.</summary>
            public const int PA7 = 7;
            /// <summary>GPIO pin.</summary>
            public const int PA8 = 8;
            /// <summary>GPIO pin.</summary>
            public const int PA9 = 9;
            /// <summary>GPIO pin.</summary>
            public const int PA10 = 10;
            /// <summary>GPIO pin.</summary>
            public const int PA11 = 11;
            /// <summary>GPIO pin.</summary>
            public const int PA12 = 12;
            /// <summary>GPIO pin.</summary>
            public const int PA13 = 13;
            /// <summary>GPIO pin.</summary>
            public const int PA14 = 14;
            /// <summary>GPIO pin.</summary>
            public const int PA15 = 15;
            /// <summary>GPIO pin.</summary>
            public const int PB0 = 0 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB1 = 1 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB2 = 2 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB3 = 3 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB4 = 4 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB5 = 5 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB6 = 6 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB7 = 7 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB8 = 8 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB9 = 9 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB10 = 10 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB11 = 11 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB12 = 12 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB13 = 13 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB14 = 14 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PB15 = 15 + 16;
            /// <summary>GPIO pin.</summary>
            public const int PC0 = 0 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC1 = 1 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC2 = 2 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC3 = 3 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC4 = 4 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC5 = 5 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC6 = 6 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC7 = 7 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC8 = 8 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC9 = 9 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC10 = 10 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC11 = 11 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC12 = 12 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC13 = 13 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC14 = 14 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PC15 = 15 + 32;
            /// <summary>GPIO pin.</summary>
            public const int PD0 = 0 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD1 = 1 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD2 = 2 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD3 = 3 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD4 = 4 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD5 = 5 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD6 = 6 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD7 = 7 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD8 = 8 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD9 = 9 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD10 = 10 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD11 = 11 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD12 = 12 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD13 = 13 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD14 = 14 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PD15 = 15 + 48;
            /// <summary>GPIO pin.</summary>
            public const int PE0 = 0 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE1 = 1 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE2 = 2 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE3 = 3 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE4 = 4 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE5 = 5 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE6 = 6 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE7 = 7 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE8 = 8 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE9 = 9 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE10 = 10 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE11 = 11 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE12 = 12 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE13 = 13 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE14 = 14 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PE15 = 15 + 64;
            /// <summary>GPIO pin.</summary>
            public const int PF0 = 0 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF1 = 1 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF2 = 2 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF3 = 3 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF4 = 4 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF5 = 5 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF6 = 6 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF7 = 7 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF8 = 8 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF9 = 9 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF10 = 10 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF11 = 11 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF12 = 12 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF13 = 13 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF14 = 14 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PF15 = 15 + 80;
            /// <summary>GPIO pin.</summary>
            public const int PG0 = 0 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG1 = 1 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG2 = 2 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG3 = 3 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG4 = 4 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG5 = 5 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG6 = 6 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG7 = 7 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG8 = 8 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG9 = 9 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG10 = 10 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG11 = 11 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG12 = 12 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG13 = 13 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG14 = 14 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PG15 = 15 + 96;
            /// <summary>GPIO pin.</summary>
            public const int PH0 = 0 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH1 = 1 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH2 = 2 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH3 = 3 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH4 = 4 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH5 = 5 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH6 = 6 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH7 = 7 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH8 = 8 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH9 = 9 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH10 = 10 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH11 = 11 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH12 = 12 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH13 = 13 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH14 = 14 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PH15 = 15 + 112;
            /// <summary>GPIO pin.</summary>
            public const int PI0 = 0 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI1 = 1 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI2 = 2 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI3 = 3 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI4 = 4 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI5 = 5 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI6 = 6 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI7 = 7 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI8 = 8 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI9 = 9 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI10 = 10 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI11 = 11 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI12 = 12 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI13 = 13 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI14 = 14 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PI15 = 15 + 128;
            /// <summary>GPIO pin.</summary>
            public const int PJ0 = 0 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ1 = 1 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ2 = 2 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ3 = 3 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ4 = 4 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ5 = 5 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ6 = 6 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ7 = 7 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ8 = 8 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ9 = 9 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ10 = 10 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ11 = 11 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ12 = 12 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ13 = 13 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ14 = 14 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PJ15 = 15 + 144;
            /// <summary>GPIO pin.</summary>
            public const int PK0 = 0 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK1 = 1 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK2 = 2 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK3 = 3 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK4 = 4 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK5 = 5 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK6 = 6 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK7 = 7 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK8 = 8 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK9 = 9 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK10 = 10 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK11 = 11 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK12 = 12 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK13 = 13 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK14 = 14 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PK15 = 15 + 160;
            /// <summary>GPIO pin.</summary>
            public const int PZ0 = 0 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ1 = 1 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ2 = 2 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ3 = 3 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ4 = 4 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ5 = 5 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ6 = 6 + 400;
            /// <summary>GPIO pin.</summary>
            public const int PZ7 = 7 + 400;
            /// <summary>GPIO pin.</summary>
            public const int NONE = -1;

            const uint RCC_MP_AHB4ENSETR = 0x50000A28;

            const uint RCC_MP_AHB5ENSETR = 0x50000210;

            const uint GPIO_BASE_REG = 0x50002000;
            const uint GPIOZ_BASE_REG = 0x54004000;

            const uint GPIO_MODER_REG_OFFSET = 0x00000000;
            const uint GPIO_OTYPER_REG_OFFSET = 0x00000004;
            const uint GPIO_OSPEEDR_REG_OFFSET = 0x00000008;
            const uint GPIO_PUPDR_REG_OFFSET = 0x0000000C;
            const uint GPIO_IDR_REG_OFFSET = 0x00000010;
            const uint GPIO_ODR_REG_OFFSET = 0x00000014;
            const uint GPIO_BSRR_REG_OFFSET = 0x00000018;
            const uint GPIO_LCKR_REG_OFFSET = 0x0000001C;
            const uint GPIO_AFRL_REG_OFFSET = 0x00000020;
            const uint GPIO_AFRH_REG_OFFSET = 0x00000024;
            const uint GPIO_BRR_REG_OFFSET = 0x00000028;
            public static void SetModer(int pin_id, Moder moder) {
                var port_id = pin_id / 16;
                var port_base = GPIO_BASE_REG + port_id * 0x1000;

                pin_id = pin_id % 16;

                if (port_id == 25) // portZ
                {
                    port_base = GPIOZ_BASE_REG;
                    port_id = 0;
                    Register.Write(RCC_MP_AHB5ENSETR, (uint)(1 << (int)port_id));
                }
                else {
                    // enable port clock
                    Register.Write(RCC_MP_AHB4ENSETR, (uint)(1 << (int)port_id));
                }

                var value = Register.Read((uint)port_base + GPIO_MODER_REG_OFFSET);

                var clear = 3 << (pin_id * 2); //3 : 2 bits

                value &= (uint)~clear;

                var set = (uint)moder << (pin_id * 2);

                value |= set;

                Register.Write((uint)port_base + GPIO_MODER_REG_OFFSET, value);
            }

            public static void SetAlternate(int pin_id, Alternate alt) {
                var port_id = pin_id / 16;
                var port_base = GPIO_BASE_REG + port_id * 0x1000;

                pin_id = pin_id % 16;

                if (port_id == 25) // portZ
                 {
                    port_base = GPIOZ_BASE_REG;
                    port_id = 0;
                    Register.Write(RCC_MP_AHB5ENSETR, (uint)(1 << (int)port_id));
                }
                else {
                    // enable port clock
                    Register.Write(RCC_MP_AHB4ENSETR, (uint)(1 << (int)port_id));
                }

                var id = (pin_id > 7) ? pin_id - 8 : pin_id;

                var reg = (pin_id > 7) ? port_base + GPIO_AFRH_REG_OFFSET : port_base + GPIO_AFRL_REG_OFFSET;

                var value = Register.Read((uint)reg);

                var clear = 0x0F << (id * 4); // 0x0F: 4 bit

                value &= (uint)~clear;

                var set = (uint)alt << (id * 4);

                value |= set;

                Register.Write((uint)reg, value);

            }

            public static void SetOutputType(int pin_id, OutputType type) {

                var port_id = pin_id / 16;
                var port_base = GPIO_BASE_REG + port_id * 0x1000;

                pin_id = pin_id % 16;

                if (port_id == 25) // portZ
                {
                    port_base = GPIOZ_BASE_REG;
                    port_id = 0;
                    Register.Write(RCC_MP_AHB5ENSETR, (uint)(1 << (int)port_id));
                }
                else {
                    // enable port clock
                    Register.Write(RCC_MP_AHB4ENSETR, (uint)(1 << (int)port_id));
                }

                var value = Register.Read((uint)port_base + GPIO_OTYPER_REG_OFFSET);

                if (type == OutputType.PushPull) {
                    value &= (uint)~(1 << pin_id);
                }
                else {
                    value |= (uint)(1 << pin_id);
                }

                Register.Write((uint)port_base + GPIO_OTYPER_REG_OFFSET, value);

            }

            public static void SetPull(int pin_id, Pull pull) {
                var port_id = pin_id / 16;
                var port_base = GPIO_BASE_REG + port_id * 0x1000;

                if (port_id == 25) // portZ
                {
                    port_base = GPIOZ_BASE_REG;
                    port_id = 0;
                    Register.Write(RCC_MP_AHB5ENSETR, (uint)(1 << (int)port_id));
                }
                else {
                    // enable port clock
                    Register.Write(RCC_MP_AHB4ENSETR, (uint)(1 << (int)port_id));
                }

                pin_id = pin_id % 16;

                var value = Register.Read((uint)port_base + GPIO_PUPDR_REG_OFFSET);

                var clear = 3 << (pin_id * 2); //3 : 2 bits

                value &= (uint)~(clear); // clear

                var set = (uint)pull << (pin_id * 2);

                value |= set;

                Register.Write((uint)port_base + GPIO_PUPDR_REG_OFFSET, value);
            }

            public static void SetSpeed(int pin_id, Speed speed) {
                var port_id = pin_id / 16;
                var port_base = GPIO_BASE_REG + port_id * 0x1000;

                pin_id = pin_id % 16;

                if (port_id == 25) // portZ
                {
                    port_base = GPIOZ_BASE_REG;
                    port_id = 0;
                    Register.Write(RCC_MP_AHB5ENSETR, (uint)(1 << (int)port_id));
                }
                else {
                    // enable port clock
                    Register.Write(RCC_MP_AHB4ENSETR, (uint)(1 << (int)port_id));
                }

                var value = Register.Read((uint)port_base + GPIO_OSPEEDR_REG_OFFSET);

                var clear = 3 << (pin_id * 2); //3 : 2 bits

                value &= (uint)~(clear); // clear

                var set = (uint)speed << (pin_id * 2);

                value |= set;

                Register.Write((uint)port_base + GPIO_OSPEEDR_REG_OFFSET, value);
            }

            
        }

    }
}
