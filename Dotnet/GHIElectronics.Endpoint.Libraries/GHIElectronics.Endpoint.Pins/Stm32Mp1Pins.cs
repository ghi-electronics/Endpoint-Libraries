using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins {
    /// <summary>
    /// Pin setup of the STM32MP1 (mode, alternate function, pull, speed,
    /// output type). All reads and writes go through the
    /// <c>/usr/sbin/endpoint-pinmux</c> C binary; no register map lives in
    /// managed code. See <c>Endpoint-Firmware/pinmux/src/stm32mp1.c</c> for
    /// the authoritative implementation.
    ///
    /// Pin encoding (shared with <c>Pins.EPM815</c>): <c>port * 16 + line</c>
    /// where <c>port</c> is 0..10 (A..K) or 25 (Z). The EPM815 user API packs
    /// Z as 9; the translation happens in <see cref="FromPin"/> and <see cref="ToPin"/>.
    /// </summary>
    internal static class Stm32Mp1Pins {
        /// <summary>STM32-specific MODER value (Input / Output / Alternate / Analog).</summary>
        public enum Mode { Input = 0, Output = 1, Alternate = 2, Analog = 3 }

        /// <summary>Opaque snapshot of a pin's full register state; pass back to <see cref="Restore"/>.</summary>
        public readonly record struct State(uint Packed);

        /// <summary>Port Z: separate bus in silicon, identified as port 25.</summary>
        public const int PortZ = 25;

        /// <summary>Decode an EPM815 packed pin (<c>port*16 + line</c>, Z=9) into (port, line) with Z=25.</summary>
        public static (int Port, int Line) FromPin(int pin) {
            var port = pin / 16;
            return (port == 9 ? PortZ : port, pin % 16);
        }

        // Re-encode (port, line) back to the chip-neutral pin number expected
        // by the C binary. Z=25 → 9 so the uint16 pin arg stays small.
        private static int ToPin(int port, int line) => (port == PortZ ? 9 : port) * 16 + line;

        // Function packing. Keep in sync with stm32mp1.c:
        //   [Mode:2][Type:1][Speed:2][Pull:2][AF:4]
        private const int AfShift    = 0,  AfMask    = 0xF;
        private const int PullShift  = 4,  PullMask  = 0x3;
        private const int SpeedShift = 6,  SpeedMask = 0x3;
        private const int TypeShift  = 8,  TypeMask  = 0x1;
        private const int ModeShift  = 9,  ModeMask  = 0x3;

        private static uint Pack(Mode mode, OutputType type, Speed speed, Pull pull, int af) =>
            (((uint)mode & ModeMask)  << ModeShift)  |
            (((uint)type & TypeMask)  << TypeShift)  |
            (((uint)speed & SpeedMask) << SpeedShift) |
            (((uint)pull & PullMask)   << PullShift)  |
            (((uint)af   & AfMask)     << AfShift);

        private static uint Replace(uint v, int shift, uint mask, uint field) =>
            (v & ~(mask << shift)) | ((field & mask) << shift);

        public static void SetMode(int port, int line, Mode mode) {
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), ModeShift, ModeMask, (uint)mode));
        }

        public static void SetPull(int port, int line, Pull pull) {
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), PullShift, PullMask, (uint)pull));
        }

        public static void SetSpeed(int port, int line, Speed speed) {
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), SpeedShift, SpeedMask, (uint)speed));
        }

        public static void SetOutputType(int port, int line, OutputType type) {
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), TypeShift, TypeMask, (uint)type));
        }

        public static void SetAlternate(int port, int line, int function) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "Alternate functions are 0 to 15");
            }
            var pin = ToPin(port, line);
            Pinmux.Set(pin, Replace(Pinmux.Get(pin), AfShift, AfMask, (uint)function));
        }

        /// <summary>Snapshot the pin's full setup so <see cref="Restore"/> can put it back.</summary>
        public static State Save(int port, int line) =>
            new State(Pinmux.Get(ToPin(port, line)));

        /// <summary>Restore the pin to the state captured by <see cref="Save"/>.</summary>
        public static void Restore(int port, int line, State state) =>
            Pinmux.Set(ToPin(port, line), state.Packed);

        /// <summary>Set the pin as a peripheral's pin: alternate function, pull, speed, type, mode all at once.</summary>
        public static void SetFunction(int port, int line, int function, Pull pull = Pull.None,
            OutputType type = OutputType.PushPull, Speed speed = Speed.Low) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "Alternate functions are 0 to 15");
            }
            Pinmux.Set(ToPin(port, line), Pack(Mode.Alternate, type, speed, pull, function));
        }
    }
}
