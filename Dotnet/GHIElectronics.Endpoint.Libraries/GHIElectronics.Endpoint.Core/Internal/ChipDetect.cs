namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// The chip the library is running on, read once from
    /// <c>/proc/device-tree/compatible</c> at process startup.
    /// </summary>
    /// <remarks>
    /// Chip-dispatch inside the library (pin muxing, device paths) uses this.
    /// Customer-facing APIs do not expose it — the board identity on the
    /// host side comes from the USB interface name (see Phase 1 of the
    /// library rework).
    /// </remarks>
    internal enum Chip {
        /// <summary>Unknown — fallback for dev machines without a Linux device tree.</summary>
        Unknown = 0,
        /// <summary>STM32MP153 (EPM815 board family). 32-bit ARMv7.</summary>
        Epm815 = 1,
        /// <summary>Nuvoton MA35D1 (EPN256 board family, dual A35). 64-bit ARM.</summary>
        Epn256 = 2,
        /// <summary>Nuvoton MA35D0 (EPN128 board family, single A35, Chili Pro). 64-bit ARM.</summary>
        Epn128 = 3,
    }

    internal static class ChipDetect {
        /// <summary>The detected chip; cached for the life of the process.</summary>
        public static readonly Chip Current = Detect();

        /// <summary>True for any Nuvoton MA35 chip (EPN256 or EPN128).</summary>
        public static bool IsMa35 => Current is Chip.Epn256 or Chip.Epn128;

        /// <summary>True for the STM32MP1 family (EPM815).</summary>
        public static bool IsStm32Mp1 => Current is Chip.Epm815;

        private static Chip Detect() {
            try {
                // /proc/device-tree/compatible is a NUL-separated list of
                // compatible strings, most-specific first. We only need to
                // find a family hint.
                var raw = File.ReadAllText("/proc/device-tree/compatible");
                if (raw.Contains("ma35d0", StringComparison.OrdinalIgnoreCase)) return Chip.Epn128;
                if (raw.Contains("ma35d1", StringComparison.OrdinalIgnoreCase)) return Chip.Epn256;
                if (raw.Contains("stm32mp15", StringComparison.OrdinalIgnoreCase)) return Chip.Epm815;
            }
            catch {
                // No device tree (dev machine, container, test host): leave Unknown.
            }
            return Chip.Unknown;
        }
    }
}
