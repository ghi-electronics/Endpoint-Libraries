namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Which pins this app uses, and for what, so two features cannot use the
    /// same pin by mistake (for example CAN2 and UART5 share PB12/PB13)
    /// </summary>
    internal static class PinReservations {
        private static readonly object Gate = new();
        private static readonly Dictionary<int, string> Owners = new();

        /// <summary>Reserves the pins for <paramref name="owner"/>, all or none</summary>
        public static void Reserve(string owner, params int[] pins) {
            lock (Gate) {
                foreach (var pin in pins) {
                    if (Owners.TryGetValue(pin, out var current)) {
                        throw new InvalidOperationException($"Pin {EPM815.Gpio.Name(pin)} is already in use ({current})");
                    }
                }
                foreach (var pin in pins) {
                    Owners[pin] = owner;
                }
            }
        }

        public static void Release(params int[] pins) {
            lock (Gate) {
                foreach (var pin in pins) {
                    Owners.Remove(pin);
                }
            }
        }

        public static bool IsReserved(int pin) {
            lock (Gate) {
                return Owners.ContainsKey(pin);
            }
        }
    }
}
