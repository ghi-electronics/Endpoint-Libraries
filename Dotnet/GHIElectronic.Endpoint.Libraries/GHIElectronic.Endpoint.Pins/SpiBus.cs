using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>SPI bus definitions.</summary>
        public static class SpiBus {
            /// <summary>SPI bus.</summary>
            public static int Spi1 = 0;
            //public static int Spi2 = -1;
            //public static int Spi3 = -1;
            public static int Spi4 = 1;
            public static int Spi5 = 2;
            public class SpiPinSettings {
                public int MosiPin { get; set; }
                public int MisoPin { get; set; }
                public int ClockPin { get; set; }
                public Alternate MosiAlternate { get; set; }
                public Alternate MisoAlternate { get; set; }
                public Alternate ClockAlternate { get; set; }
            };

            public static SpiPinSettings[] PinSettings = new SpiPinSettings[3] {
                /* 1 */ new SpiPinSettings { MosiPin = GpioPin.PZ2 , MisoPin = GpioPin.PZ1 , ClockPin = GpioPin.PZ0 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate = Alternate.AF5  },
                ///* 2 */ new SpiPinSettings { MosiPin = GpioPin.NONE, MisoPin = GpioPin.NONE, ClockPin = GpioPin.NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                ///* 3 */ new SpiPinSettings { MosiPin = GpioPin.NONE, MisoPin = GpioPin.NONE, ClockPin = GpioPin.NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                /* 4 */ new SpiPinSettings { MosiPin = GpioPin.PE14, MisoPin = GpioPin.PE13, ClockPin = GpioPin.PE12, MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
                /* 5 */ new SpiPinSettings { MosiPin = GpioPin.PF9 , MisoPin = GpioPin.PF8 , ClockPin = GpioPin.PF7 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
            };
        }
    }

    
}
