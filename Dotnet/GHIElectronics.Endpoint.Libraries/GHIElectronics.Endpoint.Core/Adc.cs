namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The analog inputs, for .NET's System.Device.Analog through the
        /// GHIElectronics.Endpoint.Devices.Adc package:
        /// <code>
        /// using var adc = new AdcController();
        /// using var input = adc.OpenPin(EPM815.Adc.Pin.PA0);
        /// Console.WriteLine(input.ReadVoltage().Volts);
        /// </code>
        /// </summary>
        public static class Adc {
            /// <summary>The pins with an analog input (the converter and channel are in the comments)</summary>
            public static class Pin {
                /// <summary>ANA0, a dedicated analog input (ADC1 channel 0)</summary>
                public const int ANA0 = 0x1000;
                /// <summary>ANA1, a dedicated analog input (ADC1 channel 1)</summary>
                public const int ANA1 = 0x1001;
                /// <summary>ADC1 channel 2</summary>
                public const int PF11 = Gpio.Pin.PF11;
                /// <summary>ADC1 channel 3</summary>
                public const int PA6 = Gpio.Pin.PA6;
                /// <summary>ADC1 channel 6</summary>
                public const int PF12 = Gpio.Pin.PF12;
                /// <summary>ADC1 channel 9</summary>
                public const int PB0 = Gpio.Pin.PB0;
                /// <summary>ADC1 channel 10</summary>
                public const int PC0 = Gpio.Pin.PC0;
                /// <summary>ADC1 channel 13</summary>
                public const int PC3 = Gpio.Pin.PC3;
                /// <summary>ADC1 channel 15</summary>
                public const int PA3 = Gpio.Pin.PA3;
                /// <summary>ADC1 channel 16</summary>
                public const int PA0 = Gpio.Pin.PA0;
                /// <summary>ADC1 channel 18</summary>
                public const int PA4 = Gpio.Pin.PA4;
                /// <summary>ADC1 channel 19</summary>
                public const int PA5 = Gpio.Pin.PA5;
                /// <summary>ADC2 channel 2</summary>
                public const int PF13 = Gpio.Pin.PF13;
                /// <summary>ADC2 channel 6</summary>
                public const int PF14 = Gpio.Pin.PF14;
            }

            /// <summary>An analog input: its converter (1 or 2) and channel there</summary>
            internal sealed record Input(int Pin, int Converter, int Channel) {
                /// <summary>Whether the input has its own pin (ANA0 and ANA1 are not GPIO pins)</summary>
                public bool IsGpio => this.Pin < Gpio.Pin.PZ7 + 1;

                public string Name => this.Pin switch {
                    EPM815.Adc.Pin.ANA0 => "ANA0",
                    EPM815.Adc.Pin.ANA1 => "ANA1",
                    _ => Gpio.Name(this.Pin),
                };
            }

            internal static readonly Input[] Inputs = {
                new(Pin.ANA0, 1, 0), new(Pin.ANA1, 1, 1), new(Pin.PF11, 1, 2), new(Pin.PA6, 1, 3),
                new(Pin.PF12, 1, 6), new(Pin.PB0, 1, 9), new(Pin.PC0, 1, 10), new(Pin.PC3, 1, 13),
                new(Pin.PA3, 1, 15), new(Pin.PA0, 1, 16), new(Pin.PA4, 1, 18), new(Pin.PA5, 1, 19),
                new(Pin.PF13, 2, 2), new(Pin.PF14, 2, 6),
            };

            /// <summary>The analog input on <paramref name="pin"/>, or null</summary>
            internal static Input Find(int pin) => Array.Find(Inputs, i => i.Pin == pin);
        }
    }
}
