using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN128 {
        /// <summary>
        /// The user-facing serial ports (UARTs) bundled with EPN128
        /// (MA35D0 Chili Pro). The console UART is reserved for the kernel
        /// and not exposed here. Mirrors <see cref="EPN256.SerialPort"/>:
        /// <see cref="Initialize"/> muxes the TX / RX pins (plus RTS / CTS
        /// when flow control is on); <see cref="UnInitialize"/> returns
        /// them to free GPIO.
        /// </summary>
        /// <example>
        /// <code>
        /// EPN128.SerialPort.Initialize(EPN128.SerialPort.Uart4);
        /// using var port = new System.IO.Ports.SerialPort(EPN128.SerialPort.Uart4, 115200);
        /// </code>
        /// </example>
        public static class SerialPort {
            /// <summary>UART4: PI10 (RXD), PI11 (TXD), PI8 (CTS), PI9 (RTS); /dev/ttyS4.</summary>
            public const string Uart4 = "/dev/ttyS4";
            /// <summary>UART5: PG6 (RXD), PG7 (TXD); /dev/ttyS5. No flow-control pins bonded.</summary>
            public const string Uart5 = "/dev/ttyS5";
            /// <summary>UART7: PL14 (RXD), PG0 (TXD); /dev/ttyS7. No flow-control pins bonded.</summary>
            public const string Uart7 = "/dev/ttyS7";
            /// <summary>UART9: PG2 (RXD), PG3 (TXD); /dev/ttyS9. No flow-control pins bonded.</summary>
            public const string Uart9 = "/dev/ttyS9";
            /// <summary>UART13: PK4 (RXD), PG10 (TXD); /dev/ttyS13. No flow-control pins bonded.</summary>
            public const string Uart13 = "/dev/ttyS13";

            /// <summary>Mapping of one UART: device path, MMIO base, pin + MFP per line. Rts/Cts = -1 means that signal isn't bonded on Chili Pro.</summary>
            private sealed record Port(string Device, ulong Address,
                int Rxd, int Txd, int Rts, int Cts, int RxdMfp, int TxdMfp, int RtsMfp, int CtsMfp);

            // MFP values from include/dt-bindings/pinctrl/ma35d1-pinfunc.h;
            // MMIO addresses from arch/arm64/.../ma35d05k.dtsi; pin assignments
            // from the vendor board DTS (ma35d0-iot-ma35d05ki1-v1-256m.dts).
            // UART7 and UART13 route pins through two different MFP values
            // because RXD and TXD share different banks (PL/PG and PK/PG).
            private static readonly Dictionary<string, Port> Ports = new() {
                [Uart4]  = new(Uart4,  0x40740000, Gpio.Pin.PI10, Gpio.Pin.PI11, Gpio.Pin.PI9, Gpio.Pin.PI8, 0x2, 0x2, 0x2, 0x2),
                [Uart5]  = new(Uart5,  0x40750000, Gpio.Pin.PG6,  Gpio.Pin.PG7,  -1,           -1,           0x2, 0x2, 0,   0),
                [Uart7]  = new(Uart7,  0x40770000, Gpio.Pin.PL14, Gpio.Pin.PG0,  -1,           -1,           0x2, 0x2, 0,   0),
                [Uart9]  = new(Uart9,  0x40790000, Gpio.Pin.PG2,  Gpio.Pin.PG3,  -1,           -1,           0x2, 0x2, 0,   0),
                [Uart13] = new(Uart13, 0x407D0000, Gpio.Pin.PK4,  Gpio.Pin.PG10, -1,           -1,           0x3, 0x3, 0,   0),
            };

            public static void Initialize(string device, bool enableHardwareFlowControl = false) {
                var p = Get(device);

                var name = $"UART{p.Device.AsSpan("/dev/ttyS".Length).ToString()}";
                var pins = new List<PinFunction>(4) {
                    new PinFunction(p.Rxd, p.RxdMfp),
                    new PinFunction(p.Txd, p.TxdMfp),
                };
                if (enableHardwareFlowControl) {
                    if (p.Rts >= 0) pins.Add(new PinFunction(p.Rts, p.RtsMfp));
                    if (p.Cts >= 0) pins.Add(new PinFunction(p.Cts, p.CtsMfp));
                }

                if (!File.Exists(device)) {
                    throw new InvalidOperationException(
                        $"{device} does not exist: this firmware is missing uart{p.Device.AsSpan("/dev/ttyS".Length).ToString()}.");
                }

                PeripheralPins.Connect(name, pins.ToArray());
            }

            public static void UnInitialize(string device) {
                if (!Ports.ContainsKey(device)) return;
                var name = $"UART{device.AsSpan("/dev/ttyS".Length).ToString()}";
                PeripheralPins.Disconnect(name);
            }

            private static Port Get(string device) =>
                Ports.TryGetValue(device, out var p) ? p
                    : throw new ArgumentOutOfRangeException(nameof(device), device,
                        "Must be one of EPN128.SerialPort.Uart4 / Uart5 / Uart7 / Uart9 / Uart13");
        }
    }
}
