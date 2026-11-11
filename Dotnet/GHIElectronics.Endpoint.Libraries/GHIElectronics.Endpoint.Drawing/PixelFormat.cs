namespace GHIElectronics.Endpoint.Drawing {
    /// <summary>
    /// Pixel layout of a <see cref="Canvas"/> buffer. Also the layout
    /// written to the destination device (on-board panel, SPI TFT,
    /// I2C OLED, …) when the <see cref="Canvas.Buffer"/> is handed
    /// to it.
    /// </summary>
    public enum PixelFormat {
        /// <summary>1 bit per pixel, packed MSB-first in each byte. Typical for SSD1306 / SH1106 OLEDs.</summary>
        Monochrome = 1,

        /// <summary>16 bits per pixel, 5-6-5 big-endian. Native format of the on-board panel and most SPI TFTs (ILI9341, ST7735).</summary>
        Rgb565 = 2,

        /// <summary>24 bits per pixel, R-G-B byte order. Common for parallel RGB panels at 24 bpp.</summary>
        Rgb888 = 3,

        /// <summary>32 bits per pixel, A-R-G-B byte order. Off-screen compositing; alpha is respected by blit operations.</summary>
        Argb8888 = 4,
    }
}
