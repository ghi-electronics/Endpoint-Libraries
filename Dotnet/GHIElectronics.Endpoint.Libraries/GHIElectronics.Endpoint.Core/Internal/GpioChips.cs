using System.Text;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Which /dev/gpiochipN is which port. The kernel names them GPIOA ...
    /// GPIOZ; their numbers depend on the order they started in.
    /// </summary>
    internal static unsafe class GpioChips {
        // GPIO_GET_CHIPINFO_IOCTL: struct gpiochip_info { char name[32]; char label[32]; u32 lines; }
        private const int InfoSize = 68;
        private const nuint GetChipInfo = 0x8044B401;

        private static readonly object Gate = new();
        private static Dictionary<string, int> chips;

        /// <summary>The gpiochip number of the port (A = 0 ... I = 8, Z = 9)</summary>
        public static int Find(int port) {
            var label = "GPIO" + (port == (int)EPM815.Gpio.Port.Z ? 'Z' : (char)('A' + port));
            lock (Gate) {
                chips ??= Scan();
                return chips.TryGetValue(label, out var chip)
                    ? chip
                    : throw new InvalidOperationException($"The kernel has no {label} (no /dev/gpiochip with that label)");
            }
        }

        private static Dictionary<string, int> Scan() {
            var found = new Dictionary<string, int>();
            var info = stackalloc byte[InfoSize];
            foreach (var path in Directory.EnumerateFiles("/dev", "gpiochip*")) {
                if (!int.TryParse(Path.GetFileName(path).AsSpan("gpiochip".Length), out var number)) {
                    continue;
                }
                var fd = Libc.Open(path, Libc.O_RDONLY | Libc.O_CLOEXEC);
                if (fd < 0) {
                    throw Libc.Error($"Cannot open {path}");
                }
                try {
                    if (Libc.Ioctl(fd, GetChipInfo, (nint)info) < 0) {
                        throw Libc.Error($"Cannot read the label of {path}");
                    }
                    var label = new ReadOnlySpan<byte>(info + 32, 32);
                    var end = label.IndexOf((byte)0);
                    found[Encoding.ASCII.GetString(end < 0 ? label : label[..end])] = number;
                }
                finally {
                    Libc.Close(fd);
                }
            }
            return found;
        }
    }
}
