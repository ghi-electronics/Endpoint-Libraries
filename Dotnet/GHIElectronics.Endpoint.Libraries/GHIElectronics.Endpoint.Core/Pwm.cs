using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// PWM pins, for .NET's System.Device.Pwm. Initialize connects the pin
        /// to its timer; GetChipId and GetChannelId give what PwmChannel needs:
        /// <code>
        /// EPM815.Pwm.Initialize(EPM815.Pwm.Pin.PA8);
        /// using var pwm = PwmChannel.Create(EPM815.Pwm.GetChipId(EPM815.Pwm.Pin.PA8),
        ///     EPM815.Pwm.GetChannelId(EPM815.Pwm.Pin.PA8), 1000, 0.5);
        /// pwm.Start();
        /// </code>
        /// Channels of one timer share its frequency.
        /// </summary>
        public static class Pwm {
            /// <summary>The pins with PWM (the timer and channel are in the comments)</summary>
            public static class Pin {
                /// <summary>TIM1 channel 1</summary>
                public const int PA8 = Gpio.Pin.PA8;
                /// <summary>TIM1 channel 2</summary>
                public const int PE11 = Gpio.Pin.PE11;
                /// <summary>TIM1 channel 3</summary>
                public const int PA10 = Gpio.Pin.PA10;
                /// <summary>TIM1 channel 4</summary>
                public const int PA11 = Gpio.Pin.PA11;
                /// <summary>TIM2 channel 1</summary>
                public const int PA15 = Gpio.Pin.PA15;
                /// <summary>TIM2 channel 2</summary>
                public const int PB3 = Gpio.Pin.PB3;
                /// <summary>TIM2 channel 4</summary>
                public const int PA3 = Gpio.Pin.PA3;
                /// <summary>TIM3 channel 1</summary>
                public const int PC6 = Gpio.Pin.PC6;
                /// <summary>TIM3 channel 2</summary>
                public const int PB5 = Gpio.Pin.PB5;
                /// <summary>TIM3 channel 3</summary>
                public const int PB0 = Gpio.Pin.PB0;
                /// <summary>TIM4 channel 1</summary>
                public const int PD12 = Gpio.Pin.PD12;
                /// <summary>TIM4 channel 2</summary>
                public const int PB7 = Gpio.Pin.PB7;
                /// <summary>TIM4 channel 3</summary>
                public const int PD14 = Gpio.Pin.PD14;
                /// <summary>TIM4 channel 4</summary>
                public const int PD15 = Gpio.Pin.PD15;
                /// <summary>TIM5 channel 1</summary>
                public const int PA0 = Gpio.Pin.PA0;
                /// <summary>TIM5 channel 2</summary>
                public const int PH11 = Gpio.Pin.PH11;
                /// <summary>TIM5 channel 3</summary>
                public const int PH12 = Gpio.Pin.PH12;
                /// <summary>TIM5 channel 4</summary>
                public const int PI0 = Gpio.Pin.PI0;
                /// <summary>TIM8 channel 1</summary>
                public const int PI5 = Gpio.Pin.PI5;
                /// <summary>TIM8 channel 2</summary>
                public const int PI6 = Gpio.Pin.PI6;
                /// <summary>TIM8 channel 3</summary>
                public const int PI7 = Gpio.Pin.PI7;
                /// <summary>TIM8 channel 4</summary>
                public const int PI2 = Gpio.Pin.PI2;
                /// <summary>TIM12 channel 1</summary>
                public const int PH6 = Gpio.Pin.PH6;
                /// <summary>TIM12 channel 2</summary>
                public const int PH9 = Gpio.Pin.PH9;
                /// <summary>TIM13 channel 1</summary>
                public const int PA6 = Gpio.Pin.PA6;
                /// <summary>TIM14 channel 1</summary>
                public const int PF9 = Gpio.Pin.PF9;
                /// <summary>TIM15 channel 1</summary>
                public const int PE5 = Gpio.Pin.PE5;
                /// <summary>TIM15 channel 2</summary>
                public const int PE6 = Gpio.Pin.PE6;
                /// <summary>TIM16 channel 1</summary>
                public const int PB8 = Gpio.Pin.PB8;
                /// <summary>TIM17 channel 1</summary>
                public const int PB9 = Gpio.Pin.PB9;
            }

            // Channel: 0 = the timer's channel 1 (the system counts from 0)
            private sealed record Output(int Pin, int Timer, int Channel, int Alternate);

            private static readonly Output[] Outputs = {
                new(Pin.PA8, 1, 0, 1), new(Pin.PE11, 1, 1, 1), new(Pin.PA10, 1, 2, 1), new(Pin.PA11, 1, 3, 1),
                new(Pin.PA15, 2, 0, 1), new(Pin.PB3, 2, 1, 1), new(Pin.PA3, 2, 3, 1),
                new(Pin.PC6, 3, 0, 2), new(Pin.PB5, 3, 1, 2), new(Pin.PB0, 3, 2, 2),
                new(Pin.PD12, 4, 0, 2), new(Pin.PB7, 4, 1, 2), new(Pin.PD14, 4, 2, 2), new(Pin.PD15, 4, 3, 2),
                new(Pin.PA0, 5, 0, 2), new(Pin.PH11, 5, 1, 2), new(Pin.PH12, 5, 2, 2), new(Pin.PI0, 5, 3, 2),
                new(Pin.PI5, 8, 0, 3), new(Pin.PI6, 8, 1, 3), new(Pin.PI7, 8, 2, 3), new(Pin.PI2, 8, 3, 3),
                new(Pin.PH6, 12, 0, 2), new(Pin.PH9, 12, 1, 2),
                new(Pin.PA6, 13, 0, 9),
                new(Pin.PF9, 14, 0, 9),
                new(Pin.PE5, 15, 0, 4), new(Pin.PE6, 15, 1, 4),
                new(Pin.PB8, 16, 0, 1),
                new(Pin.PB9, 17, 0, 1),
            };

            private static readonly Dictionary<int, ulong> TimerAddress = new() {
                [1] = 0x44000000, [2] = 0x40000000, [3] = 0x40001000, [4] = 0x40002000,
                [5] = 0x40003000, [8] = 0x44001000, [12] = 0x40006000, [13] = 0x40007000,
                [14] = 0x40008000, [15] = 0x44006000, [16] = 0x44007000, [17] = 0x44008000,
            };

            /// <summary>Connects the pin to its timer</summary>
            public static void Initialize(int pin) {
                var output = Get(pin);
                PeripheralPins.Connect(Owner(pin), new PinFunction(pin, output.Alternate));
            }

            /// <summary>Gives the pin back, as it was before Initialize</summary>
            public static void UnInitialize(int pin) {
                Get(pin);
                PeripheralPins.Disconnect(Owner(pin));
            }

            /// <summary>The PWM chip of the pin's timer (/sys/class/pwm/pwmchipN), for PwmChannel.Create</summary>
            public static int GetChipId(int pin) {
                var timer = Get(pin).Timer;
                var chip = EndpointDevices.Find("pwm", "pwmchip", TimerAddress[timer]);
                return chip >= 0 ? chip : throw new InvalidOperationException($"TIM{timer}'s PWM is not enabled in this firmware");
            }

            /// <summary>The pin's channel in its PWM chip, for PwmChannel.Create</summary>
            public static int GetChannelId(int pin) => Get(pin).Channel;

            private static string Owner(int pin) => "PWM " + Gpio.Name(pin);

            private static Output Get(int pin) =>
                Outputs.FirstOrDefault(o => o.Pin == pin)
                ?? throw new ArgumentException($"Pin {Gpio.Name(pin)} has no PWM", nameof(pin));
        }
    }
}
