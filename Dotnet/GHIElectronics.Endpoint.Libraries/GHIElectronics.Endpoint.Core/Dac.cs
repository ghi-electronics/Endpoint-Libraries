namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The analog outputs, for the GHIElectronics.Endpoint.Devices.Dac
        /// package (the same API as TinyCLR):
        /// <code>
        /// var dac = DacController.FromName(EPM815.Dac.Id);
        /// using var output = dac.OpenChannel(EPM815.Dac.PA5);
        /// output.WriteValue(0.5);    // half of 3.3 V
        /// </code>
        /// </summary>
        public static class Dac {
            /// <summary>The DAC's name, for DacController.FromName</summary>
            public const string Id = "EPM815.Dac";

            /// <summary>DAC channel 2, on PA5</summary>
            public const int PA5 = 1;

            /// <summary>The number of DAC channels in the chip</summary>
            internal const int ChannelCount = 2;

            /// <summary>A DAC channel: its pin, and its Linux IIO device and channel</summary>
            internal sealed record Output(int Channel, int Pin, string Device, int LinuxChannel);

            // Channel 0 (DAC1 on PA4) is switched off in the firmware's device tree
            internal static readonly Output[] Outputs = {
                new(PA5, Gpio.Pin.PA5, "40017000.dac:dac@2", 2),
            };

            /// <summary>The output with this channel number, or null</summary>
            internal static Output Find(int channel) => Array.Find(Outputs, o => o.Channel == channel);
        }
    }
}
