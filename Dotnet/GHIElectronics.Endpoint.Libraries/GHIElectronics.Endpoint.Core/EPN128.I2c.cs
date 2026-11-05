using System.Diagnostics;
using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN128 {
        /// <summary>
        /// The I2C buses bundled with EPN128 (MA35D0 Chili Pro).
        /// Mirrors <see cref="EPN256.I2c"/>: pay-for-use pins, bus number
        /// matches <c>/dev/i2c-N</c>. Only I2C4 and I2C5 are bonded out on
        /// Chili Pro.
        /// </summary>
        /// <example>
        /// <code>
        /// EPN128.I2c.Initialize(EPN128.I2c.I2c4);
        /// using var device = I2cDevice.Create(new I2cConnectionSettings(EPN128.I2c.I2c4, 0x50));
        /// </code>
        /// </example>
        public static class I2c {
            /// <summary>I2C4: PC0 (SDA), PC1 (SCL); /dev/i2c-4.</summary>
            public const int I2c4 = 4;
            /// <summary>I2C5: PC4 (SDA), PC5 (SCL); /dev/i2c-5.</summary>
            public const int I2c5 = 5;

            /// <summary>The speed when none is given: 400 kHz (fast mode).</summary>
            public const int DefaultFrequency = 400_000;
            /// <summary>The slowest supported bus speed: 10 kHz.</summary>
            public const int MinFrequency = 10_000;
            /// <summary>1 MHz (fast mode plus).</summary>
            public const int MaxFrequency = 1_000_000;

            private sealed record Bus(string Name, ulong Address, int Scl, int Sda, int SclMfp, int SdaMfp);

            // MFP 0x4 for I2C4/I2C5 on MA35D0 Chili Pro.
            // MMIO addresses from arch/arm64/.../ma35d0.dtsi.
            private static readonly Dictionary<int, Bus> Buses = new() {
                [I2c4] = new("I2C4", 0x40840000, Gpio.Pin.PC1, Gpio.Pin.PC0, 0x4, 0x4),
                [I2c5] = new("I2C5", 0x40850000, Gpio.Pin.PC5, Gpio.Pin.PC4, 0x4, 0x4),
            };

            public static void Initialize(int bus, int frequency = DefaultFrequency) {
                var b = Get(bus);
                if (frequency is < MinFrequency or > MaxFrequency) {
                    throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                        $"I2C speeds are {MinFrequency} to {MaxFrequency} Hz");
                }

                EnsureDriverLoaded();

                var devPath = $"/dev/i2c-{bus}";
                for (int i = 0; i < 50 && !File.Exists(devPath); i++) {
                    Thread.Sleep(20);
                }
                if (!EndpointDevices.IsAt("i2c-dev", devPath.AsSpan("/dev/".Length).ToString(), b.Address)) {
                    throw new InvalidOperationException(
                        $"{devPath} is not {b.Name} in this firmware: update the board's firmware");
                }

                var connected = PeripheralPins.Connect(b.Name,
                    new PinFunction(b.Scl, b.SclMfp, Pull.Up),
                    new PinFunction(b.Sda, b.SdaMfp, Pull.Up));
                try {
                    SetFrequency(b, frequency);
                }
                catch {
                    if (connected) {
                        PeripheralPins.Disconnect(b.Name);
                    }
                    throw;
                }
            }

            public static void UnInitialize(int bus) => PeripheralPins.Disconnect(Get(bus).Name);

            public static int GetFrequency(int bus) {
                var path = SpeedFile(Get(bus));
                return File.Exists(path)
                    ? int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture)
                    : DefaultFrequency;
            }

            private static void SetFrequency(Bus b, int frequency) {
                var path = SpeedFile(b);
                if (File.Exists(path)) {
                    File.WriteAllText(path, frequency.ToString(CultureInfo.InvariantCulture));
                }
            }

            private static string SpeedFile(Bus b) =>
                $"/sys/bus/platform/devices/{b.Address:x}.i2c/clock_frequency";

            private static Bus Get(int bus) =>
                Buses.TryGetValue(bus, out var b) ? b
                    : throw new ArgumentOutOfRangeException(nameof(bus), bus,
                        "The I2C buses on EPN128 are I2c4 and I2c5");

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) return;
                    var psi = new ProcessStartInfo("/sbin/modprobe", "i2c-ma35d1") {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };
                    using var p = Process.Start(psi)
                        ?? throw new InvalidOperationException("Failed to start /sbin/modprobe");
                    p.WaitForExit();
                    driverLoaded = true;
                }
            }
        }
    }
}
