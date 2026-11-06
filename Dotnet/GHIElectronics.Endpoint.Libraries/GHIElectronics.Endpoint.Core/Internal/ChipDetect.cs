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
                // /proc/device-tree/model carries the board/SoC name verbatim
                // ("Nuvoton MA35D05K-IoT" on Chili Pro, "Nuvoton MA35D1 SoM ..."
                // on EPN256). Use it first because /proc/device-tree/compatible
                // on MA35D0 reports just "nuvoton,ma35d1" — the family string,
                // not the specific chip — so matching compatible alone sends
                // MA35D0 boards down the MA35D1 code path.
                var model = ReadDtNodeText("/proc/device-tree/model");
                if (model is not null) {
                    // "MA35D05K" / "MA35D06K" are MA35D0 variants. "MA35D1" is
                    // MA35D1. Order matters: "MA35D0" matches both "MA35D05K"
                    // and "MA35D06K"; check it first so it wins over a later
                    // "MA35D1" fallback that would also match a D0 name by
                    // accident.
                    if (model.Contains("MA35D0", StringComparison.OrdinalIgnoreCase)) return Chip.Epn128;
                    if (model.Contains("MA35D1", StringComparison.OrdinalIgnoreCase)) return Chip.Epn256;
                    if (model.Contains("STM32MP15", StringComparison.OrdinalIgnoreCase)) return Chip.Epm815;
                }

                // Fallback to compatible for boards whose vendor DTS does not
                // set a distinctive model string.
                var compat = ReadDtNodeText("/proc/device-tree/compatible");
                if (compat is not null) {
                    if (compat.Contains("ma35d0", StringComparison.OrdinalIgnoreCase)) return Chip.Epn128;
                    if (compat.Contains("ma35d1", StringComparison.OrdinalIgnoreCase)) return Chip.Epn256;
                    if (compat.Contains("stm32mp15", StringComparison.OrdinalIgnoreCase)) return Chip.Epm815;
                }
            }
            catch {
                // No device tree (dev machine, container, test host): leave Unknown.
            }
            return Chip.Unknown;
        }

        // Device-tree node files terminate with a NUL byte. Trim it so
        // string.Contains comparisons behave intuitively.
        private static string? ReadDtNodeText(string path) {
            try {
                return File.Exists(path) ? File.ReadAllText(path).TrimEnd('\0') : null;
            }
            catch { return null; }
        }
    }
}
