namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Finds the system's device for a peripheral by the hardware it belongs to: numbers like
    /// pwmchip12 or i2c-1 depend on the order the drivers started in.
    /// </summary>
    internal static class EndpointDevices {
        /// <summary>
        /// Whether /sys/class/<paramref name="cls"/>/<paramref name="name"/> belongs
        /// to the peripheral at <paramref name="address"/> (for example i2c-0 and 0x40012000)
        /// </summary>
        public static bool IsAt(string cls, string name, ulong address) {
            var target = File.ResolveLinkTarget($"/sys/class/{cls}/{name}", returnFinalTarget: true)?.FullName;
            if (target is null) {
                return false;
            }
            // Most subsystems keep the platform device in the path, as in
            // /sys/devices/platform/40630000.spi/spi_master/spi3/spi3.0/spidev/spidev3.0
            if (target.Contains($"/{address:x}.", StringComparison.Ordinal)) {
                return true;
            }
            // The i2c core does not: an adapter lands at /sys/devices/i2c-1, whose
            // name carries the bus number instead of the address. The device tree
            // node is then the only authority, and it hangs off a parent folder.
            for (var folder = target; folder is not null; folder = Path.GetDirectoryName(folder)) {
                // LinkTarget is relative, which is all the comparison needs, and
                // it answers null instead of throwing when there is no of_node.
                var node = new FileInfo(Path.Combine(folder, "of_node")).LinkTarget;
                if (node is not null) {
                    return node.EndsWith($"@{address:x}", StringComparison.Ordinal);
                }
            }
            return false;
        }

        /// <summary>
        /// The number N of /sys/class/<paramref name="cls"/>/<paramref name="prefix"/>N
        /// that belongs to the peripheral at <paramref name="address"/>, or -1
        /// </summary>
        public static int Find(string cls, string prefix, ulong address) {
            var folder = $"/sys/class/{cls}";
            if (!Directory.Exists(folder)) {
                return -1;
            }
            foreach (var path in Directory.EnumerateFileSystemEntries(folder, prefix + "*")) {
                var name = Path.GetFileName(path);
                if (int.TryParse(name.AsSpan(prefix.Length), out var number) && IsAt(cls, name, address)) {
                    return number;
                }
            }
            return -1;
        }

        /// <summary>
        /// The folder of the IIO device (ADC, DAC...) named <paramref name="name"/>,
        /// for example /sys/bus/iio/devices/iio:device0 for "48003000.adc:adc@0", or null
        /// </summary>
        public static string FindIio(string name) {
            const string folder = "/sys/bus/iio/devices";
            if (!Directory.Exists(folder)) {
                return null;
            }
            foreach (var path in Directory.EnumerateFileSystemEntries(folder, "iio:device*")) {
                var nameFile = Path.Combine(path, "name");
                if (File.Exists(nameFile) && File.ReadAllText(nameFile).Trim() == name) {
                    return path;
                }
            }
            return null;
        }
    }
}
