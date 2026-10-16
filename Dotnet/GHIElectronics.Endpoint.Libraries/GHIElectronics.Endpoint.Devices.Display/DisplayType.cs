namespace GHIElectronics.Endpoint.Devices.Display {
    /// <summary>
    /// Which built-in transport the display uses. <see cref="None"/> means
    /// the board has no built-in display and either (a) nothing is attached
    /// or (b) a customer subclass of <see cref="DisplayController"/> owns the
    /// panel directly over SPI/I2C/anything else.
    /// </summary>
    public enum DisplayType {
        None = 0,
        Parallel = 1,
    }
}
