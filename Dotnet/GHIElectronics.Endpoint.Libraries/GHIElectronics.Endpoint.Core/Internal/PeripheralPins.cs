using GHIElectronics.Endpoint.Core.Chips;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>One pin of a peripheral (I2C, SPI, PWM, UART...) and how to set it up</summary>
    internal readonly record struct PinFunction(int Pin, int Alternate,
        Stm32Mp1Pins.Pull Pull = Stm32Mp1Pins.Pull.None,
        Stm32Mp1Pins.OutputType Type = Stm32Mp1Pins.OutputType.PushPull,
        Stm32Mp1Pins.Speed Speed = Stm32Mp1Pins.Speed.Low) {
        /// <summary>The Alternate value that means analog mode instead of a function</summary>
        public const int AnalogFunction = -1;

        /// <summary>An ADC input or DAC output pin: analog mode, no pull</summary>
        public static PinFunction Analog(int pin) => new(pin, AnalogFunction);
    }

    /// <summary>
    /// Connects pins to a peripheral, and gives them back as they were found.
    /// The device tree leaves these pins alone (empty pinctrl), so the kernel
    /// does not change them behind our back.
    /// </summary>
    internal static class PeripheralPins {
        private readonly record struct Saved(int Pin, int Port, int Line, Stm32Mp1Pins.State State);

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
                        var (port, line) = EPM815.Gpio.ChipPin(p.Pin);
                        saved.Add(new Saved(p.Pin, port, line, Stm32Mp1Pins.Save(port, line)));
                        if (p.Alternate == PinFunction.AnalogFunction) {
                            Stm32Mp1Pins.SetPull(port, line, Stm32Mp1Pins.Pull.None);
                            Stm32Mp1Pins.SetMode(port, line, Stm32Mp1Pins.Mode.Analog);
                        }
                        else {
                            Stm32Mp1Pins.SetFunction(port, line, p.Alternate, p.Pull, p.Type, p.Speed);
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
                Stm32Mp1Pins.Restore(s.Port, s.Line, s.State);
            }
        }
    }
}
