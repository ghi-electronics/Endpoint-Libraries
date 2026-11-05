using GHIElectronics.Endpoint.Core.Internal;

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
        public static void Initialize(int pin) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: throw new PlatformNotSupportedException(
                    "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
                case Chip.Epn256: EPN256.Pwm.Initialize(pin); break;
                default:          EPM815.Pwm.Initialize(pin); break;
            }
        }

        /// <summary>Releases the PWM pin back to free GPIO.</summary>
        public static void UnInitialize(int pin) {
            switch (ChipDetect.Current) {
                case Chip.Epn128: return;
                case Chip.Epn256: EPN256.Pwm.UnInitialize(pin); break;
                default:          EPM815.Pwm.UnInitialize(pin); break;
            }
        }

        /// <summary>
        /// Kernel <c>pwmchipN</c> index to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// </summary>
        public static int GetChipId(int pin) => ChipDetect.Current switch {
            Chip.Epn128 => throw new PlatformNotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins."),
            Chip.Epn256 => EPN256.Pwm.GetChipId(pin),
            _           => EPM815.Pwm.GetChipId(pin),
        };

        /// <summary>
        /// Channel index within the PWM chip to pass to
        /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
        /// </summary>
        public static int GetChannelId(int pin) => ChipDetect.Current switch {
            Chip.Epn128 => throw new PlatformNotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins."),
            Chip.Epn256 => EPN256.Pwm.GetChannelId(pin),
            _           => EPM815.Pwm.GetChannelId(pin),
        };
    }
}
