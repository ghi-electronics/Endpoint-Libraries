using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;
using static GHIElectronics.Endpoint.Core.Configuration.Gpio;


namespace GHIElectronics.Endpoint.Core {
    public static partial class Configuration {
        public static class Can {
            /// <summary>Can controller.</summary>
            public static int Can1 = 1;
            public static int Can2 = 2;


            internal class CanPinSettings {
                public int TxPin { get; set; }
                public int RxPin { get; set; }

                public Alternate TxAlternate { get; set; }
                public Alternate RxAlternate { get; set; }

            };

            internal static CanPinSettings[] PinSettings =  {
                new CanPinSettings { TxPin = Gpio.Pin.PD1, RxPin = Gpio.Pin.PD0, TxAlternate = Alternate.AF9, RxAlternate = Alternate.AF9, },
                new CanPinSettings { TxPin = Gpio.Pin.PB13, RxPin = Gpio.Pin.PB12, TxAlternate = Alternate.AF9, RxAlternate = Alternate.AF9, },
            };

        }
    }
}
