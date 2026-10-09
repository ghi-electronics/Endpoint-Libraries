using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core.Chips {
    /// <summary>
    /// Pin setup of the STM32MP1 (mode, alternate function, pull, speed,
    /// output type), written to the GPIO registers. Products built on this
    /// chip (EPM815) use it; another chip gets its own class. A pin is a port
    /// (0 = A ... 10 = K, or <see cref="PortZ"/>) and a line (0 to 15).
    /// The firmware keeps the port clocks on (kernel patch "pinctrl: stm32:
    /// keep the GPIO bank clocks on"): without it, reads return 0 and writes
    /// are lost, and turning a clock on from here would not last, because
    /// the kernel turns it off after its next access.
    /// </summary>
    internal static class Stm32Mp1Pins {
        public enum Mode { Input = 0, Output = 1, Alternate = 2, Analog = 3 }
        public enum Pull { None = 0, Up = 1, Down = 2 }
        public enum Speed { Low = 0, Medium = 1, High = 2, VeryHigh = 3 }
        public enum OutputType { PushPull = 0, OpenDrain = 1 }

        /// <summary>A pin's whole setup, to put it back later</summary>
        public readonly record struct State(Mode Mode, int Alternate, Pull Pull, OutputType Type, Speed Speed);

        /// <summary>Port Z: its registers and clock are separate from ports A to K</summary>
        public const int PortZ = 25;

        // Reading RCC_MP_AHBxENSETR gives the clocks that are on
        private const ulong RccAhb4Enable = 0x50000A28;  // GPIOA..K clocks, bit = port
        private const ulong RccAhb5Enable = 0x50000210;  // GPIOZ clock, bit 0
        private const ulong GpioABase = 0x50002000;          // GPIOA; B..K follow every 0x1000
        private const ulong GpioZBase = 0x54004000;

        private const ulong Moder = 0x00, OutputTypes = 0x04, Speeds = 0x08, Pulls = 0x0C, AlternateLow = 0x20, AlternateHigh = 0x24;

        public static void SetMode(int port, int line, Mode mode) =>
            PhysicalMemory.Modify(PortBase(port, line) + Moder, 3u << (line * 2), (uint)mode << (line * 2));

        public static void SetPull(int port, int line, Pull pull) =>
            PhysicalMemory.Modify(PortBase(port, line) + Pulls, 3u << (line * 2), (uint)pull << (line * 2));

        public static void SetSpeed(int port, int line, Speed speed) =>
            PhysicalMemory.Modify(PortBase(port, line) + Speeds, 3u << (line * 2), (uint)speed << (line * 2));

        public static void SetOutputType(int port, int line, OutputType type) =>
            PhysicalMemory.Modify(PortBase(port, line) + OutputTypes, 1u << line, (uint)type << line);

        public static void SetAlternate(int port, int line, int function) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "Alternate functions are 0 to 15");
            }
            var register = line < 8 ? AlternateLow : AlternateHigh;
            var shift = (line % 8) * 4;
            PhysicalMemory.Modify(PortBase(port, line) + register, 0xFu << shift, (uint)function << shift);
        }

        public static State Save(int port, int line) {
            var b = PortBase(port, line);
            var alternate = PhysicalMemory.Read(b + (line < 8 ? AlternateLow : AlternateHigh)) >> ((line % 8) * 4);
            return new State(
                (Mode)((PhysicalMemory.Read(b + Moder) >> (line * 2)) & 3),
                (int)(alternate & 0xF),
                (Pull)((PhysicalMemory.Read(b + Pulls) >> (line * 2)) & 3),
                (OutputType)((PhysicalMemory.Read(b + OutputTypes) >> line) & 1),
                (Speed)((PhysicalMemory.Read(b + Speeds) >> (line * 2)) & 3));
        }

        /// <summary>Puts the setup back, the mode last (no passing through another mode)</summary>
        public static void Restore(int port, int line, State state) {
            SetAlternate(port, line, state.Alternate);
            SetOutputType(port, line, state.Type);
            SetPull(port, line, state.Pull);
            SetSpeed(port, line, state.Speed);
            SetMode(port, line, state.Mode);
        }

        /// <summary>The pin as a peripheral's pin: alternate function first, then the mode</summary>
        public static void SetFunction(int port, int line, int function, Pull pull = Pull.None,
            OutputType type = OutputType.PushPull, Speed speed = Speed.Low) {
            SetAlternate(port, line, function);
            SetOutputType(port, line, type);
            SetPull(port, line, pull);
            SetSpeed(port, line, speed);
            SetMode(port, line, Mode.Alternate);
        }

        private static ulong PortBase(int port, int line) {
            if (line is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(line), line, "Pins are 0 to 15 in a port");
            }
            if (port != PortZ && port is < 0 or > 10) {
                throw new ArgumentOutOfRangeException(nameof(port), port, "Ports are A to K and Z");
            }

            var (clocks, bit) = port == PortZ ? (RccAhb5Enable, 0) : (RccAhb4Enable, port);
            if ((PhysicalMemory.Read(clocks) & (1u << bit)) == 0) {
                var name = port == PortZ ? 'Z' : (char)('A' + port);
                throw new InvalidOperationException(
                    $"The clock of GPIO port {name} is off, so its pins cannot be set up: update the board's firmware");
            }
            return port == PortZ ? GpioZBase : GpioABase + (ulong)port * 0x1000;
        }
    }
}
