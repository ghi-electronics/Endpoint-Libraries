using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The I2C buses, for .NET's System.Device.I2c. Initialize loads the
        /// MA35D1 I2C controller driver (as a module) so <c>/dev/i2c-N</c>
        /// appears and the bus's pins are muxed by the kernel's pinctrl
        /// framework; UnInitialize unloads it so the pins return to free
        /// GPIO. The kernel driver is CONFIG_I2C_MA35=m in the Endpoint
        /// firmware, which keeps I2C controllers unprobed until a userspace
        /// library needs them (pay-for-use rule). Use .NET's I2cDevice as
        /// usual between Initialize and UnInitialize:
        /// <code>
        /// EPN256.I2c.Initialize(EPN256.I2c.I2c5);
        /// using var device = I2cDevice.Create(new I2cConnectionSettings(EPN256.I2c.I2c5, 0x5D));
        /// // ... read / write ...
        /// EPN256.I2c.UnInitialize(EPN256.I2c.I2c5);
        /// </code>
        /// Each constant is the bus number for I2cConnectionSettings (/dev/i2c-N),
        /// matching the Nuvoton SOM DTS's i2cN labels.
        /// </summary>
        public static class I2c {
            /// <summary>I2C1: PB10 (SDA), PB11 (SCL); /dev/i2c-1</summary>
            public const int I2c1 = 1;
            /// <summary>I2C2: PB8 (SDA), PB9 (SCL); /dev/i2c-2</summary>
            public const int I2c2 = 2;
            /// <summary>I2C3: PM14 (SDA), PM15 (SCL); /dev/i2c-3</summary>
            public const int I2c3 = 3;
            /// <summary>I2C4: /dev/i2c-4</summary>
            public const int I2c4 = 4;
            /// <summary>I2C5: PJ12 (SDA), PJ13 (SCL); /dev/i2c-5</summary>
            public const int I2c5 = 5;

            /// <summary>The speed when none is given: 400 kHz (fast mode)</summary>
            public const int DefaultFrequency = 400_000;
            public const int MinFrequency = 10_000;
            /// <summary>1 MHz (fast mode plus)</summary>
            public const int MaxFrequency = 1_000_000;

            private const string ModuleName = "i2c-ma35d1";
            private static readonly object gate = new();
            // Reference count: how many Initialize calls are not yet matched by UnInitialize.
            // The kernel module loads on the first Initialize, unloads on the last UnInitialize.
            private static readonly Dictionary<int, int> openCount = new();
            private static bool moduleLoaded;

            /// <summary>
            /// Loads the MA35D1 I2C controller driver so <c>/dev/i2c-{bus}</c>
            /// appears and the bus's pins are muxed (by the kernel's pinctrl
            /// framework, as part of the driver's probe). May be called more
            /// than once for the same bus; the same number of UnInitialize
            /// calls then releases it.
            /// </summary>
            /// <param name="bus">I2c1, I2c2, I2c3, I2c4 or I2c5</param>
            /// <param name="frequency">The bus speed in Hz, from 10 kHz to 1 MHz.
            /// The MA35D1 I2C controller driver uses the device-tree clock-frequency
            /// (set in the SOM DTS to 100 kHz) by default; a non-default value is
            /// accepted but not yet applied (parked until the sysfs clock_frequency
            /// path is wired up — the controller runs at the DTS speed for now).</param>
            public static void Initialize(int bus, int frequency = DefaultFrequency) {
                ValidateBus(bus);
                if (frequency is < MinFrequency or > MaxFrequency) {
                    throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                        $"I2C speeds are {MinFrequency} to {MaxFrequency} Hz");
                }
                lock (gate) {
                    if (!moduleLoaded) {
                        Run("/sbin/modprobe", ModuleName);
                        moduleLoaded = true;
                    }
                    openCount[bus] = openCount.TryGetValue(bus, out var n) ? n + 1 : 1;
                }
                // The pinctrl framework applies PJ12/PJ13 (for I2C5) as part of the
                // controller's probe; by the time modprobe returns, /dev/i2c-{bus}
                // exists and the pins are muxed. If a specific firmware revision
                // probes asynchronously, poll briefly for the device file.
                var devPath = $"/dev/i2c-{bus}";
                for (int i = 0; i < 50 && !File.Exists(devPath); i++) {
                    Thread.Sleep(20);
                }
                if (!File.Exists(devPath)) {
                    throw new InvalidOperationException(
                        $"i2c-ma35d1 loaded but {devPath} did not appear within 1 s. " +
                        $"The DT may have &i2c{bus} disabled: check the firmware's DTS tweaks.");
                }
            }

            /// <summary>
            /// Reverse of Initialize. When the last outstanding Initialize for
            /// any bus is matched, the i2c-ma35d1 kernel module is unloaded,
            /// which releases the pinctrl mux for every bus and returns their
            /// pins to free GPIO.
            /// </summary>
            public static void UnInitialize(int bus) {
                ValidateBus(bus);
                lock (gate) {
                    if (!openCount.TryGetValue(bus, out var n) || n <= 0) {
                        return;
                    }
                    if (--n == 0) {
                        openCount.Remove(bus);
                    } else {
                        openCount[bus] = n;
                    }
                    if (openCount.Count == 0 && moduleLoaded) {
                        // Best-effort: rmmod can fail if the kernel briefly holds
                        // a reference (e.g. a device file still open). The caller's
                        // I2cDevice.Dispose should run before this.
                        Run("/sbin/rmmod", ModuleName);
                        moduleLoaded = false;
                    }
                }
            }

            private static void ValidateBus(int bus) {
                if (bus is < I2c1 or > I2c5) {
                    throw new ArgumentOutOfRangeException(nameof(bus), bus,
                        "The I2C buses are I2c1, I2c2, I2c3, I2c4 and I2c5");
                }
            }

            private static void Run(string path, string arg) {
                var psi = new ProcessStartInfo(path, arg) {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start {path}");
                p.WaitForExit();
            }
        }
    }
}
