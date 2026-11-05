using System.Diagnostics;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// PWM pins bundled with EPN256, for .NET's <c>System.Device.Pwm</c>.
        /// MA35D1 has three EPWM controllers (EPWM0 / EPWM1 / EPWM2), each
        /// exposing six channels (CH0..CH5) that share the controller's
        /// frequency. <see cref="Initialize"/> connects a pin to its
        /// controller + channel; <see cref="GetChipId"/> and
        /// <see cref="GetChannelId"/> give what <c>PwmChannel.Create</c>
        /// needs. Pay-for-use: initializing one pin claims only that pin.
        /// </summary>
        /// <example>
        /// <code>
        /// EPN256.Pwm.Initialize(EPN256.Pwm.Pin.PK5);
        /// using var pwm = PwmChannel.Create(
        ///     EPN256.Pwm.GetChipId(EPN256.Pwm.Pin.PK5),
        ///     EPN256.Pwm.GetChannelId(EPN256.Pwm.Pin.PK5),
        ///     1000,
        ///     0.5);
        /// pwm.Start();
        /// </code>
        /// </example>
        public static class Pwm {
            /// <summary>The pins with PWM (the controller and channel are in the comments).
            /// One representative pin per (controller, channel); if your board wires a
            /// different pin alternate, request it and we'll add it.</summary>
            public static class Pin {
                /// <summary>EPWM0 channel 0</summary>
                public const int PD6  = Gpio.Pin.PD6;
                /// <summary>EPWM0 channel 1</summary>
                public const int PD7  = Gpio.Pin.PD7;
                /// <summary>EPWM0 channel 2</summary>
                public const int PD8  = Gpio.Pin.PD8;
                /// <summary>EPWM0 channel 3</summary>
                public const int PD9  = Gpio.Pin.PD9;
                /// <summary>EPWM0 channel 4</summary>
                public const int PD10 = Gpio.Pin.PD10;
                /// <summary>EPWM0 channel 5</summary>
                public const int PD11 = Gpio.Pin.PD11;
                /// <summary>EPWM1 channel 0</summary>
                public const int PG4  = Gpio.Pin.PG4;
                /// <summary>EPWM1 channel 1 — the EPN256 reference panel's backlight pin.</summary>
                public const int PK5  = Gpio.Pin.PK5;
                /// <summary>EPWM1 channel 2</summary>
                public const int PG6  = Gpio.Pin.PG6;
                /// <summary>EPWM1 channel 3</summary>
                public const int PG7  = Gpio.Pin.PG7;
                /// <summary>EPWM1 channel 4</summary>
                public const int PG8  = Gpio.Pin.PG8;
                /// <summary>EPWM1 channel 5</summary>
                public const int PM13 = Gpio.Pin.PM13;
                /// <summary>EPWM2 channel 0</summary>
                public const int PC8  = Gpio.Pin.PC8;
                /// <summary>EPWM2 channel 1</summary>
                public const int PC9  = Gpio.Pin.PC9;
                /// <summary>EPWM2 channel 2</summary>
                public const int PC10 = Gpio.Pin.PC10;
                /// <summary>EPWM2 channel 3</summary>
                public const int PC11 = Gpio.Pin.PC11;
                /// <summary>EPWM2 channel 4</summary>
                public const int PB9  = Gpio.Pin.PB9;
                /// <summary>EPWM2 channel 5</summary>
                public const int PB10 = Gpio.Pin.PB10;
            }

            /// <summary>One PWM pin and how it maps: (pin, controller 0..2, channel 0..5, MFP alternate).</summary>
            private record Output(int Pin, int Controller, int Channel, int Mfp);

            // MFP values are from include/dt-bindings/pinctrl/ma35d1-pinfunc.h.
            // PD6..PD11 share MFP 0x8; everything else here is MFP 0x1.
            private static readonly Output[] Outputs = {
                new(Pin.PD6,  0, 0, 0x8), new(Pin.PD7,  0, 1, 0x8), new(Pin.PD8,  0, 2, 0x8),
                new(Pin.PD9,  0, 3, 0x8), new(Pin.PD10, 0, 4, 0x8), new(Pin.PD11, 0, 5, 0x8),

                new(Pin.PG4,  1, 0, 0x1), new(Pin.PK5,  1, 1, 0x1), new(Pin.PG6,  1, 2, 0x1),
                new(Pin.PG7,  1, 3, 0x1), new(Pin.PG8,  1, 4, 0x1), new(Pin.PM13, 1, 5, 0x1),

                new(Pin.PC8,  2, 0, 0x1), new(Pin.PC9,  2, 1, 0x1), new(Pin.PC10, 2, 2, 0x1),
                new(Pin.PC11, 2, 3, 0x1), new(Pin.PB9,  2, 4, 0x1), new(Pin.PB10, 2, 5, 0x1),
            };

            // MMIO base per EPWM controller (arch/arm64/.../ma35d1.dtsi).
            private static readonly ulong[] ControllerAddress = {
                0x40580000, // EPWM0
                0x40590000, // EPWM1
                0x405A0000, // EPWM2
            };

            private const string ModuleName = "pwm-ma35d1";

            /// <summary>
            /// Loads the EPWM driver (so <c>/sys/class/pwm/pwmchipN</c> appears
            /// for the pin's controller) and mux's the pin to its channel.
            /// </summary>
            public static void Initialize(int pin) {
                var o = Get(pin);
                EnsureDriverLoaded();
                // Wait briefly for the pwmchipN node that corresponds to this controller.
                for (int i = 0; i < 50; i++) {
                    if (EndpointDevices.Find("pwm", "pwmchip", ControllerAddress[o.Controller]) >= 0) break;
                    Thread.Sleep(20);
                }
                if (EndpointDevices.Find("pwm", "pwmchip", ControllerAddress[o.Controller]) < 0) {
                    throw new InvalidOperationException(
                        $"EPWM driver loaded but no pwmchip matched EPWM{o.Controller} within 1 s.");
                }
                PeripheralPins.Connect(Owner(pin), new PinFunction(pin, o.Mfp));
            }

            /// <summary>Gives the pin back as free GPIO. Reverses <see cref="Initialize"/>.</summary>
            public static void UnInitialize(int pin) {
                Get(pin);
                PeripheralPins.Disconnect(Owner(pin));
            }

            /// <summary>The PWM chip of the pin's controller (<c>/sys/class/pwm/pwmchipN</c>),
            /// for <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.
            /// Only valid after <see cref="Initialize"/>.</summary>
            public static int GetChipId(int pin) {
                var o = Get(pin);
                var chip = EndpointDevices.Find("pwm", "pwmchip", ControllerAddress[o.Controller]);
                return chip >= 0 ? chip : throw new InvalidOperationException(
                    $"EPWM{o.Controller} is not enabled in this firmware; call Initialize first.");
            }

            /// <summary>The pin's channel in its PWM chip, for
            /// <see cref="System.Device.Pwm.PwmChannel.Create(int, int, int, double)"/>.</summary>
            public static int GetChannelId(int pin) => Get(pin).Channel;

            private static string Owner(int pin) => $"PWM pin {pin}";

            private static Output Get(int pin) =>
                Outputs.FirstOrDefault(o => o.Pin == pin)
                ?? throw new ArgumentException($"Pin {pin} has no PWM on EPN256", nameof(pin));

            // =====================================================================
            // Driver load helper. Module stays loaded for the life of the process;
            // the kernel keeps per-controller bindings, we keep per-pin pin
            // reservations via PeripheralPins.
            // =====================================================================

            private static readonly object driverGate = new();
            private static bool driverLoaded;

            private static void EnsureDriverLoaded() {
                lock (driverGate) {
                    if (driverLoaded) return;
                    var psi = new ProcessStartInfo("/sbin/modprobe", ModuleName) {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };
                    using var p = Process.Start(psi)
                        ?? throw new InvalidOperationException($"Failed to start /sbin/modprobe {ModuleName}");
                    p.WaitForExit();
                    if (p.ExitCode != 0) {
                        throw new InvalidOperationException(
                            $"modprobe {ModuleName} failed: {p.StandardError.ReadToEnd().Trim()}");
                    }
                    driverLoaded = true;
                }
            }
        }
    }
}
