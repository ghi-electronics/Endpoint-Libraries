using System.Diagnostics;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// CAN interfaces bundled with EPN256. <see cref="Initialize"/> claims
        /// a port's TX/RX pins and brings the interface up at the chosen
        /// bitrate; <see cref="UnInitialize"/> releases them. Pay-for-use:
        /// only the pins of the port you initialize are claimed.
        /// </summary>
        /// <example>
        /// <code>
        /// CanController.Initialize(EPN256.Can.Can0, bitrate: 500_000);
        /// // The interface is up as "can0"; use with .NET IoT's CanRaw
        /// // (Iot.Device.SocketCan) as usual.
        /// </code>
        /// </example>
        public static class Can {
            /// <summary>CAN0 interface name: PL14 (RXD), PL15 (TXD); maps to DT can1 via aliases; /sys/class/net/can0.</summary>
            public const string Can0 = "can0";
            /// <summary>CAN1 interface name: PL10 (RXD), PL11 (TXD); maps to DT can3 via aliases; /sys/class/net/can1.</summary>
            public const string Can1 = "can1";

            /// <summary>The slowest supported bitrate: 10 kbit/s.</summary>
            public const int MinBitrate = 10_000;
            /// <summary>The fastest supported bitrate: 1 Mbit/s (classic CAN).</summary>
            public const int MaxBitrate = 1_000_000;

            /// <summary>Mapping of one CAN port: the kernel's net-interface name, its MMIO base,
            /// and the RXD/TXD pin + MFP function value used to mux it on this board.</summary>
            private record Port(string Name, ulong Address, int Rxd, int Txd, int RxdMfp, int TxdMfp);

            // MFP values are from include/dt-bindings/pinctrl/ma35d1-pinfunc.h;
            // MMIO addresses from arch/arm64/.../ma35d1.dtsi.
            // can0 -> DT can1 @ 0x403D0000, PL14/PL15 MFP 0x4.
            // can1 -> DT can3 @ 0x403F0000, PL10/PL11 MFP 0x3.
            private static readonly Dictionary<string, Port> Ports = new() {
                [Can0] = new Port("CAN0", 0x403D0000, Gpio.Pin.PL14, Gpio.Pin.PL15, 0x4, 0x4),
                [Can1] = new Port("CAN1", 0x403F0000, Gpio.Pin.PL10, Gpio.Pin.PL11, 0x3, 0x3),
            };

            private const string ModuleName = "m_can_platform";

            /// <summary>
            /// Claims the chosen CAN port, muxes its two pins, sets the bitrate
            /// and brings <paramref name="port"/> up. After this call the
            /// interface is ready for .NET IoT's <c>CanRaw</c>
            /// (<c>Iot.Device.SocketCan</c>).
            /// </summary>
            public static void Initialize(string port, int bitrate = 500_000) {
                var p = Get(port);
                if (bitrate is < MinBitrate or > MaxBitrate) {
                    throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate,
                        $"CAN bitrate must be {MinBitrate} to {MaxBitrate} bit/s");
                }

                // Current firmware ships m_can_platform as a loadable module. Future
                // firmware will build it in (=y) and this becomes a no-op; modprobe
                // is idempotent so leaving it does no harm either way.
                EnsureDriverLoaded();

                // After modprobe returns, /sys/class/net/canN appears asynchronously.
                // /sys/class/net/<iface> is a directory, so Directory.Exists is required.
                var sysPath = $"/sys/class/net/{port}";
                for (int i = 0; i < 50 && !Directory.Exists(sysPath); i++) {
                    Thread.Sleep(20);
                }
                if (!Directory.Exists(sysPath)) {
                    throw new InvalidOperationException(
                        $"m_can_platform loaded but {port} did not appear within 1 s.");
                }

                var connected = PeripheralPins.Connect(p.Name,
                    new PinFunction(p.Rxd, p.RxdMfp),
                    new PinFunction(p.Txd, p.TxdMfp));
                try {
                    Ip($"link set {port} down");
                    Ip($"link set {port} type can bitrate {bitrate}");
                    Ip($"link set {port} up");
                }
                catch {
                    if (connected) {
                        PeripheralPins.Disconnect(p.Name);
                    }
                    throw;
                }
            }

            /// <summary>Brings the interface down and gives its pins back.</summary>
            public static void UnInitialize(string port) {
                if (!Ports.TryGetValue(port, out var p)) return;
                try { Ip($"link set {port} down"); } catch { }
                PeripheralPins.Disconnect(p.Name);
            }

            private static Port Get(string port) =>
                Ports.TryGetValue(port, out var p) ? p
                    : throw new ArgumentOutOfRangeException(nameof(port), port,
                        "Must be EPN256.Can.Can0 or EPN256.Can.Can1");

            // =====================================================================
            // Driver load + ip helpers. Module stays loaded for the life of the
            // process (we never rmmod); the kernel keeps per-controller bindings,
            // we keep per-port pin reservations via PeripheralPins.
            // =====================================================================

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) {
                        return;
                    }
                    Run("/sbin/modprobe", ModuleName);
                    driverLoaded = true;
                }
            }

            private static void Ip(string arg) => Run("/sbin/ip", arg);

            private static void Run(string path, string arg) {
                var psi = new ProcessStartInfo(path, arg) {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start {path}");
                proc.WaitForExit();
                if (proc.ExitCode != 0) {
                    throw new InvalidOperationException(
                        $"{path} {arg} failed: {proc.StandardError.ReadToEnd().Trim()}");
                }
            }
        }
    }
}
