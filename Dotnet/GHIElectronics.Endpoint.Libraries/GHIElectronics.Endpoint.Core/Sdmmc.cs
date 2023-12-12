using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        public static class Sdmmc {
            /// <summary>Can controller.</summary>            
            public static int SdCard2 = 0;


            internal class SdmmcPinSettings {
                public int PinD0 { get; internal set; }
                public int PinD1 { get; internal set; }
                public int PinD2 { get; internal set; }
                public int PinD3 { get; internal set; }
                //public int PinD4 { get; internal set; }
                //public int PinD5 { get; internal set; }
                //public int PinD6 { get; internal set; }
                //public int PinD7 { get; internal set; }
                public int PinCmd { get; internal set; }
                public int PinClock { get; internal set; }

                public Gpio.Alternate AlternatePinD0 { get; internal set; }
                public Gpio.Alternate AlternatePinD1 { get; internal set; }
                public Gpio.Alternate AlternatePinD2 { get; internal set; }
                public Gpio.Alternate AlternatePinD3 { get; internal set; }
                //public Alternate AlternatePinD4 { get; internal set; }
                //public Alternate AlternatePinD5 { get; internal set; }
                //public Alternate AlternatePinD6 { get; internal set; }
                //public Alternate AlternatePinD7 { get; internal set; }
                public Gpio.Alternate AlternatePinCmd { get; internal set; }
                public Gpio.Alternate AlternatePinClock { get; internal set; }


            };

            internal static SdmmcPinSettings[] PinSettings = {
                new SdmmcPinSettings {
                    PinD0 = Gpio.Pin.PF0, AlternatePinD0 = Gpio.Alternate.AF9,
                    PinD1 = Gpio.Pin.PF4, AlternatePinD1 = Gpio.Alternate.AF9,
                    PinD2 = Gpio.Pin.PD5, AlternatePinD2 = Gpio.Alternate.AF10,
                    PinD3 = Gpio.Pin.PD7, AlternatePinD3 = Gpio.Alternate.AF10,
                    PinCmd = Gpio.Pin.PF1, AlternatePinCmd = Gpio.Alternate.AF9,
                    PinClock = Gpio.Pin.PG15, AlternatePinClock = Gpio.Alternate.AF10,
                 },
            };


        }
    }
}

