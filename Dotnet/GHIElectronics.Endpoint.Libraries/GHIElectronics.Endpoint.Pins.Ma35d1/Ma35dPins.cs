using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins.Ma35d1 {
    /// <summary>
    /// Pin setup of the Nuvoton MA35D1 / MA35D0 (multi-function pin selection,
    /// mode, pull, slew). All reads and writes go through the
    /// <c>/usr/sbin/endpoint-pinmux</c> C binary; no register map lives in
    /// managed code. See <c>Endpoint-Firmware/pinmux/src/ma35d.c</c> for
    /// the authoritative implementation.
    ///
    /// Semantic differences from STM32 (same shape as before, for API parity
    /// with <c>Stm32Mp1Pins</c>):
    /// - MFP (0..15) is the alternate function; 0 = plain GPIO.
    /// - Mode has four values: Input / Output / OpenDrain / Quasi. Open-drain
    ///   is a Mode value (there is no separate OutputType register on MA35);
    ///   the chip-neutral <see cref="OutputType.OpenDrain"/> maps to
    ///   <see cref="Mode.OpenDrain"/> in <see cref="SetFunction"/>.
    /// - Slew has three steps (0=Normal, 1=High, 2=Fast); the chip-neutral
    ///   <see cref="Speed"/> maps Low→0, Medium/High→1, VeryHigh→2 (lossy).
    /// - No analog mode: ADC pins select via MFP like any peripheral function.
    /// </summary>
    internal static class Ma35dPins {
        /// <summary>MA35-specific MODE enum (two bits per pin, per bank).</summary>
        public enum Mode { Input = 0, Output = 1, OpenDrain = 2, Quasi = 3 }

        /// <summary>Opaque snapshot of a pin's full state; pass back to <see cref="Restore"/>.</summary>
        public readonly record struct State(uint Packed);

        /// <summary>Last port exposed on the chip (A = 0 ... N = 13).</summary>
        public const int MaxPort = 13;

        /// <summary>Decode an EPN256 / EPN128 packed pin (<c>port*16 + line</c>) into (port, line).</summary>
        public static (int Port, int Line) FromPin(int pin) => (pin / 16, pin % 16);

        private static int ToPin(int port, int line) => port * 16 + line;

        // Function packing. Keep in sync with ma35d.c:
        //   [Slew:2][Pull:2][Mode:2][MFP:4]
        private const int MfpShift  = 0, MfpMask  = 0xF;
        private const int ModeShift = 4, ModeMask = 0x3;
        private const int PullShift = 6, PullMask = 0x3;
        private const int SlewShift = 8, SlewMask = 0x3;

        private static uint Pack(Mode mode, Pull pull, Speed speed, int mfp) =>
            (SpeedToSlew(speed) << SlewShift) |
            (((uint)pull & PullMask) << PullShift) |
            (((uint)mode & ModeMask) << ModeShift) |
            (((uint)mfp  & MfpMask)  << MfpShift);

        // Chip-neutral Speed (4 values) → MA35 SLEWCTL (3 values). Lossy; Medium
        // and High both collapse to "high". Same table as ma35d.c on the C side.
        private static uint SpeedToSlew(Speed speed) => speed switch {
            Speed.Low      => 0,
            Speed.VeryHigh => 2,
            _              => 1,
        };

        private static uint Replace(uint v, int shift, uint mask, uint field) =>
            (v & ~(mask << shift)) | ((field & mask) << shift);

        private static void CheckPort(int port) {
            if (port is < 0 or > MaxPort)
                throw new ArgumentOutOfRangeException(nameof(port), port, "Ports are A to N");
        }

        private static void CheckLine(int line) {
            if (line is < 0 or > 15)
                throw new ArgumentOutOfRangeException(nameof(line), line, "Pins are 0 to 15 in a port");
        }

        public static void SetMode(int port, int line, Mode mode) {
            CheckPort(port); CheckLine(line);
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), ModeShift, ModeMask, (uint)mode));
        }

        public static void SetPull(int port, int line, Pull pull) {
            CheckPort(port); CheckLine(line);
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), PullShift, PullMask, (uint)pull));
        }

        public static void SetSpeed(int port, int line, Speed speed) {
            CheckPort(port); CheckLine(line);
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), SlewShift, SlewMask, SpeedToSlew(speed)));
        }

        /// <summary>On MA35 output-type folds into Mode: OpenDrain → Mode.OpenDrain, PushPull → Mode.Output.</summary>
        public static void SetOutputType(int port, int line, OutputType type) =>
            SetMode(port, line, type == OutputType.OpenDrain ? Mode.OpenDrain : Mode.Output);

        public static void SetAlternate(int port, int line, int function) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "MFP functions are 0 to 15");
            }
            CheckPort(port); CheckLine(line);
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), MfpShift, MfpMask, (uint)function));
        }

        /// <summary>Snapshot the pin's full setup so <see cref="Restore"/> can put it back.</summary>
        public static State Save(int port, int line) {
            CheckPort(port); CheckLine(line);
            return new State(Pinmux.Get(ToPin(port, line)));
        }

        /// <summary>Restore the pin to the state captured by <see cref="Save"/>.</summary>
        public static void Restore(int port, int line, State state) {
            CheckPort(port); CheckLine(line);
            Pinmux.Set(ToPin(port, line), state.Packed);
        }

        /// <summary>
        /// Set the pin as a peripheral's pin: MFP + pull + slew + mode all at once.
        /// The <paramref name="type"/> parameter is accepted for API parity with
        /// <c>Stm32Mp1Pins.SetFunction</c>; on MA35 the peripheral owns the pin
        /// once MFP is set, so OutputType does not apply here.
        /// </summary>
        public static void SetFunction(int port, int line, int function, Pull pull = Pull.None,
            OutputType type = OutputType.PushPull, Speed speed = Speed.Low) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "MFP functions are 0 to 15");
            }
            CheckPort(port); CheckLine(line);
            _ = type;  // suppress "unused"; kept for signature parity
            Pinmux.Set(ToPin(port, line), Pack(Mode.Input, pull, speed, function));
        }
    }
}
