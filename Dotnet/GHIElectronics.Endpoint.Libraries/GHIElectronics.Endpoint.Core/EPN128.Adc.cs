using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN128 {
        /// <summary>
        /// The analog inputs bundled with EPN128 (MA35D0 Chili Pro). Use with
        /// .NET's <c>System.Device.Analog</c> through the
        /// <c>GHIElectronics.Endpoint.Devices.Adc</c> package. MA35D0 uses the
        /// 4-channel 10-bit ADC (compatible <c>nuvoton,ma35d1-adc</c>); only
        /// CH2 and CH3 are bonded out on Chili Pro.
        /// </summary>
        /// <example>
        /// <code>
        /// using var adc = new AdcController();
        /// using var input = adc.OpenPin(EPN128.Adc.Pin.PB10);
        /// Console.WriteLine($"{input.ReadVoltage().Volts:0.000} V");
        /// </code>
        /// </example>
        public static class Adc {
            /// <summary>The pins with an analog input (ADC0 channel is in the comment).</summary>
            public static class Pin {
                /// <summary>ADC0 channel 2.</summary>
                public const int PB10 = Gpio.Pin.PB10;
                /// <summary>ADC0 channel 3.</summary>
                public const int PB11 = Gpio.Pin.PB11;
            }

            /// <summary>IIO <c>name</c> of the MA35D0 ADC. The ma35_adc driver sets
            /// <c>indio_dev-&gt;name = dev_name(pdev)</c>, i.e. the DT node name.</summary>
            internal const string IioName = "40420000.adc";

            /// <summary>ADC0 pin → channel map. MFP value 0x8 muxes the pin
            /// to <c>ADC0_CHn</c> per include/dt-bindings/pinctrl/ma35d1-pinfunc.h.</summary>
            internal static readonly AnalogInput[] Inputs = {
                new(Pin.PB10, 2, 0x8, true, "PB10", IioName),
                new(Pin.PB11, 3, 0x8, true, "PB11", IioName),
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
