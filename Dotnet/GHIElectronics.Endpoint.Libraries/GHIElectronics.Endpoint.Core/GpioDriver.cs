using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        public static partial class Gpio {
            /// <summary>
            /// GPIO for .NET's <see cref="GpioController"/> with the EPM815's pin
            /// numbers, on every port:
            /// <code>
            /// using var gpio = new GpioController(new EPM815.Gpio.Driver());
            /// gpio.OpenPin(EPM815.Gpio.Pin.PC0, PinMode.Output);
            /// gpio.Write(EPM815.Gpio.Pin.PC0, PinValue.High);
            /// </code>
            /// Open pins are reserved, so the other libraries (I2C, UART...) cannot
            /// take them by mistake. The pins go through the kernel (libgpiod), so
            /// every pin mode, pull-up/down and pin change event works as on any
            /// Linux board.
            /// </summary>
            public sealed class Driver : GpioDriver {
                private const string Owner = "GPIO";

                private readonly object gate = new();
                // One controller per port (A = 0 ... Z = 9), opened when first used
                private readonly Dictionary<int, GpioController> ports = new();
                private readonly HashSet<int> open = new();
                private readonly Dictionary<(int Pin, PinChangeEventHandler Callback), PinChangeEventHandler> callbacks = new();

                protected override int PinCount => Pin.PZ7 + 1;

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
                    // The port reports its own line number: give the app the EPM815 pin number
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
                    var port = Check(pinNumber) / 16;
                    lock (this.gate) {
                        if (!this.ports.TryGetValue(port, out var controller)) {
                            controller = new GpioController(new LibGpiodDriver(GpioChips.Find(port)));
                            this.ports[port] = controller;
                        }
                        return controller;
                    }
                }
            }
        }
    }
}
