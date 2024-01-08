using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;


namespace GHIElectronics.Endpoint.Core {

    public static partial class EPM815 {
        public static class Pwm {

            public static class Pin {
                public const int PA8 = Gpio.Pin.PA8;

                public const int PE11 = Gpio.Pin.PE11;
                public const int PA10 = Gpio.Pin.PA10;
                public const int PA11 = Gpio.Pin.PA11;

                //controller2
                public const int PA15 = Gpio.Pin.PA15;
                public const int PB3 = Gpio.Pin.PB3;
                public const int PA3 = Gpio.Pin.PA3;

                //controller3
                public const int PC6 = Gpio.Pin.PC6;
                public const int PB5 = Gpio.Pin.PB5;
                public const int PB0 = Gpio.Pin.PB0;

                //controller4
                public const int PD12 = Gpio.Pin.PD12;
                public const int PB7 = Gpio.Pin.PB7;
                public const int PD14 = Gpio.Pin.PD14;
                public const int PD15 = Gpio.Pin.PD15;

                //controller5
                public const int PA0 = Gpio.Pin.PA0;
                public const int PH11 = Gpio.Pin.PH11;
                public const int PH12 = Gpio.Pin.PH12;
                public const int PI0 = Gpio.Pin.PI0;

                //controller8
                public const int PI5 = Gpio.Pin.PI5;
                public const int PI6 = Gpio.Pin.PI6;
                public const int PI7 = Gpio.Pin.PI7;
                public const int PI2 = Gpio.Pin.PI2;

                //controller12
                public const int PH6 = Gpio.Pin.PH6;
                public const int PH9 = Gpio.Pin.PH9;

                //controller13
                public const int PA6 = Gpio.Pin.PA6;

                //controller14
                public const int PF9 = Gpio.Pin.PF9;

                //controller15
                public const int PE5 = Gpio.Pin.PE5;
                public const int PE6 = Gpio.Pin.PE6;

                //Controller16
                public const int PB8 = Gpio.Pin.PB8;

                //Controller17
                public const int PB9 = Gpio.Pin.PB9;
            }
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
                public const int PA8 = (0x0000 << 16) | (Gpio.Pin.PA8 << 8) | ((int)Gpio.Alternate.AF1 << 0);
                public const int PE11 = (0x0001 << 16) | (Gpio.Pin.PE11 << 8) | ((int)Gpio.Alternate.AF1 << 0);
                public const int PA10 = (0x0002 << 16) | (Gpio.Pin.PA10 << 8) | ((int)Gpio.Alternate.AF1 << 0);
                public const int PA11 = (0x0003 << 16) | (Gpio.Pin.PA11 << 8) | ((int)Gpio.Alternate.AF1 << 0);
            }

            internal static class Controller2 {
                public const int PA15 = (0x0100 << 16) | (Gpio.Pin.PA15 << 8) | ((int)Gpio.Alternate.AF1 << 0);
                public const int PB3 = (0x0101 << 16) | (Gpio.Pin.PB3 << 8) | ((int)Gpio.Alternate.AF1 << 0);
                public const int PA3 = (0x0103 << 16) | (Gpio.Pin.PA3 << 8) | ((int)Gpio.Alternate.AF1 << 0);
            }

