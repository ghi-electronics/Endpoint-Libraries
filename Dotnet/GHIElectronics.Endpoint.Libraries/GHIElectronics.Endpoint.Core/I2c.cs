using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GHIElectronics.Endpoint.Core.Gpio;

namespace GHIElectronics.Endpoint.Core {
    public static partial class Configuration {

        public static class I2c {
            /// <summary>I2c controller.</summary>
            public static int I2c1 = 1;
            //public static int I2c2 = 2;
            //public static int I2c3 = 3;
            public static int I2c4 = 4;
            public static int I2c5 = 5;
            public static int I2c6 = 6;

            internal class I2cPinSettings {
                public int SdaPin { get; set; }
                public int SclPin { get; set; }
                public Alternate SdaAlternate { get; set; }
                public Alternate SclAlternate { get; set; }
            };

            internal static I2cPinSettings[] PinSettings = {
                /*i2c1*/new I2cPinSettings { SdaPin = PD13, SclPin = PD12, SdaAlternate = Alternate.AF5 ,  SclAlternate = Alternate.AF5  },
                /*i2c2*/new I2cPinSettings { SdaPin = NONE, SclPin = NONE, SdaAlternate = Alternate.AF0 ,  SclAlternate = Alternate.AF0  },
                /*i2c3*/new I2cPinSettings { SdaPin = NONE, SclPin = NONE, SdaAlternate = Alternate.AF0 ,  SclAlternate = Alternate.AF0  },                
                /*i2c4*/new I2cPinSettings { SdaPin = PF15, SclPin = PB6 , SdaAlternate = Alternate.AF4 ,  SclAlternate = Alternate.AF6 },
                /*i2c5*/new I2cPinSettings { SdaPin = PZ5 , SclPin = PZ4 , SdaAlternate = Alternate.AF4 ,  SclAlternate = Alternate.AF4  },                
                /*i2c6*/new I2cPinSettings { SdaPin = PD0, SclPin = PD1 , SdaAlternate = Alternate.AF2 ,  SclAlternate = Alternate.AF2 },

            };

            public static void Initialize(int port) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                port = port - 1;


                var pinConfig = PinSettings[port];

                if (IsPinReserved(pinConfig.SclPin)) {
                    ThrowExceptionPinInUsed(pinConfig.SclPin);
                }

                if (IsPinReserved(pinConfig.SdaPin)) {
                    ThrowExceptionPinInUsed(pinConfig.SdaPin);
                }


              

                SetModer(pinConfig.SclPin, Moder.Alternate);
                SetModer(pinConfig.SdaPin, Moder.Alternate);

                SetAlternate(pinConfig.SclPin, pinConfig.SclAlternate);
                SetAlternate(pinConfig.SdaPin, pinConfig.SdaAlternate);


                SetPull(pinConfig.SclPin, Pull.Up);
                SetPull(pinConfig.SdaPin, Pull.Up);

                SetOutputType(pinConfig.SclPin, OutputType.OpenDrain);
                SetOutputType(pinConfig.SdaPin, OutputType.OpenDrain);

                PinReserve(pinConfig.SclPin);
                PinReserve(pinConfig.SdaPin);

            }
            public static void UnInitialize(int port) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                port = port - 1;

                var pinConfig = PinSettings[port];

                PinRelease(pinConfig.SclPin);
                PinRelease(pinConfig.SdaPin);

                SetModer(pinConfig.SclPin, Moder.Input);
                SetModer(pinConfig.SdaPin, Moder.Input);
            }
        }
    }
}
