using System.Device.Gpio;
using GHIElectronics.Endpoint.Core.Chips;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// The EPM815 module (STM32MP15x): its pin names and the pin setup the
    /// other libraries use. Pin numbers are port * 16 + pin, with port Z
    /// after port I.
    /// </summary>
    public static partial class EPM815 {
        public static partial class Gpio {
            public enum Alternate : int {
                AF0 = 0, AF1 = 1, AF2 = 2, AF3 = 3, AF4 = 4, AF5 = 5, AF6 = 6, AF7 = 7,
                AF8 = 8, AF9 = 9, AF10 = 10, AF11 = 11, AF12 = 12, AF13 = 13, AF14 = 14, AF15 = 15,
                NONE = -1
            }

            public enum Port : uint { A = 0, B = 1, C = 2, D = 3, E = 4, F = 5, G = 6, H = 7, I = 8, Z = 9 }

            public enum Moder : int { Input = 0, Output = 1, Alternate = 2, Analog = 3, NONE = -1 }

            public enum OutputType : int { PushPull = 0, OpenDrain = 1 }

            public enum Pull : int { None = 0, Up = 1, Down = 2 }

            public enum Speed : int { Low = 0, Medium = 1, High = 2, VeryHigh = 3 }

            public enum Edge : int {
                Rising = PinEventTypes.Rising,
                Falling = PinEventTypes.Falling,
            }

            /// <summary>The EPM815's pins (ports A to I and Z)</summary>
            public static class Pin {
                public const int PA0 = 0; public const int PA1 = 1; public const int PA2 = 2; public const int PA3 = 3; public const int PA4 = 4; public const int PA5 = 5; public const int PA6 = 6; public const int PA7 = 7;
                public const int PA8 = 8; public const int PA9 = 9; public const int PA10 = 10; public const int PA11 = 11; public const int PA12 = 12; public const int PA13 = 13; public const int PA14 = 14; public const int PA15 = 15;
                public const int PB0 = 16; public const int PB1 = 17; public const int PB2 = 18; public const int PB3 = 19; public const int PB4 = 20; public const int PB5 = 21; public const int PB6 = 22; public const int PB7 = 23;
                public const int PB8 = 24; public const int PB9 = 25; public const int PB10 = 26; public const int PB11 = 27; public const int PB12 = 28; public const int PB13 = 29; public const int PB14 = 30; public const int PB15 = 31;
                public const int PC0 = 32; public const int PC1 = 33; public const int PC2 = 34; public const int PC3 = 35; public const int PC4 = 36; public const int PC5 = 37; public const int PC6 = 38; public const int PC7 = 39;
                public const int PC8 = 40; public const int PC9 = 41; public const int PC10 = 42; public const int PC11 = 43; public const int PC12 = 44; public const int PC13 = 45; public const int PC14 = 46; public const int PC15 = 47;
                public const int PD0 = 48; public const int PD1 = 49; public const int PD2 = 50; public const int PD3 = 51; public const int PD4 = 52; public const int PD5 = 53; public const int PD6 = 54; public const int PD7 = 55;
                public const int PD8 = 56; public const int PD9 = 57; public const int PD10 = 58; public const int PD11 = 59; public const int PD12 = 60; public const int PD13 = 61; public const int PD14 = 62; public const int PD15 = 63;
                public const int PE0 = 64; public const int PE1 = 65; public const int PE2 = 66; public const int PE3 = 67; public const int PE4 = 68; public const int PE5 = 69; public const int PE6 = 70; public const int PE7 = 71;
                public const int PE8 = 72; public const int PE9 = 73; public const int PE10 = 74; public const int PE11 = 75; public const int PE12 = 76; public const int PE13 = 77; public const int PE14 = 78; public const int PE15 = 79;
                public const int PF0 = 80; public const int PF1 = 81; public const int PF2 = 82; public const int PF3 = 83; public const int PF4 = 84; public const int PF5 = 85; public const int PF6 = 86; public const int PF7 = 87;
                public const int PF8 = 88; public const int PF9 = 89; public const int PF10 = 90; public const int PF11 = 91; public const int PF12 = 92; public const int PF13 = 93; public const int PF14 = 94; public const int PF15 = 95;
                public const int PG0 = 96; public const int PG1 = 97; public const int PG2 = 98; public const int PG3 = 99; public const int PG4 = 100; public const int PG5 = 101; public const int PG6 = 102; public const int PG7 = 103;
                public const int PG8 = 104; public const int PG9 = 105; public const int PG10 = 106; public const int PG11 = 107; public const int PG12 = 108; public const int PG13 = 109; public const int PG14 = 110; public const int PG15 = 111;
                public const int PH0 = 112; public const int PH1 = 113; public const int PH2 = 114; public const int PH3 = 115; public const int PH4 = 116; public const int PH5 = 117; public const int PH6 = 118; public const int PH7 = 119;
                public const int PH8 = 120; public const int PH9 = 121; public const int PH10 = 122; public const int PH11 = 123; public const int PH12 = 124; public const int PH13 = 125; public const int PH14 = 126; public const int PH15 = 127;
                public const int PI0 = 128; public const int PI1 = 129; public const int PI2 = 130; public const int PI3 = 131; public const int PI4 = 132; public const int PI5 = 133; public const int PI6 = 134; public const int PI7 = 135;
                public const int PI8 = 136; public const int PI9 = 137; public const int PI10 = 138; public const int PI11 = 139; public const int PI12 = 140; public const int PI13 = 141; public const int PI14 = 142; public const int PI15 = 143;
                public const int PZ0 = 144; public const int PZ1 = 145; public const int PZ2 = 146; public const int PZ3 = 147; public const int PZ4 = 148; public const int PZ5 = 149; public const int PZ6 = 150; public const int PZ7 = 151;

                /// <summary>No pin</summary>
                public const int NONE = -1;
            }

            public static void SetModer(int pin, Moder moder) {
                if (moder == Moder.NONE) {
                    throw new ArgumentOutOfRangeException(nameof(moder));
                }
                var (port, line) = ChipPin(pin);
                Stm32Mp1Pins.SetMode(port, line, (Stm32Mp1Pins.Mode)moder);
            }

            public static void SetAlternate(int pin, Alternate alternate) {
                if (alternate == Alternate.NONE) {
                    throw new ArgumentOutOfRangeException(nameof(alternate));
                }
                var (port, line) = ChipPin(pin);
                Stm32Mp1Pins.SetAlternate(port, line, (int)alternate);
            }

            public static void SetOutputType(int pin, OutputType type) {
                var (port, line) = ChipPin(pin);
                Stm32Mp1Pins.SetOutputType(port, line, (Stm32Mp1Pins.OutputType)type);
            }

            public static void SetPull(int pin, Pull pull) {
                var (port, line) = ChipPin(pin);
                Stm32Mp1Pins.SetPull(port, line, (Stm32Mp1Pins.Pull)pull);
            }

            public static void SetSpeed(int pin, Speed speed) {
                var (port, line) = ChipPin(pin);
                Stm32Mp1Pins.SetSpeed(port, line, (Stm32Mp1Pins.Speed)speed);
            }

            /// <summary>Marks the pin as used by the app, so the libraries do not use it too</summary>
            public static void PinReserve(int pin) => PinReservations.Reserve("the application", Check(pin));

            public static void PinRelease(int pin) => PinReservations.Release(Check(pin));

            public static bool IsPinReserved(int pin) => PinReservations.IsReserved(Check(pin));

            public static int GetPort(int pin) => Check(pin) / 16;

            public static int GetPin(int pin) => Check(pin) % 16;

            /// <summary>The pin's name, for example "PC0"</summary>
            public static string Name(int pin) =>
                IsValid(pin) ? $"P{(pin / 16 == (int)Port.Z ? 'Z' : (char)('A' + pin / 16))}{pin % 16}" : $"#{pin}";

            internal static bool IsValid(int pin) => pin >= 0 && (pin / 16 < (int)Port.Z || pin <= Pin.PZ7);

            internal static int Check(int pin) {
                if (!IsValid(pin)) {
                    throw new ArgumentOutOfRangeException(nameof(pin), pin, "Not an EPM815 pin");
                }
                return pin;
            }

            // The chip's port (A = 0 ... I = 8, Z) and line
            internal static (int Port, int Line) ChipPin(int pin) {
                Check(pin);
                var port = pin / 16 == (int)Port.Z ? Stm32Mp1Pins.PortZ : pin / 16;
                return (port, pin % 16);
            }
        }
    }
}
