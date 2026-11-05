using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The analog inputs bundled with EPM815. Use with .NET's
        /// <c>System.Device.Analog</c> through the
        /// <c>GHIElectronics.Endpoint.Devices.Adc</c> package.
        /// </summary>
        /// <example>
        /// <code>
        /// using var adc = new AdcController();
        /// using var input = adc.OpenPin(EPM815.Adc.Pin.PA0);
        /// Console.WriteLine(input.ReadVoltage().Volts);
        /// </code>
        /// </example>
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

            internal const string Adc1IioName = "48003000.adc:adc@0";
            internal const string Adc2IioName = "48003000.adc:adc@100";

            // Alternate = -1 means put the pin in analog mode; see PeripheralPins.
            internal static readonly AnalogInput[] Inputs = {
                new(Pin.ANA0, 0,  -1, false, "ANA0", Adc1IioName),
                new(Pin.ANA1, 1,  -1, false, "ANA1", Adc1IioName),
                new(Pin.PF11, 2,  -1, true,  "PF11", Adc1IioName),
                new(Pin.PA6,  3,  -1, true,  "PA6",  Adc1IioName),
                new(Pin.PF12, 6,  -1, true,  "PF12", Adc1IioName),
                new(Pin.PB0,  9,  -1, true,  "PB0",  Adc1IioName),
                new(Pin.PC0,  10, -1, true,  "PC0",  Adc1IioName),
                new(Pin.PC3,  13, -1, true,  "PC3",  Adc1IioName),
                new(Pin.PA3,  15, -1, true,  "PA3",  Adc1IioName),
                new(Pin.PA0,  16, -1, true,  "PA0",  Adc1IioName),
                new(Pin.PA4,  18, -1, true,  "PA4",  Adc1IioName),
                new(Pin.PA5,  19, -1, true,  "PA5",  Adc1IioName),
                new(Pin.PF13, 2,  -1, true,  "PF13", Adc2IioName),
                new(Pin.PF14, 6,  -1, true,  "PF14", Adc2IioName),
            };

            internal static AnalogInput? Find(int pin) {
                foreach (var i in Inputs) {
                    if (i.Pin == pin) return i;
                }
                return null;
            }
        }
    }
}
