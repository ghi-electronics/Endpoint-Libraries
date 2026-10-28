using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// MA35D1 CAN interfaces (Bosch M_CAN IP). The kernel driver is loaded
        /// on first Initialize; the kernel pinctrl framework mux's the CAN
        /// TX / RX pins at probe. UnInitialize releases them.
        /// <code>
        /// EPN256.Can.Initialize(EPN256.Can.Can0, bitrate: 500_000);
        /// // The interface is up as "can0" and can be used by any Linux
        /// // CAN library (SocketCAN).
        /// </code>
        /// Interface names are stable via the DT aliases (can0 = &amp;can1,
        /// can1 = &amp;can3), so <see cref="Can0"/> is always <c>can0</c>.
        /// </summary>
        public static class Can {
            /// <summary>CAN0: TX, RX pins per the MA35D1 pinctrl_can1 group.</summary>
            public const string Can0 = "can0";
            /// <summary>CAN1: TX, RX pins per the MA35D1 pinctrl_can3 group.</summary>
            public const string Can1 = "can1";

            private const string ModuleName = "m_can_platform";
            private static readonly object gate = new();
            private static readonly Dictionary<string, int> openCount = new();
            private static bool moduleLoaded;

            /// <summary>
            /// Loads the M_CAN driver, probes the chosen interface, sets its
            /// bitrate, and brings it up. After this call the SocketCAN
            /// interface is ready for any linux-can library (e.g.
            /// <c>SocketCANSharp</c>).
            /// </summary>
            public static void Initialize(string iface, int bitrate = 500_000) {
                if (iface != Can0 && iface != Can1) {
                    throw new ArgumentOutOfRangeException(nameof(iface), iface,
                        "Must be EPN256.Can.Can0 or EPN256.Can.Can1");
                }
                if (bitrate is < 10_000 or > 1_000_000) {
                    throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate,
                        "CAN bitrate must be 10 kbit/s to 1 Mbit/s");
                }
                lock (gate) {
                    if (!moduleLoaded) {
                        Run("/sbin/modprobe", ModuleName);
                        moduleLoaded = true;
                    }
                    openCount[iface] = openCount.TryGetValue(iface, out var n) ? n + 1 : 1;
                }
                // Wait for the net interface to appear. /sys/class/net/<iface>
                // is a directory, not a file — Directory.Exists is required.
                for (int i = 0; i < 50 && !Directory.Exists($"/sys/class/net/{iface}"); i++) {
                    Thread.Sleep(20);
                }
                if (!Directory.Exists($"/sys/class/net/{iface}")) {
                    throw new InvalidOperationException(
                        $"m_can_platform loaded but {iface} did not appear within 1 s.");
                }
                Run("/sbin/ip", $"link set {iface} down");
                Run("/sbin/ip", $"link set {iface} type can bitrate {bitrate}");
                Run("/sbin/ip", $"link set {iface} up");
            }

            /// <summary>
            /// Brings the interface down and reverses Initialize. When no CAN
            /// interface remains open, the module is unloaded.
            /// </summary>
            public static void UnInitialize(string iface) {
                if (iface != Can0 && iface != Can1) return;
                lock (gate) {
                    if (!openCount.TryGetValue(iface, out var n) || n <= 0) return;
                    if (--n == 0) {
                        openCount.Remove(iface);
                        try { Run("/sbin/ip", $"link set {iface} down"); } catch { }
                    } else {
                        openCount[iface] = n;
                    }
                    if (openCount.Count == 0 && moduleLoaded) {
                        try { Run("/sbin/rmmod", ModuleName); } catch { }
                        moduleLoaded = false;
                    }
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
