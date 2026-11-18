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
        /// is accepted for signature parity and otherwise ignored (those
        /// chips use a fixed DMA buffer).
        /// </summary>
        public static void Initialize(int bus, int bufferSize = 2048) =>
            BoardSupport.Current.SpiInitialize(bus, bufferSize);

        /// <summary>Releases the bus's pins back to free GPIO.</summary>
        public static void UnInitialize(int bus) =>
            BoardSupport.Current.SpiUnInitialize(bus);
    }
}
