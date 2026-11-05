using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for PWM setup. Call once before using
    /// <see cref="System.Device.Pwm.PwmChannel"/>: it brings up the PWM
    /// channel and muxes its pin.
    /// </summary>
    /// <example>
    /// <code>
    /// PwmController.Initialize(EPN256.Pwm.Epwm0);
    /// using var pwm = PwmChannel.Create(
    ///     PwmController.GetChipId(EPN256.Pwm.Epwm0),
    ///     PwmController.GetChannelId(EPN256.Pwm.Epwm0),
    ///     frequency: 1000, dutyCyclePercentage: 0.5);
    /// pwm.Start();
    /// </code>
    /// </example>
    public static class PwmController {
        /// <summary>
        /// Brings up a PWM channel. On EPM815 the argument is a pin constant
        /// (for example <c>EPM815.Pwm.PA0</c>); on EPN256/EPN128 it is a
        /// controller constant (for example <c>EPN256.Pwm.Epwm0</c>). In both
        /// cases the customer passes a chip-specific constant from the matching
        /// namespace, so forwarding is direct.
        /// </summary>
        public static void Initialize(int pinOrController) {
            if (ChipDetect.IsMa35) EPN256.Pwm.Initialize(pinOrController);
            else                   EPM815.Pwm.Initialize(pinOrController);
        }

        /// <summary>Releases the PWM channel's pin.</summary>
        public static void UnInitialize(int pinOrController) {
            if (ChipDetect.IsMa35) EPN256.Pwm.UnInitialize(pinOrController);
            else                   EPM815.Pwm.UnInitialize(pinOrController);
        }

        /// <summary>
        /// Kernel <c>pwmchipN</c> index to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// </summary>
        public static int GetChipId(int pinOrController) =>
            ChipDetect.IsMa35 ? EPN256.Pwm.GetChipId(pinOrController) : EPM815.Pwm.GetChipId(pinOrController);

        /// <summary>
        /// Channel index within the PWM chip to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// MA35D1 EPWM maps one controller per <c>pwmchipN</c> with a single
        /// channel 0; EPM815 uses the pin's channel within its TIM.
        /// </summary>
        public static int GetChannelId(int pinOrController) =>
            ChipDetect.IsMa35 ? 0 : EPM815.Pwm.GetChannelId(pinOrController);
    }
}
