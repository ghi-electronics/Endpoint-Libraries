using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Adc {
    /// <summary>
    /// One of the chip's two converters (ADC1, ADC2) as Linux's ADC driver
    /// shows it. The driver calibrates the converter and takes the samples;
    /// its scale and resolution give the exact voltage.
    /// </summary>
    internal sealed class Converter {
        private static readonly Converter[] Cache = new Converter[3];
        private static readonly object Gate = new();

        /// <summary>The converter's folder, /sys/bus/iio/devices/iio:deviceN</summary>
        public string Folder { get; }

        /// <summary>Millivolts per step</summary>
        public double ScaleMillivolts { get; }

        /// <summary>Added to a reading before scaling (0 for these converters)</summary>
        public int Offset { get; }

        public int Bits { get; }

        public double ReferenceMillivolts => this.ScaleMillivolts * (1L << this.Bits);

        private Converter(int number) {
            var name = number == 1 ? "48003000.adc:adc@0" : "48003000.adc:adc@100";
            this.Folder = LinuxDevices.FindIio(name)
                ?? throw new InvalidOperationException($"ADC{number} is not enabled in this firmware: update the board's firmware");

            this.ScaleMillivolts = double.Parse(File.ReadAllText($"{this.Folder}/in_voltage_scale"), CultureInfo.InvariantCulture);
            var offset = $"{this.Folder}/in_voltage_offset";
            this.Offset = File.Exists(offset) ? int.Parse(File.ReadAllText(offset), CultureInfo.InvariantCulture) : 0;
            this.Bits = ReadBits(this.Folder);
        }

        /// <summary>Converter 1 or 2</summary>
        public static Converter Get(int number) {
            lock (Gate) {
                return Cache[number] ??= new Converter(number);
            }
        }

        // The sample format, for example "le:u16/16>>0": 16 bits
        private static int ReadBits(string folder) {
            var formats = Path.Combine(folder, "scan_elements");
            if (Directory.Exists(formats)) {
                foreach (var file in Directory.EnumerateFiles(formats, "in_voltage*_type")) {
                    var format = File.ReadAllText(file);
                    var slash = format.IndexOf('/');
                    var shift = format.IndexOf(">>", StringComparison.Ordinal);
                    if (slash > 0 && shift > slash &&
                        int.TryParse(format.AsSpan(slash + 1, shift - slash - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var bits)) {
                        return bits;
                    }
                }
            }
            return 16;  // the STM32MP1 converters' resolution unless the device tree lowers it
        }
    }
}
