using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for CAN-bus setup. Call once before using
    /// the CAN device package: it muxes the TX/RX pins and brings the
    /// <c>canN</c> network interface up at the chosen bitrate.
    /// </summary>
    /// <example>
    /// <code>
    /// CanController.Initialize(EPN256.Can.Can0, 500_000);
    /// // ... use GHIElectronics.Endpoint.Devices.Can to send/receive frames ...
    /// CanController.UnInitialize(EPN256.Can.Can0);
    /// </code>
    /// </example>
    public static class CanController {
        /// <summary>Muxes the CAN pins and configures the bitrate.</summary>
        /// <param name="port">A CAN port name (for example <c>EPN256.Can.Can0</c>).</param>
        /// <param name="bitrate">Bus bitrate in bits per second (default 500 kbit/s).</param>
        public static void Initialize(string port, int bitrate = 500_000) {
            if (ChipDetect.IsMa35) EPN256.Can.Initialize(port, bitrate);
            else                   EPM815.Can.Initialize(port, bitrate);
        }

        /// <summary>Takes the interface down and releases its pins.</summary>
        public static void UnInitialize(string port) {
            if (ChipDetect.IsMa35) EPN256.Can.UnInitialize(port);
            else                   EPM815.Can.UnInitialize(port);
        }
    }
}
