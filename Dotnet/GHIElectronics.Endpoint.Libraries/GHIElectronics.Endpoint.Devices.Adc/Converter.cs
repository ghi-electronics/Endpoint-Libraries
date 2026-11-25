using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Adc {
    /// <summary>
    /// One ADC converter as the Linux IIO subsystem presents it. The driver
    /// calibrates the converter and takes samples; its scale and bit depth
    /// give the exact voltage. One <see cref="Converter"/> instance is cached
    /// per IIO <c>name</c> (for example <c>"48003000.adc:adc@0"</c> on EPM815
    /// or <c>"ma35d1-eadc"</c> on EPN256).
    /// </summary>
    internal sealed class Converter {
        private static readonly Dictionary<string, Converter> Cache = new();
        private static readonly object Gate = new();

        /// <summary>The converter's folder, <c>/sys/bus/iio/devices/iio:deviceN</c></summary>
        public string Folder { get; }

        /// <summary>Millivolts per step.</summary>
        public double ScaleMillivolts { get; }

        /// <summary>Added to a reading before scaling.</summary>
        public int Offset { get; }

        /// <summary>Sample resolution in bits.</summary>
        public int Bits { get; }

        public double ReferenceMillivolts => this.ScaleMillivolts * (1L << this.Bits);

        private Converter(string iioName) {
            this.Folder = EndpointDevices.FindIio(iioName)
                ?? throw new InvalidOperationException(
                    $"IIO device '{iioName}' is not enabled in this firmware: update the board's firmware");

            this.Bits = ReadBits(this.Folder);

            // Scale and offset can be per-device (STM32) or per-channel (some
            // MA35 kernels). Try the device-wide ones first, then the first
            // per-channel entry so either layout works. The MA35D1 EADC driver
            // publishes neither, so the last resort is the supply the converter
            // actually references, which is 3.3 V on every Endpoint board.
            this.ScaleMillivolts = ReadDouble(Path.Combine(this.Folder, "in_voltage_scale"))
                                   ?? FirstPerChannelDouble(this.Folder, "scale")
                                   ?? 3300.0 / (1L << this.Bits);
            this.Offset = (int)(ReadDouble(Path.Combine(this.Folder, "in_voltage_offset"))
                                ?? FirstPerChannelDouble(this.Folder, "offset")
                                ?? 0);
        }

        /// <summary>Returns the cached <see cref="Converter"/> for the given IIO device name.</summary>
        public static Converter Get(string iioName) {
            lock (Gate) {
                if (!Cache.TryGetValue(iioName, out var c)) {
                    Cache[iioName] = c = new Converter(iioName);
                }
                return c;
            }
        }

        // Sample format is "<endian>:<sign><real bits>/<storage bits>>><shift>".
        // The real bits are the resolution: the MA35D1 EADC reports "le:u12/16>>0",
        // 12 meaningful bits padded into a 16-bit word.
        private static int ReadBits(string folder) {
            var formats = Path.Combine(folder, "scan_elements");
            if (Directory.Exists(formats)) {
                foreach (var file in Directory.EnumerateFiles(formats, "in_voltage*_type")) {
                    var format = File.ReadAllText(file).Trim();
                    var sign = format.IndexOf(':') + 1;
                    var slash = format.IndexOf('/');
                    if (sign > 0 && slash > sign + 1 &&
                        int.TryParse(format.AsSpan(sign + 1, slash - sign - 1),
                                     NumberStyles.None, CultureInfo.InvariantCulture, out var bits)) {
                        return bits;
                    }
                }
            }
            // Only reached for a driver that publishes no sample format at all.
            return 16;
        }

        private static double? ReadDouble(string path) {
            try {
                return File.Exists(path)
                    ? double.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture)
                    : null;
            } catch { return null; }
        }

        private static double? FirstPerChannelDouble(string folder, string suffix) {
            try {
                foreach (var f in Directory.EnumerateFiles(folder, $"in_voltage*_{suffix}")) {
                    if (ReadDouble(f) is double v) return v;
                }
            } catch { }
            return null;
        }
    }
}
