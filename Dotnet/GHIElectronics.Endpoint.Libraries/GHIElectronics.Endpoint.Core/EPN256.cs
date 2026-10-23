using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// The EPN256 module (Nuvoton MA35D1, dual Cortex-A35, 256 MB DDR3L):
    /// its pin names and the pin setup the other libraries use. Pin numbers
    /// are port * 16 + pin.
    ///
    /// Mirrors <see cref="EPM815"/> so sample code reads the same shape
    /// regardless of the chip.
    /// </summary>
    public static partial class EPN256 {
        public static partial class Gpio {
            public enum Port : uint { A = 0, B = 1, C = 2, D = 3, E = 4, F = 5, G = 6, H = 7, I = 8, J = 9, K = 10, L = 11, M = 12, N = 13 }

            /// <summary>The EPN256's pins (ports A to N, 16 pins per port).</summary>
            public static class Pin {
                public const int PA0 = 0; public const int PA1 = 1; public const int PA2 = 2; public const int PA3 = 3; public const int PA4 = 4; public const int PA5 = 5; public const int PA6 = 6; public const int PA7 = 7;
                public const int PA8 = 8; public const int PA9 = 9; public const int PA10 = 10; public const int PA11 = 11; public const int PA12 = 12; public const int PA13 = 13; public const int PA14 = 14; public const int PA15 = 15;
                public const int PB0 = 16; public const int PB1 = 17; public const int PB2 = 18; public const int PB3 = 19; public const int PB4 = 20; public const int PB5 = 21; public const int PB6 = 22; public const int PB7 = 23;
                public const int PB8 = 24; public const int PB9 = 25; public const int PB10 = 26; public const int PB11 = 27; public const int PB12 = 28; public const int PB13 = 29; public const int PB14 = 30; public const int PB15 = 31;
                public const int PC0 = 32; public const int PC1 = 33; public const int PC2 = 34; public const int PC3 = 35; public const int PC4 = 36; public const int PC5 = 37; public const int PC6 = 38; public const int PC7 = 39;
                public const int PC8 = 40; public const int PC9 = 41; public const int PC10 = 42; public const int PC11 = 43; public const int PC12 = 44; public const int PC13 = 45; public const int PC14 = 46; public const int PC15 = 47;
                public const int PD0 = 48; public const int PD1 = 49; public const int PD2 = 50; public const int PD3 = 51; public const int PD4 = 52; public const int PD5 = 53; public const int PD6 = 54; public const int PD7 = 55;
                public const int PD8 = 56; public const int PD9 = 57; public const int PD10 = 58; public const int PD11 = 59; public const int PD12 = 60; public const int PD13 = 61; public const int PD14 = 62; public const int PD15 = 63;
                public const int PE0 = 64; public const int PE1 = 65; public const int PE2 = 66; public const int PE3 = 67; public const int PE4 = 68; public const int PE5 = 69; public const int PE6 = 70; public const int PE7 = 71;
                public const int PE8 = 72; public const int PE9 = 73; public const int PE10 = 74; public const int PE11 = 75; public const int PE12 = 76; public const int PE13 = 77; public const int PE14 = 78; public const int PE15 = 79;
                public const int PF0 = 80; public const int PF1 = 81; public const int PF2 = 82; public const int PF3 = 83; public const int PF4 = 84; public const int PF5 = 85; public const int PF6 = 86; public const int PF7 = 87;
                public const int PF8 = 88; public const int PF9 = 89; public const int PF10 = 90; public const int PF11 = 91; public const int PF12 = 92; public const int PF13 = 93; public const int PF14 = 94; public const int PF15 = 95;
                public const int PG0 = 96; public const int PG1 = 97; public const int PG2 = 98; public const int PG3 = 99; public const int PG4 = 100; public const int PG5 = 101; public const int PG6 = 102; public const int PG7 = 103;
                public const int PG8 = 104; public const int PG9 = 105; public const int PG10 = 106; public const int PG11 = 107; public const int PG12 = 108; public const int PG13 = 109; public const int PG14 = 110; public const int PG15 = 111;
                public const int PH0 = 112; public const int PH1 = 113; public const int PH2 = 114; public const int PH3 = 115; public const int PH4 = 116; public const int PH5 = 117; public const int PH6 = 118; public const int PH7 = 119;
                public const int PH8 = 120; public const int PH9 = 121; public const int PH10 = 122; public const int PH11 = 123; public const int PH12 = 124; public const int PH13 = 125; public const int PH14 = 126; public const int PH15 = 127;
                public const int PI0 = 128; public const int PI1 = 129; public const int PI2 = 130; public const int PI3 = 131; public const int PI4 = 132; public const int PI5 = 133; public const int PI6 = 134; public const int PI7 = 135;
                public const int PI8 = 136; public const int PI9 = 137; public const int PI10 = 138; public const int PI11 = 139; public const int PI12 = 140; public const int PI13 = 141; public const int PI14 = 142; public const int PI15 = 143;
                public const int PJ0 = 144; public const int PJ1 = 145; public const int PJ2 = 146; public const int PJ3 = 147; public const int PJ4 = 148; public const int PJ5 = 149; public const int PJ6 = 150; public const int PJ7 = 151;
                public const int PJ8 = 152; public const int PJ9 = 153; public const int PJ10 = 154; public const int PJ11 = 155; public const int PJ12 = 156; public const int PJ13 = 157; public const int PJ14 = 158; public const int PJ15 = 159;
                public const int PK0 = 160; public const int PK1 = 161; public const int PK2 = 162; public const int PK3 = 163; public const int PK4 = 164; public const int PK5 = 165; public const int PK6 = 166; public const int PK7 = 167;
                public const int PK8 = 168; public const int PK9 = 169; public const int PK10 = 170; public const int PK11 = 171; public const int PK12 = 172; public const int PK13 = 173; public const int PK14 = 174; public const int PK15 = 175;
                public const int PL0 = 176; public const int PL1 = 177; public const int PL2 = 178; public const int PL3 = 179; public const int PL4 = 180; public const int PL5 = 181; public const int PL6 = 182; public const int PL7 = 183;
                public const int PL8 = 184; public const int PL9 = 185; public const int PL10 = 186; public const int PL11 = 187; public const int PL12 = 188; public const int PL13 = 189; public const int PL14 = 190; public const int PL15 = 191;
                public const int PM0 = 192; public const int PM1 = 193; public const int PM2 = 194; public const int PM3 = 195; public const int PM4 = 196; public const int PM5 = 197; public const int PM6 = 198; public const int PM7 = 199;
                public const int PM8 = 200; public const int PM9 = 201; public const int PM10 = 202; public const int PM11 = 203; public const int PM12 = 204; public const int PM13 = 205; public const int PM14 = 206; public const int PM15 = 207;
                public const int PN0 = 208; public const int PN1 = 209; public const int PN2 = 210; public const int PN3 = 211; public const int PN4 = 212; public const int PN5 = 213; public const int PN6 = 214; public const int PN7 = 215;
                public const int PN8 = 216; public const int PN9 = 217; public const int PN10 = 218; public const int PN11 = 219; public const int PN12 = 220; public const int PN13 = 221; public const int PN14 = 222; public const int PN15 = 223;

                /// <summary>No pin</summary>
                public const int NONE = -1;
            }

            /// <summary>
            /// GPIO for .NET's <see cref="GpioController"/> with the EPN256's pin
            /// numbers.
            /// <code>
            /// using var gpio = new GpioController(new EPN256.Gpio.Driver());
            /// gpio.OpenPin(EPN256.Gpio.Pin.PJ14, PinMode.Output);
            /// gpio.Write(EPN256.Gpio.Pin.PJ14, PinValue.High);
            /// </code>
            /// Pins go through the system's GPIO driver (libgpiod), so every
            /// pin mode, pull-up/down and pin change event of .NET's
            /// GpioController works. One Linux gpiochip per port
            /// (A = gpiochip0 ... N = gpiochip13).
            /// </summary>
            public sealed class Driver : GpioDriver {
                private const string Owner = "GPIO";

                private readonly object gate = new();
                // One controller per port (A = 0 ... N = 13), opened when first used
                private readonly Dictionary<int, GpioController> ports = new();
                private readonly HashSet<int> open = new();
                private readonly Dictionary<(int Pin, PinChangeEventHandler Callback), PinChangeEventHandler> callbacks = new();

                protected override int PinCount => Pin.PN15 + 1;

                protected override void OpenPin(int pinNumber) {
                    lock (this.gate) {
                        if (this.open.Contains(pinNumber)) {
                            return;
                        }
                        PinReservations.Reserve(Owner, Check(pinNumber));
                        try {
                            this.Port(pinNumber).OpenPin(pinNumber % 16);
                        }
                        catch {
                            PinReservations.Release(pinNumber);
                            throw;
                        }
                        this.open.Add(pinNumber);
                    }
                }

                protected override void ClosePin(int pinNumber) {
                    lock (this.gate) {
                        if (!this.open.Remove(pinNumber)) {
                            return;
                        }
                        foreach (var key in this.callbacks.Keys.Where(k => k.Pin == pinNumber).ToList()) {
                            this.callbacks.Remove(key);
                        }
                        this.Port(pinNumber).ClosePin(pinNumber % 16);
                        PinReservations.Release(pinNumber);
                    }
                }

                protected override void SetPinMode(int pinNumber, PinMode mode) => this.Port(pinNumber).SetPinMode(pinNumber % 16, mode);

                protected override void SetPinMode(int pinNumber, PinMode mode, PinValue initialValue) {
                    if (mode != PinMode.Output) {
                        this.SetPinMode(pinNumber, mode);
                        return;
                    }
                    // Opening the line as an output with its value avoids a glitch
                    // (setting the mode first would drive it low for a moment)
                    var port = this.Port(pinNumber);
                    port.ClosePin(pinNumber % 16);
                    port.OpenPin(pinNumber % 16, PinMode.Output, initialValue);
                }

                protected override PinMode GetPinMode(int pinNumber) => this.Port(pinNumber).GetPinMode(pinNumber % 16);

                protected override bool IsPinModeSupported(int pinNumber, PinMode mode) =>
                    IsValid(pinNumber) && this.Port(pinNumber).IsPinModeSupported(pinNumber % 16, mode);

                protected override PinValue Read(int pinNumber) => this.Port(pinNumber).Read(pinNumber % 16);

                protected override void Write(int pinNumber, PinValue value) => this.Port(pinNumber).Write(pinNumber % 16, value);

                protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken) =>
                    this.Port(pinNumber).WaitForEvent(pinNumber % 16, eventTypes, cancellationToken);

                protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback) {
                    // The port reports its own line number: give the app the EPN256 pin number
                    PinChangeEventHandler handler = (sender, e) => callback(this, new PinValueChangedEventArgs(e.ChangeType, pinNumber));
                    lock (this.gate) {
                        this.callbacks[(pinNumber, callback)] = handler;
                    }
                    this.Port(pinNumber).RegisterCallbackForPinValueChangedEvent(pinNumber % 16, eventTypes, handler);
                }

                protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback) {
                    PinChangeEventHandler handler;
                    lock (this.gate) {
                        if (!this.callbacks.Remove((pinNumber, callback), out handler)) {
                            return;
                        }
                    }
                    this.Port(pinNumber).UnregisterCallbackForPinValueChangedEvent(pinNumber % 16, handler);
                }

                protected override void Dispose(bool disposing) {
                    if (disposing) {
                        lock (this.gate) {
                            foreach (var port in this.ports.Values) {
                                port.Dispose();
                            }
                            this.ports.Clear();
                            PinReservations.Release(this.open.ToArray());
                            this.open.Clear();
                            this.callbacks.Clear();
                        }
                    }
                    base.Dispose(disposing);
                }

                private GpioController Port(int pinNumber) {
                    // A = /dev/gpiochip0 ... N = /dev/gpiochip13. Same convention
                    // as EPM815; the MA35D1 pinctrl driver registers one chip
                    // per port in device-tree order.
                    var port = Check(pinNumber) / 16;
                    lock (this.gate) {
                        if (!this.ports.TryGetValue(port, out var controller)) {
                            controller = new GpioController(new LibGpiodDriver(port));
                            this.ports[port] = controller;
                        }
                        return controller;
                    }
                }

                private static int Check(int pinNumber) {
                    if (!IsValid(pinNumber)) {
                        throw new ArgumentOutOfRangeException(nameof(pinNumber));
                    }
                    return pinNumber;
                }

                private static bool IsValid(int pinNumber) => pinNumber >= 0 && pinNumber <= Pin.PN15;
            }
        }
    }
}
