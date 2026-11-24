#nullable enable
using System.Diagnostics;
using System.Globalization;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Fork+exec wrapper around <c>/usr/sbin/endpoint-pinmux</c>, the C helper
    /// that writes SoC pinmux registers via <c>/dev/mem</c>. One binary per
    /// chip ships in the Buildroot image; the Pins.* packages call these
    /// methods from their <c>Save</c> / <c>Restore</c> / <c>SetFunction</c>.
    /// Keeps chip-specific register knowledge in one place (the C binary) so
    /// .NET, Python, and C/C++ don't each re-implement it.
    ///
    /// A function value is a chip-specific packed <see cref="uint"/>. See
    /// <c>pinmux.h</c> for each chip's layout. Example (STM32MP1):
    /// <c>[Mode:2][Type:1][Speed:2][Pull:2][AF:4]</c>.
    /// </summary>
    internal static class Pinmux {
        private const string PinmuxPath = "/usr/sbin/endpoint-pinmux";

        /// <summary>Set the pin's function. Throws if the binary exits non-zero.</summary>
        public static void Set(int pin, uint function) =>
            Run("set",
                pin.ToString(CultureInfo.InvariantCulture),
                "0x" + function.ToString("X", CultureInfo.InvariantCulture));

        /// <summary>Read the pin's current function (packed uint32, chip-specific layout).</summary>
        /// <returns>The function value; throws if the pin is unreadable.</returns>
        public static uint Get(int pin) {
            var stdout = RunCapture("get",
                pin.ToString(CultureInfo.InvariantCulture));
            // Output format: "pin=<n> function=0x<hex> level=<0|1>\n"
            const string key = "function=";
            int i = stdout.IndexOf(key, System.StringComparison.Ordinal);
            if (i < 0) throw new System.InvalidOperationException(
                $"endpoint-pinmux get {pin}: unexpected output '{stdout.TrimEnd()}'");
            int start = i + key.Length;
            int end = start;
            while (end < stdout.Length && !char.IsWhiteSpace(stdout[end])) end++;
            var value = stdout.AsSpan(start, end - start);
            // Accept both "0x..." (what the C binary emits) and plain decimal.
            if (value.Length > 2 && value[0] == '0' && (value[1] == 'x' || value[1] == 'X')) {
                return uint.Parse(value[2..], System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return uint.Parse(value, CultureInfo.InvariantCulture);
        }

        private static void Run(params string[] args) {
            var psi = new ProcessStartInfo(PinmuxPath) {
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)
                ?? throw new System.InvalidOperationException($"cannot start {PinmuxPath}");
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) {
                throw new System.InvalidOperationException(
                    $"{PinmuxPath} {string.Join(' ', args)} failed (exit {p.ExitCode}): {err.TrimEnd()}");
            }
        }

        private static string RunCapture(params string[] args) {
            var psi = new ProcessStartInfo(PinmuxPath) {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)
                ?? throw new System.InvalidOperationException($"cannot start {PinmuxPath}");
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) {
                throw new System.InvalidOperationException(
                    $"{PinmuxPath} {string.Join(' ', args)} failed (exit {p.ExitCode}): {stderr.TrimEnd()}");
            }
            return stdout;
        }
    }
}
