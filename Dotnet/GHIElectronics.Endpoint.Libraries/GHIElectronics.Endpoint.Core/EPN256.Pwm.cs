using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// MA35D1 enhanced PWM channels, for System.Device.Pwm. Keeping the
        /// kernel driver (<c>CONFIG_PWM_MA35D1=m</c>) modular means the PWM
        /// controller stays unprobed and its pins stay free GPIO until
        /// <see cref="Initialize"/> loads the driver. Reverse with
        /// <see cref="UnInitialize"/>.
        /// <code>
        /// EPN256.Pwm.Initialize(EPN256.Pwm.Epwm1);
        /// var chipId = EPN256.Pwm.GetChipId(EPN256.Pwm.Epwm1);
        /// using var pwm = System.Device.Pwm.PwmChannel.Create(chipId, 0, 1000, 0.5);
        /// pwm.Start();
        /// </code>
        /// The <c>EPWM_</c> number IS the DT instance number; the sysfs
        /// <c>pwmchipN</c> is probe-order and resolved at Initialize time via
        /// of_node lookup, so customer code never has to deal with it.
        /// </summary>
        public static class Pwm {
            /// <summary>EPWM0 (reserved — not wired on EPN256 reference board).</summary>
            public const int Epwm0 = 0;
            /// <summary>EPWM1 (used by the display backlight on EPN256).</summary>
            public const int Epwm1 = 1;
            /// <summary>EPWM2 (reserved — not wired on EPN256 reference board).</summary>
            public const int Epwm2 = 2;

            private const string ModuleName = "pwm-ma35d1";
            private static readonly string[] PlatformDevices = {
                "40580000.epwm0",
                "40590000.epwm1",
                "405a0000.epwm2",
            };

            private static readonly object gate = new();
            private static readonly Dictionary<int, int> openCount = new();
            private static bool moduleLoaded;

            /// <summary>
            /// Loads the EPWM controller driver so <c>/sys/class/pwm/pwmchipN</c>
            /// for the chosen controller appears. The kernel pinctrl framework
            /// mux's the PWM output pins as part of probe.
            /// </summary>
            public static void Initialize(int controller) {
                ValidateController(controller);
                lock (gate) {
                    if (!moduleLoaded) {
                        Run("/sbin/modprobe", ModuleName);
                        moduleLoaded = true;
                    }
                    openCount[controller] = openCount.TryGetValue(controller, out var n) ? n + 1 : 1;
                }
                // Wait briefly for the pwmchip node that corresponds to this controller.
                for (int i = 0; i < 50; i++) {
                    if (TryGetChipId(controller, out _)) return;
                    Thread.Sleep(20);
                }
                throw new InvalidOperationException(
                    $"pwm-ma35d1 loaded but no pwmchip matched {PlatformDevices[controller]} within 1 s.");
            }

            /// <summary>Reverse of Initialize. When no controller remains open,
            /// the module is unloaded and the pins return to GPIO.</summary>
            public static void UnInitialize(int controller) {
                ValidateController(controller);
                lock (gate) {
                    if (!openCount.TryGetValue(controller, out var n) || n <= 0) return;
                    if (--n == 0) openCount.Remove(controller);
                    else openCount[controller] = n;
                    if (openCount.Count == 0 && moduleLoaded) {
                        try { Run("/sbin/rmmod", ModuleName); } catch { }
                        moduleLoaded = false;
                    }
                }
            }

            /// <summary>
            /// Returns the Linux <c>pwmchipN</c> id for the chosen controller,
            /// which System.Device.Pwm.PwmChannel.Create takes as its first
            /// argument. Only valid after Initialize.
            /// </summary>
            public static int GetChipId(int controller) {
                ValidateController(controller);
                if (!TryGetChipId(controller, out var id)) {
                    throw new InvalidOperationException(
                        $"EPN256.Pwm.Initialize({controller}) must be called first.");
                }
                return id;
            }

            private static bool TryGetChipId(int controller, out int chipId) {
                chipId = -1;
                if (!Directory.Exists("/sys/class/pwm")) return false;
                var expected = PlatformDevices[controller]; // e.g. "40590000.epwm1"
                foreach (var dir in Directory.EnumerateDirectories("/sys/class/pwm")) {
                    // The of_node/name file is the simplest, portable way to
                    // identify which DT node this pwmchip wraps.
                    var ofName = Path.Combine(dir, "device", "of_node", "name");
                    string? name = null;
                    try { if (File.Exists(ofName)) name = File.ReadAllText(ofName).TrimEnd('\0').Trim(); }
                    catch { /* keep scanning */ }
                    if (name != null && expected.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)) {
                        var chipName = Path.GetFileName(dir); // "pwmchip3"
                        if (chipName.StartsWith("pwmchip") &&
                            int.TryParse(chipName.AsSpan("pwmchip".Length), out var n)) {
                            chipId = n;
                            return true;
                        }
                    }
                }
                return false;
            }

            private static void ValidateController(int c) {
                if (c is < 0 or > 2) {
                    throw new ArgumentOutOfRangeException(nameof(c), c,
                        "Must be EPN256.Pwm.Epwm0 / Epwm1 / Epwm2");
                }
            }

            private static void Run(string path, string arg) {
                var psi = new ProcessStartInfo(path, arg) {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start {path}");
                p.WaitForExit();
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException(
                        $"{path} {arg} failed: {p.StandardError.ReadToEnd().Trim()}");
                }
            }
        }
    }
}
