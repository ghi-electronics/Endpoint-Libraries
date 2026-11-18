namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for PWM setup. Call once before using
    /// <see cref="System.Device.Pwm.PwmChannel"/>: it brings up the PWM
    /// channel and muxes its pin.
    /// </summary>
    /// <example>
    /// <code>
    /// PwmController.Initialize(EPN256.Pwm.Pin.PK5);
    /// using var pwm = PwmChannel.Create(
    ///     PwmController.GetChipId(EPN256.Pwm.Pin.PK5),
    ///     PwmController.GetChannelId(EPN256.Pwm.Pin.PK5),
    ///     frequency: 1000, dutyCyclePercentage: 0.5);
    /// pwm.Start();
    /// </code>
    /// </example>
    public static class PwmController {
        /// <summary>
        /// Brings up a PWM channel. The argument is a pin constant from the
        /// chip's namespace (for example <c>EPM815.Pwm.Pin.PA8</c> or
        /// <c>EPN256.Pwm.Pin.PK5</c>); only that pin is claimed.
        /// </summary>
        public static void Initialize(int pin) => BoardSupport.Current.PwmInitialize(pin);

        /// <summary>Releases the PWM pin back to free GPIO.</summary>
        public static void UnInitialize(int pin) => BoardSupport.Current.PwmUnInitialize(pin);

        /// <summary>
        /// Kernel <c>pwmchipN</c> index to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// </summary>
        public static int GetChipId(int pin) => BoardSupport.Current.PwmGetChipId(pin);

        /// <summary>
        /// Channel index within the PWM chip to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// </summary>
        public static int GetChannelId(int pin) => BoardSupport.Current.PwmGetChannelId(pin);
    }
}
