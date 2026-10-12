using System.Globalization;
using GHIElectronics.Endpoint.Core.Internal;
using static GHIElectronics.Endpoint.Core.Chips.Stm32Mp1Pins;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The CAN ports, for .NET IoT's CanRaw (Iot.Device.SocketCan, which the
        /// GHIElectronics.Endpoint.Devices.Can package brings). Initialize
        /// connects the port's pins, sets its bitrate and starts it; then use
        /// CanRaw as usual:
        /// <code>
        /// EPM815.Can.Initialize(EPM815.Can.Can1, 500_000);
        /// using var can = new CanRaw(EPM815.Can.Can1);
        /// can.WriteFrame(new byte[] { 1, 2, 3 }, new CanId { Standard = 0x123 });
        /// </code>
        /// Each constant is the port's name for CanRaw, fixed by the firmware.
        /// A port that goes bus-off after too many errors restarts by itself after 100 ms.
        /// </summary>
        public static class Can {
            /// <summary>FDCAN1: PD1 (TX), PD0 (RX)</summary>
            public const string Can1 = "can0";
            /// <summary>FDCAN2: PB13 (TX), PB12 (RX)</summary>
            public const string Can2 = "can1";

            /// <summary>The highest bitrate (1 Mbit/s)</summary>
            public const int MaxBitrate = 1_000_000;

            private sealed record Port(string Name, string Interface, ulong Address, int Tx, int Rx);

            private static readonly Port[] Ports = {
                new("CAN1", Can1, 0x4400E000, Gpio.Pin.PD1, Gpio.Pin.PD0),
                new("CAN2", Can2, 0x4400F000, Gpio.Pin.PB13, Gpio.Pin.PB12),
            };

            private const int Alternate = 9;

            /// <summary>Connects the port's pins, sets its bitrate and starts it</summary>
            /// <param name="port">Can1 or Can2</param>
            /// <param name="bitrate">Bits per second, up to 1,000,000; all devices on the bus must use the same</param>
            public static void Initialize(string port, int bitrate) {
                var p = Get(port);
                if (bitrate <= 0 || bitrate > MaxBitrate) {
                    throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate, $"The bitrate must be more than 0 and at most {MaxBitrate:N0}");
                }
                if (!EndpointDevices.IsAt("net", p.Interface, p.Address)) {
                    throw new InvalidOperationException($"{p.Interface} is not {p.Name} in this firmware: update the board's firmware");
                }

                // TX switches fast: medium speed edges, as ST's device trees use for CAN
                var connected = PeripheralPins.Connect(p.Name,
                    new PinFunction(p.Tx, Alternate, Speed: Speed.Medium),
                    new PinFunction(p.Rx, Alternate));
                try {
                    // The bitrate can only change while the port is stopped. The
                    // modes are named too: a mode another program switched on
                    // (loopback, listen-only, FD) would otherwise stay on.
                    var what = $"Cannot start {p.Name} at {bitrate} bit/s";
                    SystemCommands.Run("ip", what, "link", "set", p.Interface, "down");
                    SystemCommands.Run("ip", what, "link", "set", p.Interface, "type", "can",
                        "bitrate", bitrate.ToString(CultureInfo.InvariantCulture), "restart-ms", "100",
                        "loopback", "off", "listen-only", "off", "fd", "off");
                    SystemCommands.Run("ip", what, "link", "set", p.Interface, "up");
                }
                catch {
                    if (connected) {
                        PeripheralPins.Disconnect(p.Name);
                    }
                    throw;
                }
            }

            /// <summary>Stops the port and gives its pins back, as they were before Initialize</summary>
            public static void UnInitialize(string port) {
                var p = Get(port);
                SystemCommands.Run("ip", $"Cannot stop {p.Name}", "link", "set", p.Interface, "down");
                PeripheralPins.Disconnect(p.Name);
            }

            private static Port Get(string port) =>
                Array.Find(Ports, p => p.Interface == port)
                ?? throw new ArgumentException($"There is no CAN port \"{port}\": use EPM815.Can.Can1 or Can2", nameof(port));
        }
    }
}
