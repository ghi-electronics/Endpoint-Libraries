using System.Globalization;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The ADC channels bundled with EPN256. Call
        /// <see cref="ReadMillivolts"/> on <see cref="Eadc0"/> and the channel
        /// index to read a channel. Channels 0 - 3 of <see cref="Eadc0"/> drive
        /// the resistive touch screen on the reference panel; stay off those
        /// unless the touch subnode has been removed.
        /// </summary>
        /// <example>
        /// <code>
        /// int mv = EPN256.Adc.ReadMillivolts(EPN256.Adc.Eadc0, 2);
        /// </code>
        /// </example>
        public static class Adc {
            /// <summary>ADC bank 0 (<c>eadc0</c>) — 8 channels, 12-bit.</summary>
            public const string Eadc0 = "eadc0";

            private const string IioRoot = "/sys/bus/iio/devices";

            /// <summary>Raw ADC code (0..4095 for the 12-bit <see cref="Eadc0"/>).</summary>
            public static int ReadRaw(string bank, int channel) {
                var iioDir = ResolveIioDir(bank);
                var path = $"{iioDir}/in_voltage{channel}_raw";
                if (!File.Exists(path)) {
                    throw new InvalidOperationException(
                        $"ADC channel {channel} not available on {bank} ({path} missing).");
                }
                return int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture);
            }

            /// <summary>
            /// Reads raw and applies the IIO scale and offset so the result is
            /// millivolts. The reference is 3.3 V over 12 bits, giving roughly
            /// 0.806 mV/count.
            /// </summary>
            public static int ReadMillivolts(string bank, int channel) {
                var iioDir = ResolveIioDir(bank);
                int raw = ReadRaw(bank, channel);
                double scale = TryReadDouble($"{iioDir}/in_voltage_scale")
                               ?? TryReadDouble($"{iioDir}/in_voltage{channel}_scale")
                               ?? (3300.0 / 4096.0);
                double offset = TryReadDouble($"{iioDir}/in_voltage_offset") ?? 0.0;
                return (int)Math.Round((raw + offset) * scale);
            }

            private static string ResolveIioDir(string bank) {
                if (bank != Eadc0) {
                    throw new ArgumentOutOfRangeException(nameof(bank), bank,
                        "Only EPN256.Adc.Eadc0 is supported on this chip.");
                }
                if (!Directory.Exists(IioRoot)) {
                    throw new InvalidOperationException(
                        "IIO subsystem not available: CONFIG_IIO is not enabled.");
                }
                // eadc0's platform device is at 40430000.adc. Match by of_node.
                foreach (var dir in Directory.EnumerateDirectories(IioRoot)) {
                    try {
                        var namePath = Path.Combine(dir, "name");
                        if (!File.Exists(namePath)) continue;
                        var name = File.ReadAllText(namePath).Trim();
                        if (string.Equals(name, "40430000.adc", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "ma35d1-eadc", StringComparison.OrdinalIgnoreCase)) {
                            return dir;
                        }
                    } catch { /* keep scanning */ }
                }
                throw new InvalidOperationException(
                    $"IIO device for {bank} not found under {IioRoot}.");
            }

            private static double? TryReadDouble(string path) {
                try {
                    if (!File.Exists(path)) return null;
                    return double.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture);
                } catch { return null; }
            }
        }
    }
}
