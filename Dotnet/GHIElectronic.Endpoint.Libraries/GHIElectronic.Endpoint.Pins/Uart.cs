using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Uart controller definitions.</summary>
        public static class Uart {
            /// <summary>Uart controller.</summary>
            public static string Uart1 = "/dev/ttySTM0";
            public static string Uart2 = "/dev/ttySTM1";
            public static string Uart3 = "/dev/ttySTM2";
            public static string Uart4 = "/dev/ttySTM3";
            public static string Uart5 = "/dev/ttySTM4";
            public static string Uart6 = "/dev/ttySTM5";
            public static string Uart7 = "/dev/ttySTM6";
            public static string Uart8 = "/dev/ttySTM7";

            public class UartPinSettings {
                public int TxPin { get; set; }
                public int RxPin { get; set; }                
                public int RtsPin { get; set; }
                public int CtsPin { get; set; }
                public Alternate TxAlternate { get; set; }
                public Alternate RxAlternate { get; set; }
                public Alternate RtsAlternate { get; set; }
                public Alternate CtsAlternate { get; set; }
            };

            public static UartPinSettings[] PinSettings = new UartPinSettings[8] {
                /* 1 */ new UartPinSettings { TxPin = GpioPin.PZ7 , RxPin = GpioPin.PZ6 , RtsPin = GpioPin.PA12, CtsPin = GpioPin.PZ3 , TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 2 */ new UartPinSettings { TxPin = GpioPin.PF5 , RxPin = GpioPin.PD6 , RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 3 */ new UartPinSettings { TxPin = GpioPin.PD8 , RxPin = GpioPin.PD9 , RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 4 */ new UartPinSettings { TxPin = GpioPin.PD1 , RxPin = GpioPin.PD0 , RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 5 */ new UartPinSettings { TxPin = GpioPin.PB13, RxPin = GpioPin.PB12, RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.AF14,  CtsAlternate = Alternate.AF14 },
                /* 6 */ new UartPinSettings { TxPin = GpioPin.NONE, RxPin = GpioPin.NONE, RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.NONE, RxAlternate = Alternate.NONE, RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
                /* 7 */ new UartPinSettings { TxPin = GpioPin.PE8 , RxPin = GpioPin.PF6 , RtsPin = GpioPin.PE9 , CtsPin = GpioPin.PE10, TxAlternate = Alternate.AF7 , RxAlternate = Alternate.AF7 , RtsAlternate = Alternate.AF7 ,  CtsAlternate = Alternate.AF7  },
                /* 8 */ new UartPinSettings { TxPin = GpioPin.PE1 , RxPin = GpioPin.PE0 , RtsPin = GpioPin.NONE, CtsPin = GpioPin.NONE, TxAlternate = Alternate.AF8 , RxAlternate = Alternate.AF8 , RtsAlternate = Alternate.NONE,  CtsAlternate = Alternate.NONE },
            };
        }
    }
}
