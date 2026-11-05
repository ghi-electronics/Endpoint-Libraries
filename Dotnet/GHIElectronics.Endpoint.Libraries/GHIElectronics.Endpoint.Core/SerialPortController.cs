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
        /// Muxes the port's pins. On EPM815 <paramref name="enableHardwareFlowControl"/>
        /// also connects RTS/CTS; on EPN256/EPN128 it is accepted for signature
        /// parity but ignored (hardware flow control requires a per-port DTS
        /// override that the current firmware does not ship).
        /// </summary>
        public static void Initialize(string portName, bool enableHardwareFlowControl = false) {
            if (ChipDetect.IsMa35) EPN256.SerialPort.Initialize(portName);
            else                   EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl);
        }

        /// <summary>Releases the port's pins back to free GPIO.</summary>
        public static void UnInitialize(string portName) {
            if (ChipDetect.IsMa35) EPN256.SerialPort.UnInitialize(portName);
            else                   EPM815.SerialPort.UnInitialize(portName);
        }
    }
}
