using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Can controller definitions.</summary>
        public static class Sdmmc {
            /// <summary>Can controller.</summary>            
            public static int SdCard2 = 0;


            public class SdmmcPinSettings {
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

                public Alternate AlternatePinD0 { get; internal set; }
                public Alternate AlternatePinD1 { get; internal set; }
                public Alternate AlternatePinD2 { get; internal set; }
                public Alternate AlternatePinD3 { get; internal set; }
                //public Alternate AlternatePinD4 { get; internal set; }
                //public Alternate AlternatePinD5 { get; internal set; }
                //public Alternate AlternatePinD6 { get; internal set; }
                //public Alternate AlternatePinD7 { get; internal set; }
                public Alternate AlternatePinCmd { get; internal set; }
                public Alternate AlternatePinClock { get; internal set; }


            };

            public static SdmmcPinSettings[] PinSettings = new SdmmcPinSettings[1] {
                new SdmmcPinSettings {
                    PinD0 = GpioPin.PF0, AlternatePinD0 = Alternate.AF9,
                    PinD1 = GpioPin.PF4, AlternatePinD1 = Alternate.AF9,
                    PinD2 = GpioPin.PD5, AlternatePinD2 = Alternate.AF10,
                    PinD3 = GpioPin.PD7, AlternatePinD3 = Alternate.AF10,
                    PinCmd = GpioPin.PF1, AlternatePinCmd = Alternate.AF9,
                    PinClock = GpioPin.PG15, AlternatePinClock = Alternate.AF10,
                 },                
            };
        }
    }
}
