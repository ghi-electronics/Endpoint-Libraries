using System.Diagnostics;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN128 {
        /// <summary>
        /// CAN interfaces bundled with EPN128 (MA35D0 Chili Pro). Mirrors
        /// <see cref="EPN256.Can"/>: pay-for-use pins, <see cref="Initialize"/>
        /// mux's TX/RX pins and brings the <c>canN</c> interface up at the
        /// chosen bitrate. All four CAN controllers are bonded out.
        /// </summary>
        /// <example>
        /// <code>
        /// EPN128.Can.Initialize(EPN128.Can.Can0, bitrate: 500_000);
        /// </code>
        /// </example>
        public static class Can {
            /// <summary>CAN0 interface name: PC2 (RXD), PC3 (TXD); /sys/class/net/can0.</summary>
            public const string Can0 = "can0";
            /// <summary>CAN1 interface name: PC6 (RXD), PC7 (TXD); /sys/class/net/can1.</summary>
            public const string Can1 = "can1";
            /// <summary>CAN2 interface name: PA15 (RXD), PG1 (TXD); /sys/class/net/can2.</summary>
            public const string Can2 = "can2";
            /// <summary>CAN3 interface name: PG8 (RXD), PG9 (TXD); /sys/class/net/can3.</summary>
            public const string Can3 = "can3";

            /// <summary>The slowest supported bitrate: 10 kbit/s.</summary>
            public const int MinBitrate = 10_000;
            /// <summary>The fastest supported bitrate: 1 Mbit/s (classic CAN).</summary>
            public const int MaxBitrate = 1_000_000;

            /// <summary>Mapping of one CAN port: net-interface name, MMIO base, pin + MFP per line.</summary>
            private sealed record Port(string Name, ulong Address, int Rxd, int Txd, int RxdMfp, int TxdMfp);

            // MFP values from include/dt-bindings/pinctrl/ma35d1-pinfunc.h;
            // MMIO addresses from arch/arm64/.../ma35d05k.dtsi.
            // Note CAN2 uses MFP 0x5 for both RXD/TXD on PA15+PG1 (only pin pair bonded on Chili Pro).
            private static readonly Dictionary<string, Port> Ports = new() {
                [Can0] = new Port("CAN0", 0x403C0000, Gpio.Pin.PC2,  Gpio.Pin.PC3,  0x3, 0x3),
                [Can1] = new Port("CAN1", 0x403D0000, Gpio.Pin.PC6,  Gpio.Pin.PC7,  0x3, 0x3),
                [Can2] = new Port("CAN2", 0x403E0000, Gpio.Pin.PA15, Gpio.Pin.PG1,  0x5, 0x5),
                [Can3] = new Port("CAN3", 0x403F0000, Gpio.Pin.PG8,  Gpio.Pin.PG9,  0x3, 0x3),
            };

            private const string ModuleName = "m_can_platform";

            public static void Initialize(string port, int bitrate = 500_000) {
                var p = Get(port);
                if (bitrate is < MinBitrate or > MaxBitrate) {
                    throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate,
                        $"CAN bitrate must be {MinBitrate} to {MaxBitrate} bit/s");
                }

                EnsureDriverLoaded();

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

            public static void UnInitialize(string port) {
                if (!Ports.TryGetValue(port, out var p)) return;
                try { Ip($"link set {port} down"); } catch { }
                PeripheralPins.Disconnect(p.Name);
            }

            private static Port Get(string port) =>
                Ports.TryGetValue(port, out var p) ? p
                    : throw new ArgumentOutOfRangeException(nameof(port), port,
                        "Must be EPN128.Can.Can0 / Can1 / Can2 / Can3");

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) return;
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
