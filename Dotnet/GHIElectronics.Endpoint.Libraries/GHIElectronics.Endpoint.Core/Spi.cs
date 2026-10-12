using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;
using static GHIElectronics.Endpoint.Core.Chips.Stm32Mp1Pins;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The SPI buses, for .NET's System.Device.Spi. Initialize connects the
        /// bus's pins and loads Linux's SPI driver; then use the bus as on any
        /// Linux board:
        /// <code>
        /// EPM815.Spi.Initialize(EPM815.Spi.Spi4);
        /// using var device = SpiDevice.Create(new SpiConnectionSettings(EPM815.Spi.Spi4, 0) { ClockFrequency = 1_000_000 });
        /// </code>
        /// Each constant is the Linux bus number (/dev/spidevN.0), fixed by the
        /// firmware. Chip select is not driven by the bus: use any GPIO pin.
        /// </summary>
        public static class Spi {
            /// <summary>SPI1: PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI); /dev/spidev0.0</summary>
            public const int Spi1 = 0;
            /// <summary>SPI4: PE12 (SCK), PE13 (MISO), PE14 (MOSI); /dev/spidev1.0</summary>
            public const int Spi4 = 1;
            /// <summary>SPI5: PF7 (SCK), PF8 (MISO), PF9 (MOSI); /dev/spidev2.0</summary>
            public const int Spi5 = 2;

            /// <summary>The largest transfer when none is given (Linux's default)</summary>
            public const int DefaultBufferSize = 4096;

            private sealed record Bus(string Name, ulong Address, int Sck, int Miso, int Mosi, int Alternate);

            // Indexed by the constants above
            private static readonly Bus[] Buses = {
                new("SPI1", 0x44004000, Gpio.Pin.PZ0, Gpio.Pin.PZ1, Gpio.Pin.PZ2, 5),
                new("SPI4", 0x44005000, Gpio.Pin.PE12, Gpio.Pin.PE13, Gpio.Pin.PE14, 5),
                new("SPI5", 0x44009000, Gpio.Pin.PF7, Gpio.Pin.PF8, Gpio.Pin.PF9, 5),
            };

            private const string SpidevBufferSize = "/sys/module/spidev/parameters/bufsiz";
            private static readonly object Gate = new();

            /// <summary>Connects the bus's pins and loads Linux's SPI driver (spidev) if needed</summary>
            /// <param name="bus">Spi1, Spi4 or Spi5</param>
            /// <param name="bufferSize">
            /// The largest transfer in bytes. It is set when the driver loads, so
            /// for all buses: the first Initialize after boot decides it.
            /// </param>
            public static void Initialize(int bus, int bufferSize = DefaultBufferSize) {
                var b = Get(bus);
                if (bufferSize < 1) {
                    throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "The buffer size must be at least 1 byte");
                }

                LoadDriver(bufferSize);
                var device = $"spidev{bus}.0";
                if (!LinuxDevices.IsAt("spidev", device, b.Address)) {
                    throw new InvalidOperationException($"/dev/{device} is not {b.Name} in this firmware: update the board's firmware");
                }
                KernelModules.WaitFor($"/dev/{device}");

                // Clock and data out switch fast: medium speed edges, as the
                // ST device trees use for SPI
                PeripheralPins.Connect(b.Name,
                    new PinFunction(b.Sck, b.Alternate, Speed: Speed.Medium),
                    new PinFunction(b.Miso, b.Alternate),
                    new PinFunction(b.Mosi, b.Alternate, Speed: Speed.Medium));
            }

            /// <summary>Gives the bus's pins back, as they were before Initialize</summary>
            public static void UnInitialize(int bus) => PeripheralPins.Disconnect(Get(bus).Name);

            private static void LoadDriver(int bufferSize) {
                lock (Gate) {
                    if (!File.Exists(SpidevBufferSize)) {
                        KernelModules.Load("spidev", bufferSize == DefaultBufferSize ? null : $"bufsiz={bufferSize}");
                    }
                    var loaded = int.Parse(File.ReadAllText(SpidevBufferSize).Trim(), CultureInfo.InvariantCulture);
                    if (loaded < bufferSize) {
                        throw new InvalidOperationException(
                            $"Linux's SPI driver is already loaded for transfers up to {loaded} bytes. " +
                            $"For {bufferSize}, give this size to the first Initialize after boot.");
                    }
                }
            }

            private static Bus Get(int bus) =>
                bus >= 0 && bus < Buses.Length
                    ? Buses[bus]
                    : throw new ArgumentOutOfRangeException(nameof(bus), bus, "The SPI buses are Spi1, Spi4 and Spi5");
        }
    }
}