            internal static class Controller3 {
                public const int PC6 = (0x0200 << 16) | (Gpio.Pin.PC6 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PB5 = (0x0201 << 16) | (Gpio.Pin.PB5 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PB0 = (0x0202 << 16) | (Gpio.Pin.PB0 << 8) | ((int)Gpio.Alternate.AF2 << 0);
            }

            internal static class Controller4 {
                public const int PD12 = (0x0300 << 16) | (Gpio.Pin.PD12 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PB7 = (0x0301 << 16) | (Gpio.Pin.PB7 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PD14 = (0x0302 << 16) | (Gpio.Pin.PD14 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PD15 = (0x0303 << 16) | (Gpio.Pin.PD15 << 8) | ((int)Gpio.Alternate.AF2 << 0);
            }

            internal static class Controller5 {
                public const int PA0 = (0x0400 << 16) | (Gpio.Pin.PA0 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PH11 = (0x0401 << 16) | (Gpio.Pin.PH11 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PH12 = (0x0402 << 16) | (Gpio.Pin.PH12 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PI0 = (0x0403 << 16) | (Gpio.Pin.PI0 << 8) | ((int)Gpio.Alternate.AF2 << 0);
            }

            internal static class Controller8 {
                public const int PI5 = (0x0700 << 16) | (Gpio.Pin.PI5 << 8) | ((int)Gpio.Alternate.AF3 << 0);
                public const int PI6 = (0x0701 << 16) | (Gpio.Pin.PI6 << 8) | ((int)Gpio.Alternate.AF3 << 0);
                public const int PI7 = (0x0702 << 16) | (Gpio.Pin.PI7 << 8) | ((int)Gpio.Alternate.AF3 << 0);
                public const int PI2 = (0x0703 << 16) | (Gpio.Pin.PI2 << 8) | ((int)Gpio.Alternate.AF3 << 0);
            }

            internal static class Controller12 {
                public const int PH6 = (0x0B00 << 16) | (Gpio.Pin.PH6 << 8) | ((int)Gpio.Alternate.AF2 << 0);
                public const int PH9 = (0x0B01 << 16) | (Gpio.Pin.PH9 << 8) | ((int)Gpio.Alternate.AF2 << 0);
            }

            internal static class Controller13 {
                public const int PA6 = (0x0C00 << 16) | (Gpio.Pin.PA6 << 8) | ((int)Gpio.Alternate.AF9 << 0);
            }

            internal static class Controller14 {
                public const int PF9 = (0x0D00 << 16) | (Gpio.Pin.PF9 << 8) | ((int)Gpio.Alternate.AF9 << 0);
            }

            internal static class Controller15 {
                public const int PE5 = (0x0E00 << 16) | (Gpio.Pin.PE5 << 8) | ((int)Gpio.Alternate.AF4 << 0);
                public const int PE6 = (0x0E01 << 16) | (Gpio.Pin.PE6 << 8) | ((int)Gpio.Alternate.AF4 << 0);
            }

            internal static class Controller16 {
                public const int PB8 = (0x0F00 << 16) | (Gpio.Pin.PB8 << 8) | ((int)Gpio.Alternate.AF1 << 0);
            }

            internal static class Controller17 {
                public const int PB9 = (0x1000 << 16) | (Gpio.Pin.PB9 << 8) | ((int)Gpio.Alternate.AF1 << 0);
            }

            internal static int GetPinEncodeFromPin(int pin) {

                switch (pin) {
                    //controller1
                    case Gpio.Pin.PA8: return Controller1.PA8;
                    case Gpio.Pin.PE11: return Controller1.PE11;
                    case Gpio.Pin.PA10: return Controller1.PA10;
                    case Gpio.Pin.PA11: return Controller1.PA11;

                    //controller2
                    case Gpio.Pin.PA15: return Controller2.PA15;
                    case Gpio.Pin.PB3: return Controller2.PB3;
                    case Gpio.Pin.PA3: return Controller2.PA3;

                    //controller3
                    case Gpio.Pin.PC6: return Controller3.PC6;
                    case Gpio.Pin.PB5: return Controller3.PB5;
                    case Gpio.Pin.PB0: return Controller3.PB0;

                    //controller4
                    case Gpio.Pin.PD12: return Controller4.PD12;
                    case Gpio.Pin.PB7: return Controller4.PB7;
                    case Gpio.Pin.PD14: return Controller4.PD14;
                    case Gpio.Pin.PD15: return Controller4.PD15;

                    //controller5
                    case Gpio.Pin.PA0: return Controller5.PA0;
                    case Gpio.Pin.PH11: return Controller5.PH11;
                    case Gpio.Pin.PH12: return Controller5.PH12;
                    case Gpio.Pin.PI0: return Controller5.PI0;

                    //controller8
                    case Gpio.Pin.PI5: return Controller8.PI5;
                    case Gpio.Pin.PI6: return Controller8.PI6;
                    case Gpio.Pin.PI7: return Controller8.PI7;
                    case Gpio.Pin.PI2: return Controller8.PI2;

                    //controller12
                    case Gpio.Pin.PH6: return Controller12.PH6;
                    case Gpio.Pin.PH9: return Controller12.PH9;

                    //controller13
                    case Gpio.Pin.PA6: return Controller13.PA6;

                    //controller14
                    case Gpio.Pin.PF9: return Controller14.PF9;

                    //controller15
                    case Gpio.Pin.PE5: return Controller15.PE5;
                    case Gpio.Pin.PE6: return Controller15.PE6;

                    //Controller16
                    case Gpio.Pin.PB8: return Controller16.PB8;

                    //Controller17
                    case Gpio.Pin.PB9: return Controller17.PB9;

                    default: return Gpio.Pin.NONE;



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

            private static bool initialized = false;
            public static void Initialize(int pin) {

                if (initialized)
                    return;

                if (Gpio.IsPinReserved(pin)) {
                    EPM815.ThrowExceptionPinInUsed(pin);
                }



                var pwm_pin = GetPinEncodeFromPin(pin);

                if (pwm_pin == Gpio.Pin.NONE)
                    throw new Exception($"Pin {pin} does not support pwm.");



                //var controllerId = (pwm_pin >> 24) & 0xFF;
                //var channelId = (pwm_pin >> 16) & 0xFF;
                var pinId = (pwm_pin >> 8) & 0xFF;
                var alternateId = (pwm_pin >> 0) & 0xFF;



                Gpio.SetModer(pinId, Gpio.Moder.Alternate);
                Gpio.SetAlternate(pinId, (Gpio.Alternate)alternateId);

                Gpio.PinReserve(pinId);

                initialized = true;
            }

            public static void UnInitialize(int pin) {
                if (initialized) {
                    var pwm_pin = GetPinEncodeFromPin(pin);

                    if (pwm_pin == Gpio.Pin.NONE)
                        throw new Exception($"The pin {pin} does not support pwm.");

                    var pinId = (pwm_pin >> 8) & 0xFF;
                    var alternateId = (pwm_pin >> 0) & 0xFF;

                    Gpio.SetModer(pinId, Gpio.Moder.Input);

                    Gpio.PinRelease(pinId);

                    initialized = false;
                }

            }

        }
    }
}


