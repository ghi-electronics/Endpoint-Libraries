using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The serial ports (UARTs), for .NET's System.IO.Ports. Initialize
        /// connects the port's pins; then use .NET's SerialPort as usual:
        /// <code>
        /// EPM815.SerialPort.Initialize(EPM815.SerialPort.Uart4);
        /// using var port = new System.IO.Ports.SerialPort(EPM815.SerialPort.Uart4, 115200);
        /// port.Open();
        /// </code>
        /// </summary>
        public static class SerialPort {
            /// <summary>UART1: PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS)</summary>
            public const string Uart1 = "/dev/ttySTM0";
            /// <summary>UART2: PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS)</summary>
            public const string Uart2 = "/dev/ttySTM1";
            /// <summary>UART3: PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS)</summary>
            public const string Uart3 = "/dev/ttySTM2";
            /// <summary>UART4: PD1 (TX), PD0 (RX)</summary>
            public const string Uart4 = "/dev/ttySTM3";
            /// <summary>UART5: PB13 (TX), PB12 (RX)</summary>
            public const string Uart5 = "/dev/ttySTM4";
            /// <summary>UART7: PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS)</summary>
            public const string Uart7 = "/dev/ttySTM6";
            /// <summary>UART8: PE1 (TX), PE0 (RX)</summary>
            public const string Uart8 = "/dev/ttySTM7";

            private sealed record Port(string Device, string Name, ulong Address, int Alternate,
                int Tx, int Rx, int Rts = Gpio.Pin.NONE, int Cts = Gpio.Pin.NONE) {
                public bool HasFlowControl => this.Rts != Gpio.Pin.NONE;
            }

            private static readonly Port[] Ports = {
                new(Uart1, "UART1", 0x5C000000, 7, Gpio.Pin.PZ7, Gpio.Pin.PZ6, Gpio.Pin.PA12, Gpio.Pin.PZ3),
                new(Uart2, "UART2", 0x4000E000, 7, Gpio.Pin.PF5, Gpio.Pin.PD6, Gpio.Pin.PD4, Gpio.Pin.PD3),
                new(Uart3, "UART3", 0x4000F000, 7, Gpio.Pin.PD8, Gpio.Pin.PD9, Gpio.Pin.PD12, Gpio.Pin.PD11),
                new(Uart4, "UART4", 0x40010000, 8, Gpio.Pin.PD1, Gpio.Pin.PD0),
                new(Uart5, "UART5", 0x40011000, 14, Gpio.Pin.PB13, Gpio.Pin.PB12),
                new(Uart7, "UART7", 0x40018000, 7, Gpio.Pin.PE8, Gpio.Pin.PF6, Gpio.Pin.PE9, Gpio.Pin.PE10),
                new(Uart8, "UART8", 0x40019000, 8, Gpio.Pin.PE1, Gpio.Pin.PE0),
            };

            private static readonly object Gate = new();
            // Whether each initialized port has flow control
            private static readonly Dictionary<string, bool> FlowControl = new();

            /// <summary>Connects the port's TX and RX pins, and RTS and CTS for hardware flow control</summary>
            /// <param name="portName">Uart1 ... Uart8</param>
            /// <param name="enableHardwareFlowControl">Also connect RTS and CTS (UART1, 2, 3 and 7)</param>
            public static void Initialize(string portName, bool enableHardwareFlowControl = false) {
                var port = Get(portName);
                if (enableHardwareFlowControl && !port.HasFlowControl) {
                    throw new ArgumentException($"{port.Name} has no RTS and CTS pins", nameof(enableHardwareFlowControl));
                }

                lock (Gate) {
                    if (FlowControl.TryGetValue(port.Device, out var current)) {
                        if (current != enableHardwareFlowControl) {
                            throw new InvalidOperationException(
                                $"{port.Name} is already initialized {(current ? "with" : "without")} flow control: call UnInitialize first");
                        }
                        return;
                    }

                    var name = Path.GetFileName(port.Device);
                    if (!EndpointDevices.IsAt("tty", name, port.Address)) {
                        throw new InvalidOperationException($"{port.Device} is not {port.Name} in this firmware: update the board's firmware");
                    }

                    var pins = new List<PinFunction> {
                        new(port.Tx, port.Alternate),
                        new(port.Rx, port.Alternate),
                    };
                    if (enableHardwareFlowControl) {
                        pins.Add(new(port.Rts, port.Alternate));
                        pins.Add(new(port.Cts, port.Alternate));
                    }
                    PeripheralPins.Connect(port.Name, pins.ToArray());
                    FlowControl[port.Device] = enableHardwareFlowControl;
                }
            }

            /// <summary>Gives the port's pins back, as they were before Initialize</summary>
            public static void UnInitialize(string portName) {
                var port = Get(portName);
                lock (Gate) {
                    PeripheralPins.Disconnect(port.Name);
                    FlowControl.Remove(port.Device);
                }
            }

            /// <summary>Gives the port's pins back, as they were before Initialize</summary>
            [Obsolete("The flow control setting is remembered from Initialize: use UnInitialize(portName)")]
            public static void UnInitialize(string portName, bool enabledHardwareFlowControl) => UnInitialize(portName);

            private static Port Get(string portName) =>
                Ports.FirstOrDefault(p => p.Device == portName)
                ?? throw new ArgumentException($"{portName} is not an EPM815 serial port (Uart1 ... Uart8)", nameof(portName));
        }
    }
}
