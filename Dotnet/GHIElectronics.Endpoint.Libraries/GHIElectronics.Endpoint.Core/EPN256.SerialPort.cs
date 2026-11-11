using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The user-facing serial ports (UARTs) bundled with EPN256, for
        /// <c>System.IO.Ports.SerialPort</c>. The console UART is reserved for
        /// the kernel and not exposed here. The three ports below have stable
        /// names, so <see cref="Uart11"/> is always <c>/dev/ttyS11</c>.
        /// <see cref="Initialize"/> muxes the TX / RX pins (plus RTS / CTS
        /// when flow control is on); <see cref="UnInitialize"/> returns them
        /// to free GPIO. Pay-for-use: only the pins of the port you
        /// initialize are claimed.
        /// </summary>
        /// <example>
        /// <code>
        /// SerialPortController.Initialize(EPN256.SerialPort.Uart11);
        /// using var port = new System.IO.Ports.SerialPort(EPN256.SerialPort.Uart11, 115200);
        /// </code>
        /// </example>
        public static class SerialPort {
            /// <summary>UART11: PL2 (RXD), PL3 (TXD), PL0 (CTS), PL1 (RTS); /dev/ttyS11.</summary>
            public const string Uart11 = "/dev/ttyS11";
            /// <summary>UART12: PI2 (RXD), PI3 (TXD), PI1 (RTS); /dev/ttyS12. CTS pin not bonded out.</summary>
            public const string Uart12 = "/dev/ttyS12";
            /// <summary>UART14: PI6 (RXD), PI7 (TXD), PI5 (RTS); /dev/ttyS14. CTS pin not bonded out.</summary>
            public const string Uart14 = "/dev/ttyS14";

            /// <summary>Mapping of one UART: device path, MMIO base, and the pin + MFP for each
            /// line. Cts = -1 means the board does not expose CTS for this port.</summary>
            private record Port(string Device, ulong Address,
                int Rxd, int Txd, int Rts, int Cts, int Mfp);

            // MFP values are from include/dt-bindings/pinctrl/ma35d1-pinfunc.h;
            // MMIO addresses from arch/arm64/.../ma35d1.dtsi; pin assignments
            // from the vendor board DTS (ma35d1-som-256m.dts).
            private static readonly Dictionary<string, Port> Ports = new() {
                [Uart11] = new Port(Uart11, 0x407B0000, Gpio.Pin.PL2, Gpio.Pin.PL3, Gpio.Pin.PL1, Gpio.Pin.PL0, 0x2),
                [Uart12] = new Port(Uart12, 0x407C0000, Gpio.Pin.PI2, Gpio.Pin.PI3, Gpio.Pin.PI1, -1,            0x2),
                [Uart14] = new Port(Uart14, 0x407E0000, Gpio.Pin.PI6, Gpio.Pin.PI7, Gpio.Pin.PI5, -1,            0x2),
            };

            /// <summary>
            /// Muxes the port's TX / RX pins and, if <paramref name="enableHardwareFlowControl"/>
            /// is set, its RTS / CTS pins too (where bonded). After this call
            /// the <c>/dev/ttyS*</c> device is ready for
            /// <c>System.IO.Ports.SerialPort</c>.
            /// </summary>
            /// <param name="device">One of <see cref="Uart11"/>, <see cref="Uart12"/>, <see cref="Uart14"/>.</param>
            /// <param name="enableHardwareFlowControl">Mux RTS / CTS too. UART12 and UART14 have no
            /// CTS pin bonded out; CTS is skipped silently on those.</param>
            public static void Initialize(string device, bool enableHardwareFlowControl = false) {
                var p = Get(device);

                var name = $"UART{p.Device.AsSpan("/dev/ttyS".Length).ToString()}";
                var pins = new List<PinFunction>(4) {
                    new PinFunction(p.Rxd, p.Mfp),
                    new PinFunction(p.Txd, p.Mfp),
                };
                if (enableHardwareFlowControl) {
                    pins.Add(new PinFunction(p.Rts, p.Mfp));
                    if (p.Cts >= 0) {
                        pins.Add(new PinFunction(p.Cts, p.Mfp));
                    }
                }

                // The ma35d1-uart driver is built-in and auto-binds at boot
                // (SERIAL_MA35D1 can't be modular). Phase 7 strips pinctrl-0
                // from the board DTS so the auto-bind no longer mux's pins;
                // /dev/ttySN exists, pins stay free until we connect them.
                if (!File.Exists(device)) {
                    throw new InvalidOperationException(
                        $"{device} does not exist: this firmware is missing uart{p.Device.AsSpan("/dev/ttyS".Length).ToString()}.");
                }

                PeripheralPins.Connect(name, pins.ToArray());
            }

            /// <summary>Returns the port's pins to free GPIO. Reverses <see cref="Initialize"/>.</summary>
            public static void UnInitialize(string device) {
                if (!Ports.ContainsKey(device)) return;
                var name = $"UART{device.AsSpan("/dev/ttyS".Length).ToString()}";
                PeripheralPins.Disconnect(name);
            }

            private static Port Get(string device) =>
                Ports.TryGetValue(device, out var p) ? p
                    : throw new ArgumentOutOfRangeException(nameof(device), device,
                        "Must be one of EPN256.SerialPort.Uart11 / Uart12 / Uart14");
        }
    }
}
