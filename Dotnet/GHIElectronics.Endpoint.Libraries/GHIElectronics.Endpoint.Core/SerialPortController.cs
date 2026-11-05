using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for UART / serial-port setup. Call once before
    /// using <c>System.IO.Ports.SerialPort</c>: it muxes the TX/RX pins (plus
    /// RTS/CTS when flow control is on) and makes <c>/dev/ttyS*</c> usable.
    /// </summary>
    /// <example>
    /// <code>
    /// SerialPortController.Initialize(EPN256.SerialPort.Uart2);
    /// using var port = new SerialPort(EPN256.SerialPort.Uart2, 115200);
    /// port.Open();
    /// port.WriteLine("Hello");
    /// </code>
    /// </example>
    public static class SerialPortController {
        /// <summary>
        /// Muxes the port's pins. When <paramref name="enableHardwareFlowControl"/>
        /// is set, RTS and (where bonded out) CTS are muxed too.
        /// </summary>
        public static void Initialize(string portName, bool enableHardwareFlowControl = false) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: EPN128.SerialPort.Initialize(portName, enableHardwareFlowControl); break;
                case Chip.Epn256: EPN256.SerialPort.Initialize(portName, enableHardwareFlowControl); break;
                default:          EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl); break;
            }
        }

        /// <summary>Releases the port's pins back to free GPIO.</summary>
        public static void UnInitialize(string portName) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: EPN128.SerialPort.UnInitialize(portName); break;
                case Chip.Epn256: EPN256.SerialPort.UnInitialize(portName); break;
                default:          EPM815.SerialPort.UnInitialize(portName); break;
            }
        }
    }
}
