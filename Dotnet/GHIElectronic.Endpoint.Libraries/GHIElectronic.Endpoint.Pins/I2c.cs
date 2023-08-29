using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>I2c controller definitions.</summary>
        public static class I2c {
            /// <summary>I2c controller.</summary>
            public static int I2c1 = 0;
            //public static int I2c2 = -1;
            //public static int I2c3 = -1;
            public static int I2c4 = 2;
            public static int I2c5 = 1; 

            public class I2cPinSettings {
                public int SdaPin { get; set; }
                public int SclPin { get; set; }
                public Alternate SdaAlternate { get; set; }
                public Alternate SclAlternate { get; set; }
            };

            public static I2cPinSettings[] PinSettings = {
                /*i2c1*/new I2cPinSettings { SdaPin = GpioPin.PD13, SclPin = GpioPin.PD12, SdaAlternate = Alternate.AF5 ,  SclAlternate = Alternate.AF5  },                
                /*i2c5*/new I2cPinSettings { SdaPin = GpioPin.PZ5 , SclPin = GpioPin.PZ4 , SdaAlternate = Alternate.AF4 ,  SclAlternate = Alternate.AF4  },
                /*i2c4*/new I2cPinSettings { SdaPin = GpioPin.PF15, SclPin = GpioPin.PB6 , SdaAlternate = Alternate.AF4 ,  SclAlternate = Alternate.AF6 },

            };
        }
    }
}
