namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The analog outputs bundled with EPM815. Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Dac</c> package (same API as TinyCLR).
        /// </summary>
        /// <example>
        /// <code>
        /// var dac = DacController.FromName(EPM815.Dac.Id);
        /// using var output = dac.OpenChannel(EPM815.Dac.PA5);
        /// output.WriteValue(0.5);    // half of 3.3 V
        /// </code>
        /// </example>
        public static class Dac {
            /// <summary>The DAC's name, for <c>DacController.FromName</c>.</summary>
            public const string Id = "EPM815.Dac";

            /// <summary>DAC channel 1, on PA4.</summary>
            public const int PA4 = 0;

            /// <summary>DAC channel 2, on PA5.</summary>
            public const int PA5 = 1;

            /// <summary>The number of DAC channels available.</summary>
            internal const int ChannelCount = 2;

            /// <summary>A DAC channel: its pin, its IIO device and the channel there.</summary>
            internal record Output(int Channel, int Pin, string Device, int DeviceChannel);

            internal static readonly Output[] Outputs = {
                new(PA4, Gpio.Pin.PA4, "40017000.dac:dac@1", 1),
                new(PA5, Gpio.Pin.PA5, "40017000.dac:dac@2", 2),
            };

            /// <summary>The output with this channel number, or null</summary>
            internal static Output Find(int channel) => Array.Find(Outputs, o => o.Channel == channel);
        }
    }
}
