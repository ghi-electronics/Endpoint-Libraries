using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static GHIElectronics.Endpoint.Core.Gpio;

namespace GHIElectronics.Endpoint.Core {

    public static partial class Configuration {

        public static class PwmPin {
            public const int PA8 = Gpio.PA8;

            public const int PE11 = Gpio.PE11;
            public const int PA10 = Gpio.PA10;
            public const int PA11 = Gpio.PA11;

            //controller2
            public const int PA15 = Gpio.PA15;
            public const int PB3 = Gpio.PB3;
            public const int PA3 = Gpio.PA3;

            //controller3
            public const int PC6 = Gpio.PC6;
            public const int PB5 = Gpio.PB5;
            public const int PB0 = Gpio.PB0;

            //controller4
            public const int PD12 = Gpio.PD12;
            public const int PB7 = Gpio.PB7;
            public const int PD14 = Gpio.PD14;
            public const int PD15 = Gpio.PD15;

            //controller5
            public const int PA0 = Gpio.PA0;
            public const int PH11 = Gpio.PH11;
            public const int PH12 = Gpio.PH12;
            public const int PI0 = Gpio.PI0;

            //controller8
            public const int PI5 = Gpio.PI5;
            public const int PI6 = Gpio.PI6;
            public const int PI7 = Gpio.PI7;
            public const int PI2 = Gpio.PI2;

            //controller12
            public const int PH6 = Gpio.PH6;
            public const int PH9 = Gpio.PH9;

            //controller13
            public const int PA6 = Gpio.PA6;

            //controller14
            public const int PF9 = Gpio.PF9;

            //controller15
            public const int PE5 = Gpio.PE5;
            public const int PE6 = Gpio.PE6;

            //Controller16
            public const int PB8 = Gpio.PB8;

            //Controller17
            public const int PB9 = Gpio.PB9;
        }
        public static class Pwm {

            
            internal static int ToActualController(int id) {
                switch (id) {
                    case 2 - 1: return 0;
                    case 3 - 1: return 4;
                    case 4 - 1: return 8;
                    case 5 - 1: return 12;
                    case 12 - 1: return 16;
                    case 13 - 1: return 18;
                    case 14 - 1: return 19;
                    case 1 - 1: return 20;
                    case 8 - 1: return 24;
                    case 15 - 1: return 28;
                    case 16 - 1: return 30;
                    case 17 - 1: return 31;

                }
                throw new Exception(string.Format("The controller {0} is not available.", id));

            }

