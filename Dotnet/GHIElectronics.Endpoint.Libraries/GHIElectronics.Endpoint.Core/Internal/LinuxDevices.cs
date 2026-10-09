namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Finds Linux devices by the hardware they belong to: numbers like
    /// pwmchip12 or i2c-1 depend on the order the drivers started in.
    /// </summary>
    internal static class LinuxDevices {
        /// <summary>
        /// Whether /sys/class/<paramref name="cls"/>/<paramref name="name"/> belongs
        /// to the peripheral at <paramref name="address"/> (for example i2c-0 and 0x40012000)
        /// </summary>
        public static bool IsAt(string cls, string name, ulong address) {
            var target = File.ResolveLinkTarget($"/sys/class/{cls}/{name}", returnFinalTarget: false)?.FullName;
            return target is not null && target.Contains($"/{address:x}.", StringComparison.Ordinal);
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
    }
}
