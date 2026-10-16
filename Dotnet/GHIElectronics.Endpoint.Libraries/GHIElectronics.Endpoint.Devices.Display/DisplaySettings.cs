#nullable enable
namespace GHIElectronics.Endpoint.Devices.Display {
    /// <summary>Polarity of a logic-level control signal.</summary>
    public enum SignalPolarity {
        /// <summary>The signal is asserted when the GPIO level is high.</summary>
        ActiveHigh = 0,
        /// <summary>The signal is asserted when the GPIO level is low.</summary>
        ActiveLow = 1,
    }

    /// <summary>Which edge of the pixel clock the panel latches pixel data on.</summary>
    public enum ClockEdge {
        /// <summary>Panel latches on the rising edge (SoC drives on the falling edge).</summary>
        Rising = 0,
        /// <summary>Panel latches on the falling edge (SoC drives on the rising edge).</summary>
        Falling = 1,
    }

    /// <summary>
    /// Everything in /config/display.json. Read with
    /// <see cref="DisplayController.GetSettings"/> and apply with
    /// <see cref="DisplayController.SetSettings"/>. Only the built-in
    /// Parallel transport has schema here; custom panels live in the
    /// customer's own <see cref="DisplayController"/> subclass.
    /// </summary>
    public sealed class DisplaySettings {
        /// <summary>Which transport the built-in display uses.</summary>
        public DisplayType Type { get; set; } = DisplayType.None;

        /// <summary>
        /// True = the board brings up the built-in display during boot so the
        /// user's program sees a ready framebuffer. Only honoured for the
        /// built-in Parallel transport; custom subclasses init from their own code.
        /// </summary>
        public bool EnableAtBoot { get; set; }

        /// <summary>
        /// GPIO pin that drives the backlight, or null for "no backlight" or
        /// "customer's external backlight controller". The DisplayController
        /// does not fail when this is null - the panel is enabled, Flush
        /// works, the backlight is simply left alone.
        /// </summary>
        public BacklightSettings? Backlight { get; set; }

        /// <summary>Filename of a 24-bit uncompressed BMP on the config partition to show after Enable (null = none).</summary>
        public string? Logo { get; set; }

        /// <summary>Timings for the SoC's parallel RGB panel. Required when <see cref="Type"/> is Parallel.</summary>
        public ParallelLcdSettings? Parallel { get; set; }
    }

    /// <summary>GPIO pin that drives the panel's backlight.</summary>
    public sealed class BacklightSettings {
        /// <summary>Gpiochip name as the kernel exposes it (e.g. "gpiochip7").</summary>
        public string? GpioChip { get; set; }

        /// <summary>Line number on the chip.</summary>
        public int? GpioPin { get; set; }

        /// <summary>
        /// Which GPIO level turns the backlight ON. ActiveHigh (default) =
        /// pulled high to light the panel. ActiveLow is common when the pin
        /// drives the gate of a PNP transistor.
        /// </summary>
        public SignalPolarity ActivePolarity { get; set; } = SignalPolarity.ActiveHigh;
    }

    /// <summary>Timings for the SoC's built-in parallel RGB panel controller.</summary>
    public sealed class ParallelLcdSettings {
        /// <summary>Visible width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Visible height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Pixel clock in Hz.</summary>
        public int PixelClockHz { get; set; }

        /// <summary>Horizontal front porch, in pixel clocks.</summary>
        public int HFrontPorch { get; set; }

        /// <summary>Horizontal back porch, in pixel clocks.</summary>
        public int HBackPorch { get; set; }

        /// <summary>Horizontal sync pulse width, in pixel clocks.</summary>
        public int HSync { get; set; }

        /// <summary>Vertical front porch, in lines.</summary>
        public int VFrontPorch { get; set; }

        /// <summary>Vertical back porch, in lines.</summary>
        public int VBackPorch { get; set; }

        /// <summary>Vertical sync pulse width, in lines.</summary>
        public int VSync { get; set; }

        /// <summary>Framebuffer bit depth (16 = RGB565, 24 = RGB888).</summary>
        public int BitsPerPixel { get; set; } = 16;

        /// <summary>
        /// Polarity of the DE (Data Enable) signal. Most RGB panels want
        /// ActiveHigh (DE high while valid pixel data is on the bus); check
        /// your panel's datasheet.
        /// </summary>
        public SignalPolarity DataEnablePolarity { get; set; } = SignalPolarity.ActiveHigh;

        /// <summary>
        /// Which edge of the pixel clock the panel latches data on. The SoC
        /// drives data on the opposite edge. Rising is the common default;
        /// try Falling if the image looks sheared or torn.
        /// </summary>
        public ClockEdge PixelClockEdge { get; set; } = ClockEdge.Rising;
    }
}
