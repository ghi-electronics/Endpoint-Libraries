using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;
using static GHIElectronics.Endpoint.Core.Chips.Stm32Mp1Pins;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The I2C buses, for .NET's System.Device.I2c. Initialize connects the
        /// bus's pins and sets its speed; then use the bus as on any Linux board:
        /// <code>
        /// EPM815.I2c.Initialize(EPM815.I2c.I2c1);
        /// using var device = I2cDevice.Create(new I2cConnectionSettings(EPM815.I2c.I2c1, 0x50));
        /// </code>
        /// Each constant is the Linux bus number (/dev/i2c-N), fixed by the firmware.
        /// </summary>
        public static class I2c {
            /// <summary>I2C1: PD12 (SCL), PD13 (SDA); /dev/i2c-0</summary>
            public const int I2c1 = 0;
            /// <summary>I2C5: PZ4 (SCL), PZ5 (SDA); /dev/i2c-1</summary>
            public const int I2c5 = 1;
            /// <summary>I2C4: PB6 (SCL), PF15 (SDA); /dev/i2c-2</summary>
            public const int I2c4 = 2;
            /// <summary>I2C6: PD1 (SCL), PD0 (SDA); /dev/i2c-3</summary>
            public const int I2c6 = 3;

            /// <summary>The speed when none is given: 400 kHz (fast mode)</summary>
            public const int DefaultFrequency = 400_000;
            public const int MinFrequency = 10_000;
            /// <summary>1 MHz (fast mode plus)</summary>
            public const int MaxFrequency = 1_000_000;

            private sealed record Bus(string Name, ulong Address, int Scl, int Sda, int SclAlternate, int SdaAlternate);

            // Indexed by the constants above
            private static readonly Bus[] Buses = {
                new("I2C1", 0x40012000, Gpio.Pin.PD12, Gpio.Pin.PD13, 5, 5),
                new("I2C5", 0x40015000, Gpio.Pin.PZ4, Gpio.Pin.PZ5, 4, 4),
                new("I2C4", 0x5C002000, Gpio.Pin.PB6, Gpio.Pin.PF15, 6, 4),
                new("I2C6", 0x5C009000, Gpio.Pin.PD1, Gpio.Pin.PD0, 2, 2),
            };

            /// <summary>
            /// Connects the bus's pins (open drain, with the internal pull-ups on
            /// for boards without their own) and sets its speed, until the next boot
            /// </summary>
            /// <param name="bus">I2c1, I2c4, I2c5 or I2c6</param>
            /// <param name="frequency">The bus speed in Hz, from 10 kHz to 1 MHz</param>
            public static void Initialize(int bus, int frequency = DefaultFrequency) {
                var b = Get(bus);
                if (frequency is < MinFrequency or > MaxFrequency) {
                    throw new ArgumentOutOfRangeException(nameof(frequency), frequency, $"I2C speeds are {MinFrequency} to {MaxFrequency} Hz");
                }
                if (!LinuxDevices.IsAt("i2c-dev", $"i2c-{bus}", b.Address)) {
                    throw new InvalidOperationException($"/dev/i2c-{bus} is not {b.Name} in this firmware: update the board's firmware");
                }

                var connected = PeripheralPins.Connect(b.Name,
                    new PinFunction(b.Scl, b.SclAlternate, Pull.Up, OutputType.OpenDrain),
                    new PinFunction(b.Sda, b.SdaAlternate, Pull.Up, OutputType.OpenDrain));
                try {
                    SetFrequency(b, frequency);
                }
                catch {
                    // A bus that was already initialized stays as it was
                    if (connected) {
                        PeripheralPins.Disconnect(b.Name);
                    }
                    throw;
                }
            }

            /// <summary>Gives the bus's pins back, as they were before Initialize</summary>
            public static void UnInitialize(int bus) => PeripheralPins.Disconnect(Get(bus).Name);

            /// <summary>The bus speed in use, in Hz</summary>
            public static int GetFrequency(int bus) {
                var path = SpeedFile(Get(bus));
                return File.Exists(path)
                    ? int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture)
                    : DefaultFrequency;   // older firmware: the device tree's speed
            }

            private static void SetFrequency(Bus b, int frequency) {
                var path = SpeedFile(b);
                if (File.Exists(path)) {
                    File.WriteAllText(path, frequency.ToString(CultureInfo.InvariantCulture));
                    // The kernel lowers a speed the board's bus cannot reach: the
                    // rise and fall times in its device tree (pull-ups, wiring) are
                    // too slow for it, for example 1 MHz needs a rise time under 120 ns
                    var actual = int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture);
                    if (actual != frequency) {
                        throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                            $"{b.Name} cannot run at {frequency} Hz on this board (its bus rise and fall times are too slow): it runs at {actual} Hz");
                    }
                }
                else if (frequency != DefaultFrequency) {
                    throw new NotSupportedException("This firmware cannot change the I2C speed: update the board's firmware");
                }
            }

            // Added to the kernel's I2C driver by the Endpoint firmware: the
            // kernel computes the timings for the speed, as for the device tree's
            private static string SpeedFile(Bus b) => $"/sys/bus/platform/devices/{b.Address:x}.i2c/clock_frequency";

            private static Bus Get(int bus) =>
                bus >= 0 && bus < Buses.Length
                    ? Buses[bus]
                    : throw new ArgumentOutOfRangeException(nameof(bus), bus, "The I2C buses are I2c1, I2c4, I2c5 and I2c6");
        }
    }
}
