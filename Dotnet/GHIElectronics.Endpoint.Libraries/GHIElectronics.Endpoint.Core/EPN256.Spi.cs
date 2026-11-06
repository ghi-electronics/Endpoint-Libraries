using System.Diagnostics;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// SPI controllers bundled with EPN256. At boot nothing probes, so
        /// pins stay free GPIO; <see cref="Initialize"/> brings up the bus,
        /// muxes just that bus's pins, and makes <c>/dev/spidev{N}.0</c>
        /// appear for userspace. Pay-for-use: initializing one SPI bus does
        /// not touch any other SPI's pins.
        /// </summary>
        /// <example>
        /// <code>
        /// EPN256.Spi.Initialize(EPN256.Spi.Spi0);
        /// using var device = SpiDevice.Create(new SpiConnectionSettings(EPN256.Spi.Spi0, 0) {
        ///     ClockFrequency = 1_000_000,
        /// });
        /// </code>
        /// </example>
        public static class Spi {
            /// <summary>SPI0: PB8 (SS1), PB9 (CLK), PB10 (MOSI), PB11 (MISO); /dev/spidev0.0. Conflicts with I2C1/I2C2 on PB8-PB11; initialize one or the other, not both.</summary>
            public const int Spi0 = 0;
            /// <summary>SPI1: PC8 (SS0), PC9 (CLK), PC10 (MOSI), PC11 (MISO); /dev/spidev1.0.</summary>
            public const int Spi1 = 1;
            /// <summary>SPI2: PG8 (SS0), PG9 (CLK), PG10 (MOSI), PK4 (MISO); /dev/spidev2.0.</summary>
            public const int Spi2 = 2;
            /// <summary>SPI3: PL8 (SS0), PL9 (CLK), PL4 (MOSI), PL5 (MISO); /dev/spidev3.0.</summary>
            public const int Spi3 = 3;

            /// <summary>Mapping of one SPI bus: name, MMIO base, four pins, and the MFP function
            /// value used to mux them. All four pins on MA35D1 SPI buses share the same MFP value.</summary>
            private record Bus(string Name, ulong Address, int Ss, int Clk, int Mosi, int Miso, int Mfp);

            // Indexed by bus number. MFP values are from
            // include/dt-bindings/pinctrl/ma35d1-pinfunc.h; MMIO addresses from
            // arch/arm64/.../ma35d1.dtsi.
            private static readonly Bus[] Buses = {
                new("SPI0", 0x40600000, Gpio.Pin.PB8,  Gpio.Pin.PB9,  Gpio.Pin.PB10, Gpio.Pin.PB11, 0x5),
                new("SPI1", 0x40610000, Gpio.Pin.PC8,  Gpio.Pin.PC9,  Gpio.Pin.PC10, Gpio.Pin.PC11, 0x5),
                new("SPI2", 0x40620000, Gpio.Pin.PG8,  Gpio.Pin.PG9,  Gpio.Pin.PG10, Gpio.Pin.PK4,  0x5),
                new("SPI3", 0x40630000, Gpio.Pin.PL8,  Gpio.Pin.PL9,  Gpio.Pin.PL4,  Gpio.Pin.PL5,  0x5),
            };

            private const string ControllerModule = "spi-ma35d1-spi";
            // spidev is the userspace chardev glue; needed for /dev/spidev* to appear.
            // DT child nodes use compatible = "rohm,dh2228fv" because kernel 5.x+ removed
            // "linux,spidev" from spidev's of_match_table.
            private const string SpidevModule = "spidev";

            /// <summary>
            /// Brings up the SPI controller + spidev userspace glue, muxes the
            /// bus's four pins (SS / CLK / MOSI / MISO). Only this bus's pins
            /// are claimed; every other SPI bus's pins stay free.
            /// </summary>
            /// <param name="bus"><see cref="Spi0"/>, <see cref="Spi1"/>, <see cref="Spi2"/> or <see cref="Spi3"/>.</param>
            public static void Initialize(int bus) {
                var b = Get(bus);

                // Load the driver modules once per process. Future firmware will build
                // them in (=y) and this becomes a no-op; modprobe is idempotent either way.
                EnsureDriverLoaded();

                var dev = $"/dev/spidev{bus}.0";
                for (int i = 0; i < 50 && !File.Exists(dev); i++) {
                    Thread.Sleep(20);
                }
                if (!File.Exists(dev)) {
                    throw new InvalidOperationException(
                        $"SPI controller loaded but {dev} did not appear within 1 s. " +
                        "Check the firmware's device-tree tweaks for this bus.");
                }

                var connected = PeripheralPins.Connect(b.Name,
                    new PinFunction(b.Ss,   b.Mfp),
                    new PinFunction(b.Clk,  b.Mfp),
                    new PinFunction(b.Mosi, b.Mfp),
                    new PinFunction(b.Miso, b.Mfp));
                if (!connected) {
                    // Already open by this owner; nothing more to do.
                }
            }

            /// <summary>Gives the bus's pins back, as they were before <see cref="Initialize"/>.</summary>
            public static void UnInitialize(int bus) => PeripheralPins.Disconnect(Get(bus).Name);

            private static Bus Get(int bus) =>
                bus is >= Spi0 and <= Spi3
                    ? Buses[bus]
                    : throw new ArgumentOutOfRangeException(nameof(bus), bus,
                        "The SPI buses are Spi0, Spi1, Spi2 and Spi3");

            // =====================================================================
            // Driver load helper. Modules stay loaded for the life of the process;
            // the kernel keeps per-controller bindings, we keep per-bus pin
            // reservations via PeripheralPins.
            // =====================================================================

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) {
                        return;
                    }
                    Modprobe(ControllerModule);
                    Modprobe(SpidevModule);
                    driverLoaded = true;
                }
            }

            private static void Modprobe(string module) {
                var psi = new ProcessStartInfo("/sbin/modprobe", module) {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start /sbin/modprobe {module}");
                p.WaitForExit();
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException(
                        $"modprobe {module} failed: {p.StandardError.ReadToEnd().Trim()}");
                }
            }
        }
    }
}
