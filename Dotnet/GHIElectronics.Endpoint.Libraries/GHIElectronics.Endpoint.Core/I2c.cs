using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;


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
                public Gpio.Alternate SdaAlternate { get; set; }
                public Gpio.Alternate SclAlternate { get; set; }
            };

            internal static I2cPinSettings[] PinSettings = {
                /*i2c1*/new I2cPinSettings { SdaPin = Gpio.Pin.PD13, SclPin = Gpio.Pin.PD12, SdaAlternate = Gpio.Alternate.AF5 ,  SclAlternate = Gpio.Alternate.AF5  },
                /*i2c2*/new I2cPinSettings { SdaPin = Gpio.Pin.NONE, SclPin = Gpio.Pin.NONE, SdaAlternate = Gpio.Alternate.AF0 ,  SclAlternate = Gpio.Alternate.AF0  },
                /*i2c3*/new I2cPinSettings { SdaPin = Gpio.Pin.NONE, SclPin = Gpio.Pin.NONE, SdaAlternate = Gpio.Alternate.AF0 ,  SclAlternate = Gpio.Alternate.AF0  },                
                /*i2c4*/new I2cPinSettings { SdaPin = Gpio.Pin.PF15, SclPin = Gpio.Pin.PB6 , SdaAlternate = Gpio.Alternate.AF4 ,  SclAlternate = Gpio.Alternate.AF6 },
                /*i2c5*/new I2cPinSettings { SdaPin = Gpio.Pin.PZ5 , SclPin = Gpio.Pin.PZ4 , SdaAlternate = Gpio.Alternate.AF4 ,  SclAlternate = Gpio.Alternate.AF4  },                
                /*i2c6*/new I2cPinSettings { SdaPin = Gpio.Pin.PD0, SclPin = Gpio.Pin.PD1 , SdaAlternate = Gpio.Alternate.AF2 ,  SclAlternate = Gpio.Alternate.AF2 },

            };

            public static void Initialize(int port) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                port = port - 1;


                var pinConfig = PinSettings[port];

                if (Gpio.IsPinReserved(pinConfig.SclPin)) {
                    Configuration.ThrowExceptionPinInUsed(pinConfig.SclPin);
                }

                if (Gpio.IsPinReserved(pinConfig.SdaPin)) {
                    Configuration.ThrowExceptionPinInUsed(pinConfig.SdaPin);
                }




                Gpio.SetModer(pinConfig.SclPin, Gpio.Moder.Alternate);
                Gpio.SetModer(pinConfig.SdaPin, Gpio.Moder.Alternate);

                Gpio.SetAlternate(pinConfig.SclPin, pinConfig.SclAlternate);
                Gpio.SetAlternate(pinConfig.SdaPin, pinConfig.SdaAlternate);


                Gpio.SetPull(pinConfig.SclPin, Gpio.Pull.Up);
                Gpio.SetPull(pinConfig.SdaPin, Gpio.Pull.Up);

                Gpio.SetOutputType(pinConfig.SclPin, Gpio.OutputType.OpenDrain);
                Gpio.SetOutputType(pinConfig.SdaPin, Gpio.OutputType.OpenDrain);

                Gpio.PinReserve(pinConfig.SclPin);
                Gpio.PinReserve(pinConfig.SdaPin);

            }
            public static void UnInitialize(int port) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                port = port - 1;

                var pinConfig = PinSettings[port];

                Gpio.PinRelease(pinConfig.SclPin);
                Gpio.PinRelease(pinConfig.SdaPin);

                Gpio.SetModer(pinConfig.SclPin, Gpio.Moder.Input);
                Gpio.SetModer(pinConfig.SdaPin, Gpio.Moder.Input);
            }

        }
    }
}
