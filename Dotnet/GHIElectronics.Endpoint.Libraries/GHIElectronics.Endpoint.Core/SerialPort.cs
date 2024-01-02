using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GHIElectronics.Endpoint.Core.EPM815.Gpio;



namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        public static class SerialPort {

            /// <summary>Uart controller.</summary>
            public const string Uart1 = "/dev/ttySTM0";
            public const string Uart2 = "/dev/ttySTM1";
            public const string Uart3 = "/dev/ttySTM2";
            public const string Uart4 = "/dev/ttySTM3";
            public const string Uart5 = "/dev/ttySTM4";
            public const string Uart6 = "/dev/ttySTM5";
            public const string Uart7 = "/dev/ttySTM6";
            public const string Uart8 = "/dev/ttySTM7";

            internal class UartPinSettings {
                public int TxPin { get; set; }
                public int RxPin { get; set; }
                public int RtsPin { get; set; }
                public int CtsPin { get; set; }
                public Alternate TxAlternate { get; set; }
                public Alternate RxAlternate { get; set; }
                public Alternate RtsAlternate { get; set; }
                public Alternate CtsAlternate { get; set; }
            };

            internal static UartPinSettings[] PinSettings = new UartPinSettings[8] {
                /* 1 */ new UartPinSettings { TxPin = Gpio.Pin.PZ7 , RxPin = Gpio.Pin.PZ6 , RtsPin = Gpio.Pin.PA12, CtsPin = Gpio.Pin.PZ3 , TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 2 */ new UartPinSettings { TxPin = Gpio.Pin.PF5 , RxPin = Gpio.Pin.PD6 , RtsPin = Gpio.Pin.PD4 , CtsPin = Gpio.Pin.PD3 , TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 3 */ new UartPinSettings { TxPin = Gpio.Pin.PD8 , RxPin = Gpio.Pin.PD9 , RtsPin = Gpio.Pin.PD12, CtsPin = Gpio.Pin.PD11, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 4 */ new UartPinSettings { TxPin = Gpio.Pin.PD1 , RxPin = Gpio.Pin.PD0 , RtsPin = Gpio.Pin.NONE, CtsPin = Gpio.Pin.NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 5 */ new UartPinSettings { TxPin = Gpio.Pin.PB13, RxPin = Gpio.Pin.PB12, RtsPin = Gpio.Pin.NONE, CtsPin = Gpio.Pin.NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.AF14,  CtsAlternate = Alternate.AF14 },
                /* 6 */ new UartPinSettings { TxPin = Gpio.Pin.NONE, RxPin = Gpio.Pin.NONE, RtsPin = Gpio.Pin.NONE, CtsPin = Gpio.Pin.NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 7 */ new UartPinSettings { TxPin = Gpio.Pin.PE8 , RxPin = Gpio.Pin.PF6 , RtsPin = Gpio.Pin.PE9 , CtsPin = Gpio.Pin.PE10, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 8 */ new UartPinSettings { TxPin = Gpio.Pin.PE1 , RxPin = Gpio.Pin.PE0 , RtsPin = Gpio.Pin.NONE, CtsPin = Gpio.Pin.NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
            };
            public static void Initialize(string portname, bool enableHardwareFlowControl = false) {

                int port;
                try {
                    port = int.Parse("" + portname[portname.Length - 1]);
                }
                catch {
                    throw new ArgumentException("Invalid Serialport.");
                }

                if (port < 0 || port > 7) {
                    throw new ArgumentException("Invalid Serialport.");
                }




                var pinConfig = PinSettings[port];

                if (IsPinReserved(pinConfig.TxPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.TxPin);
                }

                if (IsPinReserved(pinConfig.RxPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.RxPin);
                }

                if (enableHardwareFlowControl) {
                    if (pinConfig.RtsPin == Gpio.Pin.NONE || pinConfig.CtsPin == Gpio.Pin.NONE) {
                        throw new ArgumentException($"Port {port} does not support handshaking.");
                    }

                    if (IsPinReserved(pinConfig.RtsPin)) {
                        EPM815.ThrowExceptionPinInUsed(pinConfig.RtsPin);
                    }

                    if (IsPinReserved(pinConfig.CtsPin)) {
                        EPM815.ThrowExceptionPinInUsed(pinConfig.CtsPin);
                    }

                }

                SetModer(pinConfig.TxPin, Moder.Alternate);
                SetModer(pinConfig.RxPin, Moder.Alternate);

                SetAlternate(pinConfig.TxPin, pinConfig.TxAlternate);
                SetAlternate(pinConfig.RxPin, pinConfig.RxAlternate);

                PinReserve(pinConfig.TxPin);
                PinReserve(pinConfig.RxPin);

                if (enableHardwareFlowControl) {

                    SetModer(pinConfig.CtsPin, Moder.Alternate);
                    SetAlternate(pinConfig.CtsPin, pinConfig.CtsAlternate);

                    SetModer(pinConfig.RtsPin, Moder.Alternate);
                    SetAlternate(pinConfig.RtsPin, pinConfig.RtsAlternate);

                    PinReserve(pinConfig.CtsPin);
                    PinReserve(pinConfig.RtsPin);

                }
            }

            public static void UnInitialize(string portname, bool enabledHardwareFlowControl) {


                int port;
                try {
                    port = int.Parse("" + portname[portname.Length - 1]);
                }
                catch {
                    throw new ArgumentException("Invalid Serialport.");
                }

                if (port < 1 || port > 8) {
                    throw new ArgumentException("Invalid Serial port.");
                }

                port = port - 1;


                var pinConfig = PinSettings[port];



                SetModer(pinConfig.TxPin, Moder.Input);
                SetModer(pinConfig.RxPin, Moder.Input);

                PinRelease(pinConfig.TxPin);
                PinRelease(pinConfig.RxPin);

                if (enabledHardwareFlowControl) {

                    SetModer(pinConfig.CtsPin, Moder.Input);
                    SetModer(pinConfig.RtsPin, Moder.Input);

                    PinRelease(pinConfig.CtsPin);
                    PinRelease(pinConfig.RtsPin);
                }
            }

        }
    }
}

