using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The analog inputs bundled with EPN256. Use with .NET's
        /// <c>System.Device.Analog</c> through the
        /// <c>GHIElectronics.Endpoint.Devices.Adc</c> package. MA35D1's EADC0
        /// is the general-purpose 12-bit ADC, 8 channels on PB0..PB7 (the
        /// resistive touchscreen uses a separate ADC — see the touch
        /// package — so the full EADC range is free for application code).
        /// </summary>
        /// <example>
        /// <code>
        /// using var adc = new AdcController();
        /// using var input = adc.OpenPin(EPN256.Adc.Pin.PB0);
        /// Console.WriteLine($"{input.ReadVoltage().Volts:0.000} V");
        /// </code>
        /// </example>
        public static class Adc {
            /// <summary>The pins with an analog input (EADC0 channel is in the comment).</summary>
            public static class Pin {
                /// <summary>EADC0 channel 0</summary>
                public const int PB0 = Gpio.Pin.PB0;
                /// <summary>EADC0 channel 1</summary>
                public const int PB1 = Gpio.Pin.PB1;
                /// <summary>EADC0 channel 2</summary>
                public const int PB2 = Gpio.Pin.PB2;
                /// <summary>EADC0 channel 3</summary>
                public const int PB3 = Gpio.Pin.PB3;
                /// <summary>EADC0 channel 4</summary>
                public const int PB4 = Gpio.Pin.PB4;
                /// <summary>EADC0 channel 5</summary>
                public const int PB5 = Gpio.Pin.PB5;
                /// <summary>EADC0 channel 6</summary>
                public const int PB6 = Gpio.Pin.PB6;
                /// <summary>EADC0 channel 7</summary>
                public const int PB7 = Gpio.Pin.PB7;
            }

            /// <summary>IIO <c>name</c> of the MA35D1 EADC driver, see
            /// drivers/iio/adc/ma35d1_eadc.c in the vendor kernel.</summary>
            internal const string IioName = "ma35d1-eadc";

            /// <summary>EADC0 pin → channel map. MFP value 0x8 muxes the pin
            /// to <c>EADC0_CHn</c> per include/dt-bindings/pinctrl/ma35d1-pinfunc.h.</summary>
            internal static readonly AnalogInput[] Inputs = {
                new(Pin.PB0, 0, 0x8, true, "PB0", IioName),
                new(Pin.PB1, 1, 0x8, true, "PB1", IioName),
                new(Pin.PB2, 2, 0x8, true, "PB2", IioName),
                new(Pin.PB3, 3, 0x8, true, "PB3", IioName),
                new(Pin.PB4, 4, 0x8, true, "PB4", IioName),
                new(Pin.PB5, 5, 0x8, true, "PB5", IioName),
                new(Pin.PB6, 6, 0x8, true, "PB6", IioName),
                new(Pin.PB7, 7, 0x8, true, "PB7", IioName),
            };

            /// <summary>The analog input on <paramref name="pin"/>, or null.</summary>
            internal static AnalogInput? Find(int pin) {
                foreach (var i in Inputs) {
                    if (i.Pin == pin) return i;
                }
                return null;
            }
        }
    }
}
