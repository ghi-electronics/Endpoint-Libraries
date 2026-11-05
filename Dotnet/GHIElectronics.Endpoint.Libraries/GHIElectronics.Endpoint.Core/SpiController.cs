using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for SPI setup. Call once before using
    /// <see cref="System.Device.Spi.SpiDevice"/>: it brings up the bus
    /// and muxes its pins.
    /// </summary>
    /// <example>
    /// <code>
    /// SpiController.Initialize(EPN256.Spi.Spi0);
    /// using var dev = SpiDevice.Create(new SpiConnectionSettings(EPN256.Spi.Spi0, 0) {
    ///     ClockFrequency = 1_000_000,
    ///     Mode = SpiMode.Mode0,
    /// });
    /// dev.TransferFullDuplex(txBuffer, rxBuffer);
    /// </code>
    /// </example>
    public static class SpiController {
        /// <summary>
        /// Connects the bus's SCK/MISO/MOSI pins. On EPM815, also sizes the
        /// kernel transfer buffer; on EPN256/EPN128 <paramref name="bufferSize"/>
        /// is accepted for signature parity and otherwise ignored (the Nuvoton
        /// driver uses a fixed DMA buffer).
        /// </summary>
        public static void Initialize(int bus, int bufferSize = 2048) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: throw new PlatformNotSupportedException(
                    "EPN128 (MA35D0 Chili Pro) does not bond out SPI pins.");
                case Chip.Epn256: EPN256.Spi.Initialize(bus); break;
                default:          EPM815.Spi.Initialize(bus, bufferSize); break;
            }
        }

        /// <summary>Releases the bus's pins back to free GPIO.</summary>
        public static void UnInitialize(int bus) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: return;
                case Chip.Epn256: EPN256.Spi.UnInitialize(bus); break;
                default:          EPM815.Spi.UnInitialize(bus); break;
            }
        }
    }
}
