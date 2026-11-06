using System.Diagnostics;
using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The I2C buses bundled with EPN256. Use with .NET's
        /// <c>System.Device.I2c</c>. <see cref="Initialize"/> connects the
        /// bus's pins; <see cref="UnInitialize"/> releases them. Pay-for-use:
        /// only the pins of the bus you initialize are claimed. Every other
        /// bus's pins stay free for GPIO. Each constant is the bus number for
        /// <c>I2cConnectionSettings</c> (<c>/dev/i2c-N</c>).
        /// </summary>
        /// <example>
        /// <code>
        /// EPN256.I2c.Initialize(EPN256.I2c.I2c5);
        /// using var device = I2cDevice.Create(new I2cConnectionSettings(EPN256.I2c.I2c5, 0x5D));
        /// // ... read / write ...
        /// EPN256.I2c.UnInitialize(EPN256.I2c.I2c5);
        /// </code>
        /// </example>
        public static class I2c {
            /// <summary>I2C1: PB10 (SDA), PB11 (SCL); /dev/i2c-1.</summary>
            public const int I2c1 = 1;
            /// <summary>I2C2: PB8 (SDA), PB9 (SCL); /dev/i2c-2. Hosts the on-SoM audio codec at 0x1A; sharing is fine.</summary>
            public const int I2c2 = 2;
            /// <summary>I2C3: PM14 (SDA), PM15 (SCL); /dev/i2c-3.</summary>
            public const int I2c3 = 3;
            /// <summary>I2C4: PL4 (SDA), PL5 (SCL); /dev/i2c-4.</summary>
            public const int I2c4 = 4;
            /// <summary>I2C5: PJ12 (SDA), PJ13 (SCL); /dev/i2c-5. Default bus for the touch chip on the EPN256 reference panel.</summary>
            public const int I2c5 = 5;

            /// <summary>The speed when none is given: 400 kHz (fast mode)</summary>
            public const int DefaultFrequency = 400_000;
            /// <summary>The slowest supported bus speed: 10 kHz.</summary>
            public const int MinFrequency = 10_000;
            /// <summary>1 MHz (fast mode plus)</summary>
            public const int MaxFrequency = 1_000_000;

            /// <summary>Mapping of one I2C bus: the kernel's device file, its MMIO base, and the
            /// SDA/SCL pin + MFP function value used to mux it on this board.</summary>
            private record Bus(string Name, ulong Address, int Scl, int Sda, int SclMfp, int SdaMfp);

            // Indexed by bus number minus 1 (I2c1..I2c5 → Buses[0..4]).
            // MFP values are from include/dt-bindings/pinctrl/ma35d1-pinfunc.h in the
            // Nuvoton kernel tree; MMIO addresses from arch/arm64/.../ma35d1.dtsi.
            private static readonly Bus[] Buses = {
                new("I2C1", 0x40810000, Gpio.Pin.PB11, Gpio.Pin.PB10, 0xC, 0xC),
                new("I2C2", 0x40820000, Gpio.Pin.PB9,  Gpio.Pin.PB8,  0x4, 0x4),
                new("I2C3", 0x40830000, Gpio.Pin.PM15, Gpio.Pin.PM14, 0x6, 0x6),
                new("I2C4", 0x40840000, Gpio.Pin.PL5,  Gpio.Pin.PL4,  0x4, 0x4),
                new("I2C5", 0x40850000, Gpio.Pin.PJ13, Gpio.Pin.PJ12, 0x4, 0x4),
            };

            /// <summary>
            /// Connects the bus's pins (SDA/SCL with the internal pull-ups on for boards without
            /// their own) and sets its speed, until the next <see cref="UnInitialize"/> or boot.
            /// </summary>
            /// <param name="bus">I2c1, I2c2, I2c3, I2c4 or I2c5.</param>
            /// <param name="frequency">The bus speed in Hz, from 10 kHz to 1 MHz.</param>
            public static void Initialize(int bus, int frequency = DefaultFrequency) {
                var b = Get(bus);
                if (frequency is < MinFrequency or > MaxFrequency) {
                    throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                        $"I2C speeds are {MinFrequency} to {MaxFrequency} Hz");
                }

                // Current firmware ships i2c-ma35d1 as a loadable module. Future firmware
                // will build it in (=y) and this becomes a no-op; modprobe is idempotent
                // so leaving it does no harm either way.
                EnsureDriverLoaded();

                // After modprobe returns, /dev/i2c-N appears asynchronously via devfs.
                // Poll briefly so a quick-fire Initialize does not race it.
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

            /// <summary>Gives the bus's pins back, as they were before <see cref="Initialize"/>.</summary>
            public static void UnInitialize(int bus) => PeripheralPins.Disconnect(Get(bus).Name);

            /// <summary>The bus speed in use, in Hz.</summary>
            public static int GetFrequency(int bus) {
                var path = SpeedFile(Get(bus));
                return File.Exists(path)
                    ? int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture)
                    : DefaultFrequency;  // driver does not expose it: assume DTS default
            }

            private static void SetFrequency(Bus b, int frequency) {
                var path = SpeedFile(b);
                if (File.Exists(path)) {
                    File.WriteAllText(path, frequency.ToString(CultureInfo.InvariantCulture));
                }
                // Else: driver does not expose runtime speed setting on this firmware;
                // bus runs at the DTS-configured clock-frequency. Not a failure.
            }

            private static string SpeedFile(Bus b) =>
                $"/sys/bus/platform/devices/{b.Address:x}.i2c/clock_frequency";

            private static Bus Get(int bus) =>
                bus is >= I2c1 and <= I2c5
                    ? Buses[bus - 1]
                    : throw new ArgumentOutOfRangeException(nameof(bus), bus,
                        "The I2C buses are I2c1, I2c2, I2c3, I2c4 and I2c5");

            // =====================================================================
            // Driver load helper. Module stays loaded for the life of the process
            // (we never rmmod); the kernel keeps per-controller bindings, we keep
            // per-bus pin reservations via PeripheralPins.
            // =====================================================================

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) {
                        return;
                    }
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
