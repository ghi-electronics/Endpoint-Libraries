using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The user-facing serial ports (UARTs), for System.IO.Ports.SerialPort.
        /// Uart0 is reserved for the kernel console and NOT exposed here. The
        /// three ports below are stable thanks to the DT aliases in the firmware
        /// (serial11 = &amp;uart11, etc.), so <c>EPN256.SerialPort.Uart11</c>
        /// is always <c>/dev/ttyS11</c> regardless of probe order.
        /// <code>
        /// EPN256.SerialPort.Initialize(EPN256.SerialPort.Uart11);
        /// using var port = new System.IO.Ports.SerialPort(EPN256.SerialPort.Uart11, 115200);
        /// </code>
        /// Initialize binds the UART to its kernel driver, which re-applies the
        /// pinctrl state and claims the TX / RX pins. UnInitialize unbinds and
        /// returns the pins to free GPIO. Both are safe to call multiple times;
        /// the first Initialize does the work, the matching UnInitialize undoes it.
        /// </summary>
        public static class SerialPort {
            /// <summary>UART11: PB2 (TX), PB3 (RX); /dev/ttyS11</summary>
            public const string Uart11 = "/dev/ttyS11";
            /// <summary>UART12: PK1 (TX), PK0 (RX); /dev/ttyS12</summary>
            public const string Uart12 = "/dev/ttyS12";
            /// <summary>UART14: PK14 (TX), PK15 (RX); /dev/ttyS14</summary>
            public const string Uart14 = "/dev/ttyS14";

            private const string DriverPath = "/sys/bus/platform/drivers/ma35d1-uart";
            private static readonly Dictionary<string, string> DeviceName = new() {
                [Uart11] = "407b0000.serial",
                [Uart12] = "407c0000.serial",
                [Uart14] = "407e0000.serial",
            };

            private static readonly object gate = new();
            private static readonly Dictionary<string, int> openCount = new();

            /// <summary>
            /// Binds the UART to the ma35d1-uart kernel driver so its TX / RX
            /// pins get muxed and <c>/dev/ttyS&lt;N&gt;</c> becomes writable.
            /// </summary>
            /// <param name="device">
            /// One of <see cref="Uart11"/>, <see cref="Uart12"/>, <see cref="Uart14"/>.
            /// </param>
            public static void Initialize(string device) {
                if (!DeviceName.TryGetValue(device, out var platform)) {
                    throw new ArgumentOutOfRangeException(nameof(device), device,
                        "Must be one of EPN256.SerialPort.Uart11 / Uart12 / Uart14");
                }
                lock (gate) {
                    var n = openCount.TryGetValue(device, out var v) ? v : 0;
                    if (n == 0 && !File.Exists($"{DriverPath}/{platform}")) {
                        WriteSysfs($"{DriverPath}/bind", platform);
                    }
                    openCount[device] = n + 1;
                }
                // The kernel re-registers the ttyS device as part of bind; wait
                // briefly for the character device node to show up.
                for (int i = 0; i < 50 && !File.Exists(device); i++) {
                    Thread.Sleep(20);
                }
                if (!File.Exists(device)) {
                    throw new InvalidOperationException(
                        $"Bound {platform} but {device} did not appear within 1 s.");
                }
            }

            /// <summary>
            /// Unbinds the UART from its driver and releases the TX / RX pins
            /// back to free GPIO. Reverses Initialize; call once per Initialize.
            /// </summary>
            public static void UnInitialize(string device) {
                if (!DeviceName.TryGetValue(device, out var platform)) {
                    throw new ArgumentOutOfRangeException(nameof(device), device,
                        "Must be one of EPN256.SerialPort.Uart11 / Uart12 / Uart14");
                }
                lock (gate) {
                    if (!openCount.TryGetValue(device, out var n) || n <= 0) return;
                    if (--n == 0) {
                        openCount.Remove(device);
                        try { WriteSysfs($"{DriverPath}/unbind", platform); } catch { }
                    } else {
                        openCount[device] = n;
                    }
                }
            }

            private static void WriteSysfs(string path, string value) {
                // tee writes with the required root privilege (the agent runs
                // as root on the board); File.WriteAllText fails on sysfs
                // bind / unbind because of the strict single-write semantics.
                var psi = new ProcessStartInfo("/bin/sh", $"-c \"echo {value} > {path}\"") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start shell for {path}");
                p.WaitForExit();
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException(
                        $"Writing '{value}' to {path} failed: {p.StandardError.ReadToEnd().Trim()}");
                }
            }
        }
    }
}
