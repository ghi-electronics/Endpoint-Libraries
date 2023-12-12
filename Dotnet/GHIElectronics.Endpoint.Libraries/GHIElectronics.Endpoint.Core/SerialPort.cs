using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core.STM32MP1;
using static GHIElectronics.Endpoint.Core.Configuration;
using static GHIElectronics.Endpoint.Core.STM32MP1.GpioPin;

namespace GHIElectronics.Endpoint.Core {
    public static partial class Configuration {
        public static class SerialPort {

            /// <summary>Uart controller.</summary>
            //public static string Uart1 = "/dev/ttySTM0";
            //public static string Uart2 = "/dev/ttySTM1";
            //public static string Uart3 = "/dev/ttySTM2";
            //public static string Uart4 = "/dev/ttySTM3";
            //public static string Uart5 = "/dev/ttySTM4";
            //public static string Uart6 = "/dev/ttySTM5";
            //public static string Uart7 = "/dev/ttySTM6";
            //public static string Uart8 = "/dev/ttySTM7";

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
                /* 1 */ new UartPinSettings { TxPin = PZ7 , RxPin = PZ6 , RtsPin = PA12, CtsPin = PZ3 , TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 2 */ new UartPinSettings { TxPin = PF5 , RxPin = PD6 , RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 3 */ new UartPinSettings { TxPin = PD8 , RxPin = PD9 , RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 4 */ new UartPinSettings { TxPin = PD1 , RxPin = PD0 , RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 5 */ new UartPinSettings { TxPin = PB13, RxPin = PB12, RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.AF14,  CtsAlternate = Alternate.AF14 },
                /* 6 */ new UartPinSettings { TxPin = NONE, RxPin = NONE, RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 7 */ new UartPinSettings { TxPin = PE8 , RxPin = PF6 , RtsPin = PE9 , CtsPin = PE10, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 8 */ new UartPinSettings { TxPin = PE1 , RxPin = PE0 , RtsPin = NONE, CtsPin = NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
            };
            public static void Initialize(string portname, bool enableHardwareFlowControl=false) {

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
                    ThrowExceptionPinInUsed(pinConfig.TxPin);
                }

                if (IsPinReserved(pinConfig.RxPin)) {
                    ThrowExceptionPinInUsed(pinConfig.RxPin);
                }

                if (enableHardwareFlowControl) {
                    if (pinConfig.RtsPin == NONE || pinConfig.CtsPin == NONE) {
                        throw new ArgumentException($"Port {port} does not support handshaking.");
                    }

                    if (IsPinReserved(pinConfig.RtsPin)) {
                        ThrowExceptionPinInUsed(pinConfig.RtsPin);
                    }

                    if (IsPinReserved(pinConfig.CtsPin)) {
                        ThrowExceptionPinInUsed(pinConfig.CtsPin);
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
