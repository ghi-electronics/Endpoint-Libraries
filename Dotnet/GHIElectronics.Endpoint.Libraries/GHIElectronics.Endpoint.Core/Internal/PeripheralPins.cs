using GHIElectronics.Endpoint.Core.Chips;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>One pin of a peripheral (I2C, SPI, PWM, UART...) and how to set it up</summary>
    internal readonly record struct PinFunction(int Pin, int Alternate,
        Pull Pull = Pull.None,
        OutputType Type = OutputType.PushPull,
        Speed Speed = Speed.Low) {
        /// <summary>The Alternate value that means analog mode instead of a function</summary>
        public const int AnalogFunction = -1;

        /// <summary>An ADC input or DAC output pin: analog mode, no pull</summary>
        public static PinFunction Analog(int pin) => new(pin, AnalogFunction);
    }

    /// <summary>
    /// Connects pins to a peripheral, and gives them back as they were found.
    /// The device tree leaves these pins alone (empty pinctrl), so the kernel
    /// does not change them behind our back.
    ///
    /// Dispatches to the right chip helper (<see cref="Stm32Mp1Pins"/> for
    /// EPM815, <see cref="Ma35dPins"/> for EPN256 / EPN128) based on the
    /// chip detected at startup (see <see cref="ChipDetect"/>). Both helpers
    /// expose the same shape of SetFunction / Save / Restore; the per-chip
    /// State object is boxed in <see cref="Saved"/> so this file can stay
    /// chip-neutral.
    /// </summary>
    internal static class PeripheralPins {
        // State is held as object because each chip helper has its own
        // State record (different register layouts). Boxing happens once per
        // pin, on Connect; restored via cast on Disconnect.
        private readonly record struct Saved(int Pin, int Port, int Line, object State);

        private static readonly object Gate = new();
        private static readonly Dictionary<string, Saved[]> Connected = new();

        /// <summary>
        /// Reserves the pins for <paramref name="owner"/> ("I2C1", "UART4"...),
        /// all or none, and connects them. Nothing changes if the owner already
        /// has them: then it returns false.
        /// </summary>
        public static bool Connect(string owner, params PinFunction[] pins) {
            lock (Gate) {
                if (Connected.ContainsKey(owner)) {
                    return false;
                }

                PinReservations.Reserve(owner, pins.Select(p => p.Pin).ToArray());
                var saved = new List<Saved>();
                try {
                    foreach (var p in pins) {
                        // Pin number is port * 16 + line, but each chip has its
                        // own port numbering (STM32 identifies port Z as 25, not
                        // 9, because its clock + MMIO live on a different bus).
                        // Delegate the decode to the chip helper via dispatch.
                        var (port, line) = ChipFromPin(p.Pin);
                        saved.Add(new Saved(p.Pin, port, line, ChipSave(port, line)));
                        if (p.Alternate == PinFunction.AnalogFunction) {
                            ChipSetAnalog(port, line);
                        }
                        else {
                            ChipSetFunction(port, line, p.Alternate, p.Pull, p.Type, p.Speed);
                        }
                    }
                }
                catch {
                    Restore(saved);
                    PinReservations.Release(pins.Select(p => p.Pin).ToArray());
                    throw;
                }
                Connected[owner] = saved.ToArray();
                return true;
            }
        }

        /// <summary>Puts the owner's pins back as they were and frees them</summary>
        public static void Disconnect(string owner) {
            lock (Gate) {
                if (Connected.Remove(owner, out var saved)) {
                    Restore(saved);
                    PinReservations.Release(saved.Select(s => s.Pin).ToArray());
                }
            }
        }

        public static bool IsConnected(string owner) {
            lock (Gate) {
                return Connected.ContainsKey(owner);
            }
        }

        private static void Restore(IEnumerable<Saved> saved) {
            foreach (var s in saved) {
                ChipRestore(s.Port, s.Line, s.State);
            }
        }

        // ============== Chip dispatch ==============

        private static (int Port, int Line) ChipFromPin(int pin) => ChipDetect.IsMa35
            ? Ma35dPins.FromPin(pin)
            : Stm32Mp1Pins.FromPin(pin);

        private static object ChipSave(int port, int line) => ChipDetect.IsMa35
            ? Ma35dPins.Save(port, line)
            : Stm32Mp1Pins.Save(port, line);

        private static void ChipSetFunction(int port, int line, int function,
                                            Pull pull, OutputType type, Speed speed) {
            if (ChipDetect.IsMa35) {
                Ma35dPins.SetFunction(port, line, function, pull, type, speed);
            }
            else {
                Stm32Mp1Pins.SetFunction(port, line, function, pull, type, speed);
            }
        }

        private static void ChipSetAnalog(int port, int line) {
            // STM32: Analog is a MODE value. MA35: no analog mode; ADC pins
            // are picked by MFP like any other peripheral function (callers
            // for Adc on Nuvoton use SetFunction with the ADC MFP value, not
            // this path). Nothing to do here on MA35.
            if (!ChipDetect.IsMa35) {
                Stm32Mp1Pins.SetPull(port, line, Pull.None);
                Stm32Mp1Pins.SetMode(port, line, Stm32Mp1Pins.Mode.Analog);
            }
        }

        private static void ChipRestore(int port, int line, object state) {
            if (ChipDetect.IsMa35) {
                Ma35dPins.Restore(port, line, (Ma35dPins.State)state);
            }
            else {
                Stm32Mp1Pins.Restore(port, line, (Stm32Mp1Pins.State)state);
            }
        }

        /// <summary>
        /// Linux gpiochip number for the pin, dispatched to the right chip's
        /// <c>Gpio.GetChipId</c> based on <see cref="ChipDetect.Current"/>.
        /// Chip-neutral entry point so callers (e.g. DisplayController's
        /// backlight serializer) can turn a packed pin into chip + line
        /// without caring which board they're on. Falls back to EPM815 when
        /// the chip is unknown (dev host, test machine).
        /// </summary>
        public static int ChipGetChipId(int pin) => ChipDetect.Current switch {
            Chip.Epn256 => EPN256.Gpio.GetChipId(pin),
            Chip.Epn128 => EPN128.Gpio.GetChipId(pin),
            _ => EPM815.Gpio.GetChipId(pin),
        };

        /// <summary>
        /// Line number within the gpiochip for the pin. Pairs with
        /// <see cref="ChipGetChipId"/>.
        /// </summary>
        public static int ChipGetLineId(int pin) => ChipDetect.Current switch {
            Chip.Epn256 => EPN256.Gpio.GetLineId(pin),
            Chip.Epn128 => EPN128.Gpio.GetLineId(pin),
            _ => EPM815.Gpio.GetLineId(pin),
        };
    }
}
