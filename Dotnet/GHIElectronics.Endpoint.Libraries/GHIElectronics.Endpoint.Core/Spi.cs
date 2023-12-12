using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core.STM32MP1;
using static GHIElectronics.Endpoint.Core.Configuration;
using static GHIElectronics.Endpoint.Core.STM32MP1.GpioPin;

namespace GHIElectronics.Endpoint.Core {

    public static partial class Configuration {
        public static class Spi {
            /// <summary>SPI bus.</summary>
            public static int Spi1 = 1;
            //public static int Spi2 = 2;
            //public static int Spi3 = 3;
            public static int Spi4 = 4;
            public static int Spi5 = 5;

            const string CMD_LOCATION = "/sbin";
            const string DRIVER_LOCATION = "/lib/modules/5.13.0/kernel/drivers/spi/spidev.ko";
            internal class SpiPinSettings {
                public int MosiPin { get; set; }
                public int MisoPin { get; set; }
                public int ClockPin { get; set; }
                public Alternate MosiAlternate { get; set; }
                public Alternate MisoAlternate { get; set; }
                public Alternate ClockAlternate { get; set; }
            };

            internal static SpiPinSettings[] PinSettings =  {
                /* 1 */ new SpiPinSettings { MosiPin = PZ2 , MisoPin = PZ1 , ClockPin = PZ0 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate = Alternate.AF5  },
                /* 2 */ new SpiPinSettings { MosiPin = NONE, MisoPin = NONE, ClockPin = NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                /* 3 */ new SpiPinSettings { MosiPin = NONE, MisoPin = NONE, ClockPin = NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                /* 4 */ new SpiPinSettings { MosiPin = PE14, MisoPin = PE13, ClockPin = PE12, MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
                /* 5 */ new SpiPinSettings { MosiPin = PF9 , MisoPin = PF8 , ClockPin = PF7 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
            };
            public static void Initialize(int port) {

                if (port < Spi1 || port > Spi5) {
                    throw new ArgumentException("Invalid Spi port.");
                }

                port = port - 1;


                var pinConfig = PinSettings[port];

                if (CheckPinInUsed(pinConfig.MosiPin)) {
                    ThrowExceptionPinInUsed(pinConfig.MosiPin);
                }

                if (CheckPinInUsed(pinConfig.MisoPin)) {
                    ThrowExceptionPinInUsed(pinConfig.MisoPin);
                }

                if (CheckPinInUsed(pinConfig.ClockPin)) {
                    ThrowExceptionPinInUsed(pinConfig.ClockPin);
                }

                

                SetModer(pinConfig.MosiPin, Moder.Alternate);
                SetModer(pinConfig.MisoPin, Moder.Alternate);
                SetModer(pinConfig.ClockPin, Moder.Alternate);

                SetAlternate(pinConfig.MosiPin, pinConfig.MosiAlternate);
                SetAlternate(pinConfig.MisoPin, pinConfig.MisoAlternate);
                SetAlternate(pinConfig.ClockPin, pinConfig.ClockAlternate);

                RegisterPin(pinConfig.MosiPin);
                RegisterPin(pinConfig.MisoPin);
                RegisterPin(pinConfig.ClockPin);

                // load driver
                if (Directory.Exists("/sys/class/spidev"))
                    return;

                var script = new Script("insmod", CMD_LOCATION, DRIVER_LOCATION);
                script.Start();

                while (!Directory.Exists("/sys/class/spidev")) ;

            }
            public static void UnInitialize(int port) {

                if (port < Spi1 || port > Spi5) {
                    throw new ArgumentException("Invalid Spi port.");
                }

                port = port - 1;


                var pinConfig = PinSettings[port];

                // release driver
                if (!Directory.Exists("/sys/class/spidev")) // unloaded
                    return;

                var script = new Script("rmmod", CMD_LOCATION, DRIVER_LOCATION);
                script.Start();
              
                SetModer(pinConfig.MosiPin, Moder.Input);
                SetModer(pinConfig.MisoPin, Moder.Input);
                SetModer(pinConfig.ClockPin, Moder.Input);

                UnRegisterPin(pinConfig.MosiPin);
                UnRegisterPin(pinConfig.MisoPin);
                UnRegisterPin(pinConfig.ClockPin);
            }
        }
    }
}
