using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        internal static class Dcmi {

            internal const int Dcmi0 = 0;
            internal class DcmiPinSettings {

                
                public int DcmiHsync { get; internal set; }
                public int DcmiVsync { get; internal set; }
                public int DcmiPixclk { get; internal set; }
                public int DcmiD0 { get; internal set; }               
                public int DcmiD1 { get; internal set; }
                public int DcmiD2 { get; internal set; }
                public int DcmiD3 { get; internal set; }
                public int DcmiD4 { get; internal set; }
                public int DcmiD5 { get; internal set; }
                public int DcmiD6 { get; internal set; }
                public int DcmiD7 { get; internal set; }

                public Gpio.Alternate AlternatePinDcmiHsync { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiVsync { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiPixclk { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD0 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD1 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD2 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD3 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD4 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD5 { get; internal set; }
                public Gpio.Alternate AlternatePinDcmiD6 { get; internal set; }

                public Gpio.Alternate AlternatePinDcmiD7 { get; internal set; }


            };

            internal static DcmiPinSettings[] PinSettings = {
                new DcmiPinSettings {
                    DcmiHsync = Gpio.Pin.PA4, AlternatePinDcmiHsync = Gpio.Alternate.AF13,
                    DcmiVsync = Gpio.Pin.PG9, AlternatePinDcmiVsync = Gpio.Alternate.AF13,
                    DcmiPixclk = Gpio.Pin.PA6, AlternatePinDcmiPixclk = Gpio.Alternate.AF13,
                    DcmiD0 = Gpio.Pin.PH9, AlternatePinDcmiD0 = Gpio.Alternate.AF13,
                    DcmiD1 = Gpio.Pin.PH10, AlternatePinDcmiD1 = Gpio.Alternate.AF13,
                    DcmiD2 = Gpio.Pin.PG10, AlternatePinDcmiD2 = Gpio.Alternate.AF13,
                    DcmiD3 = Gpio.Pin.PH12, AlternatePinDcmiD3 = Gpio.Alternate.AF13,
                    DcmiD4 = Gpio.Pin.PE4, AlternatePinDcmiD4 = Gpio.Alternate.AF13,
                    DcmiD5 = Gpio.Pin.PI4, AlternatePinDcmiD5 = Gpio.Alternate.AF13,
                    DcmiD6 = Gpio.Pin.PE5, AlternatePinDcmiD6 = Gpio.Alternate.AF13,
                    DcmiD7 = Gpio.Pin.PB9, AlternatePinDcmiD7 = Gpio.Alternate.AF13,

                 },
            };


        }
    }
}
