using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// MA35D1 SPI controllers. Kernel driver is
        /// <c>CONFIG_SPI_MA35D1_SPI=m</c> (modular), DT nodes stay
        /// <c>status = "okay"</c>: at boot nothing probes, so pins stay free
        /// GPIO; Initialize modprobes <c>spi-ma35d1-spi</c> + <c>spidev</c>,
        /// which triggers probe + pinctrl mux and makes <c>/dev/spidev&lt;N&gt;.0</c>
        /// appear for userspace.
        /// <code>
        /// EPN256.Spi.Initialize(EPN256.Spi.Spi0);
        /// using var device = SpiDevice.Create(new SpiConnectionSettings(EPN256.Spi.Spi0, 0) {
        ///     ClockFrequency = 1_000_000,
        /// });
        /// </code>
        /// <para>Pin assignments (from Nuvoton's ma35d1-iot / ma35d1-hmi
        /// reference designs — picked so none collide with the EPN256 bundled
        /// hardware, console UART0, or the resistive touch pins):</para>
        /// </summary>
        public static class Spi {
            /// <summary>SPI0: PB8 (SS1), PB9 (CLK), PB10 (MOSI), PB11 (MISO); /dev/spidev0.0.</summary>
            public const int Spi0 = 0;
            /// <summary>SPI1: PC8 (SS0), PC9 (CLK), PC10 (MOSI), PC11 (MISO); /dev/spidev1.0.</summary>
            public const int Spi1 = 1;
            /// <summary>SPI2: PG8 (SS0), PG9 (CLK), PG10 (MOSI), PK4 (MISO); /dev/spidev2.0.</summary>
            public const int Spi2 = 2;
            /// <summary>SPI3: PL8 (SS0), PL9 (CLK), PL4 (MOSI), PL5 (MISO); /dev/spidev3.0.</summary>
            public const int Spi3 = 3;

            private const string ControllerModule = "spi-ma35d1-spi";
            // spidev is the userspace chardev glue; loaded alongside the
            // controller so /dev/spidev* appears. DT child nodes use
            // compatible = "rohm,dh2228fv" because kernel 5.x+ removed
            // "linux,spidev" from spidev's of_match_table.
            private const string SpidevModule = "spidev";
            private static readonly object gate = new();
            private static readonly Dictionary<int, int> openCount = new();
            private static bool moduleLoaded;

            public static void Initialize(int bus) {
                ValidateBus(bus);
                lock (gate) {
                    if (!moduleLoaded) {
                        Run("/sbin/modprobe", ControllerModule);
                        Run("/sbin/modprobe", SpidevModule);
                        moduleLoaded = true;
                    }
                    openCount[bus] = openCount.TryGetValue(bus, out var n) ? n + 1 : 1;
                }
                var dev = $"/dev/spidev{bus}.0";
                for (int i = 0; i < 50 && !File.Exists(dev); i++) {
                    Thread.Sleep(20);
                }
                if (!File.Exists(dev)) {
                    throw new InvalidOperationException(
                        $"spi-ma35d1 loaded but {dev} did not appear within 1 s. " +
                        "Have you added a pinctrl_spi group to the device tree for your board?");
                }
            }

            public static void UnInitialize(int bus) {
                ValidateBus(bus);
                lock (gate) {
                    if (!openCount.TryGetValue(bus, out var n) || n <= 0) return;
                    if (--n == 0) openCount.Remove(bus);
                    else openCount[bus] = n;
                    if (openCount.Count == 0 && moduleLoaded) {
                        // Unload in reverse order: spidev depends on the controller.
                        try { Run("/sbin/rmmod", SpidevModule); } catch { }
                        try { Run("/sbin/rmmod", ControllerModule); } catch { }
                        moduleLoaded = false;
                    }
                }
            }

            private static void ValidateBus(int b) {
                if (b is < 0 or > 3) {
                    throw new ArgumentOutOfRangeException(nameof(b), b,
                        "Must be EPN256.Spi.Spi0 / Spi1 / Spi2 / Spi3");
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
