using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Can controller definitions.</summary>
        public static class Can {
            /// <summary>Can controller.</summary>
            public static int Can1 = 0;
            public static int Can2 = 1;
         

            public class CanPinSettings {
                public int TxPin { get; set; }
                public int RxPin { get; set; }                
                
                public Alternate TxAlternate { get; set; }
                public Alternate RxAlternate { get; set; }
              
            };

            public static CanPinSettings[] PinSettings = new CanPinSettings[2] {
                new CanPinSettings { TxPin = GpioPin.PD1, RxPin = GpioPin.PD0, TxAlternate = Alternate.AF9, RxAlternate = Alternate.AF9, },
                new CanPinSettings { TxPin = GpioPin.PB13, RxPin = GpioPin.PB12, TxAlternate = Alternate.AF9, RxAlternate = Alternate.AF9, },                
            };
        }
    }
}
