using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core.Chips {
    /// <summary>
    /// Pin setup of the Nuvoton MA35D1 / MA35D0 (multi-function pin selection,
    /// mode, pull, slew), written to the SYS + GPIO registers. Products built
    /// on these chips (EPN256 for MA35D1, EPN128 for MA35D0) use it; another
    /// chip family gets its own class. A pin is a port (0 = A ... 13 = N) and
    /// a line (0 to 15).
    ///
    /// Shape mirrors <see cref="Stm32Mp1Pins"/> so <see cref="PeripheralPins"/>
    /// can dispatch between them by detected chip. Semantic differences from
    /// STM32:
    /// <list type="bullet">
    /// <item><description>No separate "Alternate function" MODE state. Any
    /// non-zero MFP value selects a peripheral function; MODE is ignored
    /// while a peripheral owns the pin. MFP = 0 means plain GPIO.</description></item>
    /// <item><description>No separate output-type register. Open-drain is a
    /// MODE value, not a bit — <see cref="OutputType.OpenDrain"/> maps to
    /// <see cref="Mode.OpenDrain"/>.</description></item>
    /// <item><description>No analog mode: ADC pins are selected via MFP as
    /// a peripheral function like any other.</description></item>
    /// </list>
    /// MA35D0 (EPN128) and MA35D1 (EPN256) share the same pinctrl layout at
    /// this level — the chips differ in how many ports / pins are bonded
    /// out, not in the register map — so one helper covers both. Per-chip
    /// bus tables (EPN256.I2c.cs, EPN128.I2c.cs) carry the function numbers.
    /// </summary>
    internal static class Ma35dPins {
        // =====================================================================
        // MA35-specific MODE enum. Pull / OutputType / Speed come from the
        // chip-neutral PinOptions so PinFunction can carry them for either
        // chip; the shared Speed values (Low/Medium/High/VeryHigh) map onto
        // the MA35's three SLEWCTL settings below in SetSpeed.
        // =====================================================================

        public enum Mode { Input = 0, Output = 1, OpenDrain = 2, Quasi = 3 }

        /// <summary>A pin's whole setup, snapshot for <see cref="Restore"/>.</summary>
        public readonly record struct State(Mode Mode, int Mfp, Pull Pull, Speed Speed);

        // =====================================================================
        // Register map (confirmed from Linux drivers/pinctrl/nuvoton/pinctrl-nvt.c
        // and arch/arm64/boot/dts/nuvoton/ma35d1.dtsi).
        // =====================================================================

        /// <summary>SYS block base: holds MFP registers for every port.</summary>
        private const ulong SysBase = 0x40460000;

        /// <summary>Offset of the first MFP register inside the SYS block.</summary>
        private const ulong MfpBase = 0x80;

        /// <summary>GPIO bank A MMIO base; banks B..N follow every 0x40 bytes.</summary>
        private const ulong GpioABase = 0x40040000;

        private const ulong GpioStride = 0x40;

        /// <summary>Last port: N (A = 0 ... N = 13).</summary>
        public const int MaxPort = 13;

        /// <summary>
        /// Decodes an EPN256 / EPN128 pin number (<c>port * 16 + line</c>) into
        /// the (port, line) pair this chip helper expects. MA35 ports A..N number
        /// sequentially 0..13 with no gaps, so this is a plain divmod — mirrors
        /// <see cref="Stm32Mp1Pins.FromPin"/> for dispatch from
        /// <see cref="Internal.PeripheralPins"/>.
        /// </summary>
        public static (int Port, int Line) FromPin(int pin) => (pin / 16, pin % 16);

        // Per-bank register offsets (same for every bank)
        private const ulong GpioMode    = 0x00;  // 2 bits/pin
        private const ulong GpioPusel   = 0x30;  // 2 bits/pin
        private const ulong GpioSlewctl = 0x28;  // 2 bits/pin
        private const ulong GpioSmten   = 0x24;  // 1 bit/pin — Schmitt trigger
        private const ulong GpioDs      = 0x38;  // drive strength

        // =====================================================================
        // Public methods — same shape as Stm32Mp1Pins.
        // =====================================================================

        /// <summary>Writes the pin's MODE field.</summary>
        public static void SetMode(int port, int line, Mode mode) =>
            PhysicalMemory.Modify(PortBase(port, line) + GpioMode,
                3u << (line * 2), (uint)mode << (line * 2));

        /// <summary>Writes the pin's PUSEL field (pull-up / pull-down / off).</summary>
        public static void SetPull(int port, int line, Pull pull) =>
            PhysicalMemory.Modify(PortBase(port, line) + GpioPusel,
                3u << (line * 2), (uint)pull << (line * 2));

        /// <summary>
        /// Writes the pin's SLEWCTL field. The chip-neutral <see cref="Speed"/>
        /// enum has four values; the MA35 SLEWCTL has three (0=normal,
        /// 1=high, 2=fast). Mapping: Low→0, Medium/High→1, VeryHigh→2.
        /// </summary>
        public static void SetSpeed(int port, int line, Speed speed) {
            uint slewctl = speed switch {
                Speed.Low => 0,
                Speed.VeryHigh => 2,
                _ => 1,  // Medium and High both map to "high slew"
            };
            PhysicalMemory.Modify(PortBase(port, line) + GpioSlewctl,
                3u << (line * 2), slewctl << (line * 2));
        }

        /// <summary>
        /// Writes the pin's output type. On MA35 this folds into MODE —
        /// OpenDrain selects <see cref="Mode.OpenDrain"/>, PushPull selects
        /// <see cref="Mode.Output"/>. Only meaningful when the pin is in a
        /// GPIO function (MFP = 0).
        /// </summary>
        public static void SetOutputType(int port, int line, OutputType type) =>
            SetMode(port, line, type == OutputType.OpenDrain ? Mode.OpenDrain : Mode.Output);

        /// <summary>Writes the pin's MFP field (multi-function selector; 0 = GPIO).</summary>
        public static void SetAlternate(int port, int line, int function) {
            if (function is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(function), function, "MFP functions are 0 to 15");
            }
            CheckPort(port); CheckLine(line);
            var register = SysBase + MfpBase + ((ulong)port * 0x08) + (line < 8 ? 0ul : 0x04ul);
            var shift = (line % 8) * 4;
            PhysicalMemory.Modify(register, 0xFu << shift, (uint)function << shift);
        }

        /// <summary>Reads the pin's whole setup so <see cref="Restore"/> can put it back.</summary>
        public static State Save(int port, int line) {
            CheckPort(port); CheckLine(line);
            var b = PortBase(port, line);
            var mfpReg = SysBase + MfpBase + ((ulong)port * 0x08) + (line < 8 ? 0ul : 0x04ul);
            var mfpShift = (line % 8) * 4;
            var slewctl = (PhysicalMemory.Read(b + GpioSlewctl) >> (line * 2)) & 3;
            // Inverse of SetSpeed's mapping (lossy — Medium and High both saved as High).
            var speed = slewctl switch {
                0u => Speed.Low,
                2u => Speed.VeryHigh,
                _ => Speed.High,
            };
            return new State(
                (Mode)((PhysicalMemory.Read(b + GpioMode) >> (line * 2)) & 3),
                (int)((PhysicalMemory.Read(mfpReg) >> mfpShift) & 0xF),
                (Pull)((PhysicalMemory.Read(b + GpioPusel) >> (line * 2)) & 3),
                speed);
        }

        /// <summary>
        /// Puts the pin back as it was. MFP last so the peripheral is
        /// released before the GPIO MODE / PUSEL settings become active.
        /// </summary>
        public static void Restore(int port, int line, State state) {
            SetMode(port, line, state.Mode);
            SetPull(port, line, state.Pull);
            SetSpeed(port, line, state.Speed);
            SetAlternate(port, line, state.Mfp);
        }

        /// <summary>
        /// The pin as a peripheral's pin: MFP first (hands ownership to the
        /// peripheral IP), then pull and slew for signal integrity. The
        /// <paramref name="type"/> parameter is accepted for API parity with
        /// <see cref="Stm32Mp1Pins.SetFunction"/>; on MA35 the peripheral
        /// controls direction / type once MFP picks it, so OutputType does
        /// not apply here — if the bus is open-drain (I²C), the peripheral
        /// already drives open-drain.
        /// </summary>
        public static void SetFunction(int port, int line, int function, Pull pull = Pull.None,
            OutputType type = OutputType.PushPull, Speed speed = Speed.Low) {
            SetAlternate(port, line, function);
            SetPull(port, line, pull);
            SetSpeed(port, line, speed);
            _ = type;  // suppress "unused" — kept for signature parity
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static ulong PortBase(int port, int line) {
            CheckPort(port); CheckLine(line);
            return GpioABase + (ulong)port * GpioStride;
        }

        private static void CheckPort(int port) {
            if (port is < 0 or > MaxPort) {
                throw new ArgumentOutOfRangeException(nameof(port), port, "Ports are A to N");
            }
        }

        private static void CheckLine(int line) {
            if (line is < 0 or > 15) {
                throw new ArgumentOutOfRangeException(nameof(line), line, "Pins are 0 to 15 in a port");
            }
        }
    }
}