            internal static class Controller1 {
                public const int PA8 = (0x0000 << 16) | (Gpio.PA8 << 8) | ((int)Alternate.AF1 << 0);
                public const int PE11 = (0x0001 << 16) | (Gpio.PE11 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA10 = (0x0002 << 16) | (Gpio.PA10 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA11 = (0x0003 << 16) | (Gpio.PA11 << 8) | ((int)Alternate.AF1 << 0);
            }

            internal static class Controller2 {
                public const int PA15 = (0x0100 << 16) | (Gpio.PA15 << 8) | ((int)Alternate.AF1 << 0);
                public const int PB3 = (0x0101 << 16) | (Gpio.PB3 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA3 = (0x0103 << 16) | (Gpio.PA3 << 8) | ((int)Alternate.AF1 << 0);
            }

            internal static class Controller3 {
                public const int PC6 = (0x0200 << 16) | (Gpio.PC6 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB5 = (0x0201 << 16) | (Gpio.PB5 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB0 = (0x0202 << 16) | (Gpio.PB0 << 8) | ((int)Alternate.AF2 << 0);
            }

            internal static class Controller4 {
                public const int PD12 = (0x0300 << 16) | (Gpio.PD12 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB7 = (0x0301 << 16) | (Gpio.PB7 << 8) | ((int)Alternate.AF2 << 0);
                public const int PD14 = (0x0302 << 16) | (Gpio.PD14 << 8) | ((int)Alternate.AF2 << 0);
                public const int PD15 = (0x0303 << 16) | (Gpio.PD15 << 8) | ((int)Alternate.AF2 << 0);
            }

            internal static class Controller5 {
                public const int PA0 = (0x0400 << 16) | (Gpio.PA0 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH11 = (0x0401 << 16) | (Gpio.PH11 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH12 = (0x0402 << 16) | (Gpio.PH12 << 8) | ((int)Alternate.AF2 << 0);
                public const int PI0 = (0x0403 << 16) | (Gpio.PI0 << 8) | ((int)Alternate.AF2 << 0);
            }

            internal static class Controller8 {
                public const int PI5 = (0x0700 << 16) | (Gpio.PI5 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI6 = (0x0701 << 16) | (Gpio.PI6 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI7 = (0x0702 << 16) | (Gpio.PI7 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI2 = (0x0703 << 16) | (Gpio.PI2 << 8) | ((int)Alternate.AF3 << 0);
            }

            internal static class Controller12 {
                public const int PH6 = (0x0B00 << 16) | (Gpio.PH6 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH9 = (0x0B01 << 16) | (Gpio.PH9 << 8) | ((int)Alternate.AF2 << 0);
            }

            internal static class Controller13 {
                public const int PA6 = (0x0C00 << 16) | (Gpio.PA6 << 8) | ((int)Alternate.AF9 << 0);
            }

            internal static class Controller14 {
                public const int PF9 = (0x0D00 << 16) | (Gpio.PF9 << 8) | ((int)Alternate.AF9 << 0);
            }

            internal static class Controller15 {
                public const int PE5 = (0x0E00 << 16) | (Gpio.PE5 << 8) | ((int)Alternate.AF4 << 0);
                public const int PE6 = (0x0E01 << 16) | (Gpio.PE6 << 8) | ((int)Alternate.AF4 << 0);
            }

            internal static class Controller16 {
                public const int PB8 = (0x0F00 << 16) | (Gpio.PB8 << 8) | ((int)Alternate.AF1 << 0);
            }

            internal static class Controller17 {
                public const int PB9 = (0x1000 << 16) | (Gpio.PB9 << 8) | ((int)Alternate.AF1 << 0);
            }

            internal static int GetPinEncodeFromPin(int pin) {

                switch (pin) {
                    //controller1
                    case PA8: return Controller1.PA8;
                    case PE11: return Controller1.PE11;
                    case PA10: return Controller1.PA10;
                    case PA11: return Controller1.PA11;

                    //controller2
                    case PA15: return Controller2.PA15;
                    case PB3: return Controller2.PB3;
                    case PA3: return Controller2.PA3;

                    //controller3
                    case PC6: return Controller3.PC6;
                    case PB5: return Controller3.PB5;
                    case PB0: return Controller3.PB0;

                    //controller4
                    case PD12: return Controller4.PD12;
                    case PB7: return Controller4.PB7;
                    case PD14: return Controller4.PD14;
                    case PD15: return Controller4.PD15;

                    //controller5
                    case PA0: return Controller5.PA0;
                    case PH11: return Controller5.PH11;
                    case PH12: return Controller5.PH12;
                    case PI0: return Controller5.PI0;

                    //controller8
                    case PI5: return Controller8.PI5;
                    case PI6: return Controller8.PI6;
                    case PI7: return Controller8.PI7;
                    case PI2: return Controller8.PI2;

                    //controller12
                    case PH6: return Controller12.PH6;
                    case PH9: return Controller12.PH9;

                    //controller13
                    case PA6: return Controller13.PA6;

                    //controller14
                    case PF9: return Controller14.PF9;

                    //controller15
                    case PE5: return Controller15.PE5;
                    case PE6: return Controller15.PE6;

                    //Controller16
                    case PB8: return Controller16.PB8;

                    //Controller17
                    case PB9: return Controller17.PB9;

                    default: return NONE;



                }

            }



            public static int GetChipId(int pin) {
                var pwm_pin = GetPinEncodeFromPin(pin);

                var controllerId = (pwm_pin >> 24) & 0xFF;

                return ToActualController(controllerId);
            }

            public static int GetChannelId(int pin) {
                var pwm_pin = GetPinEncodeFromPin(pin);

                var channelId = (pwm_pin >> 16) & 0xFF;

                return channelId;
            }

            public static void Initialize(int pin) {
                var pwm_pin = GetPinEncodeFromPin(pin);

                if (pwm_pin == NONE)
                    throw new Exception($"Pin {pin} does not support pwm.");



                //var controllerId = (pwm_pin >> 24) & 0xFF;
                //var channelId = (pwm_pin >> 16) & 0xFF;
                var pinId = (pwm_pin >> 8) & 0xFF;
                var alternateId = (pwm_pin >> 0) & 0xFF;



                SetModer(pinId, Moder.Alternate);
                SetAlternate(pinId, (Alternate)alternateId);

                PinReserve(pinId);
            }

            public static void UnInitialize(int pin) {
                var pwm_pin = GetPinEncodeFromPin(pin);

                if (pwm_pin == NONE)
                    throw new Exception($"The pin {pin} does not support pwm.");

                var pinId = (pwm_pin >> 8) & 0xFF;
                var alternateId = (pwm_pin >> 0) & 0xFF;

                Gpio.SetModer(pinId, Moder.Input);

                PinRelease(pinId);

            }

        }
    }

}
