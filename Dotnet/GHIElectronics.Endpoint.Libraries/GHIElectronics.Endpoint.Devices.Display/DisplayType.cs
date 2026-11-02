namespace GHIElectronics.Endpoint.Devices.Display {
    /// <summary>
    /// Which built-in transport the display uses. <see cref="None"/> means
    /// the board has no built-in display and either (a) nothing is attached
    /// or (b) a customer subclass of <see cref="DisplayController"/> owns the
    /// panel directly over SPI/I2C/anything else.
    /// </summary>
    public enum DisplayType {
        /// <summary>No built-in display is attached, or a customer subclass owns the panel directly.</summary>
        None = 0,
        /// <summary>The SoC's parallel-RGB controller drives the panel through <c>/dev/fb0</c>.</summary>
        Parallel = 1,
    }
}
