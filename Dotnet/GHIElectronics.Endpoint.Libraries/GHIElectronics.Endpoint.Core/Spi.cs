using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GHIElectronics.Endpoint.Core.EPM815.Gpio;


namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        public static class Spi {
            /// <summary>SPI bus.</summary>
            public const int Spi1 = 0;
            //public static int Spi2 = 2;
            //public static int Spi3 = 3;
            public const int Spi4 = 1;
            public const int Spi5 = 2;

            //private static bool initialized = false;
            private static List<int> initializedList = new List<int>();

            //const string CMD_LOCATION = "/sbin";
            //const string DRIVER_LOCATION = "/lib/modules/5.13.0/kernel/drivers/spi/spidev.ko";
            internal class SpiPinSettings {
                public int MosiPin { get; set; }
                public int MisoPin { get; set; }
                public int ClockPin { get; set; }
                public Alternate MosiAlternate { get; set; }
                public Alternate MisoAlternate { get; set; }
                public Alternate ClockAlternate { get; set; }
            };

            internal static SpiPinSettings[] PinSettings =  {
                /* 1 */ new SpiPinSettings { MosiPin = Gpio.Pin.PZ2 , MisoPin = Gpio.Pin.PZ1 , ClockPin = Gpio.Pin.PZ0 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate = Alternate.AF5  },
                ///* 2 */ new SpiPinSettings { MosiPin = Gpio.Pin.NONE, MisoPin = Gpio.Pin.NONE, ClockPin = Gpio.Pin.NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                ///* 3 */ new SpiPinSettings { MosiPin = Gpio.Pin.NONE, MisoPin = Gpio.Pin.NONE, ClockPin = Gpio.Pin.NONE, MosiAlternate = Alternate.NONE, MisoAlternate = Alternate.NONE, ClockAlternate = Alternate.NONE },
                /* 4 */ new SpiPinSettings { MosiPin = Gpio.Pin.PE14, MisoPin = Gpio.Pin.PE13, ClockPin = Gpio.Pin.PE12, MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
                /* 5 */ new SpiPinSettings { MosiPin = Gpio.Pin.PF9 , MisoPin = Gpio.Pin.PF8 , ClockPin = Gpio.Pin.PF7 , MosiAlternate = Alternate.AF5 , MisoAlternate = Alternate.AF5 , ClockAlternate =Alternate.AF5   },
            };

            //public static int GetNativePort(int port) {
            //    switch (port) {
            //        case 1: return 0;
            //        case 4: return 1;
            //        case 5: return 2;

            //    }

            //    throw new Exception($"SPI {port} is not supported.");
            //}
            public static void Initialize(int port) {

                if (port != Spi1 && port != Spi4 && port != Spi5) {
                    throw new ArgumentException($"Invalid Spi port. The device support only: 0 = SPI1; 1 = SPI4; 2 = SPI5");
                }

                //port = port - 1;

                if (initializedList.Contains(port))
                    return;


                var pinConfig = PinSettings[port];

                if (IsPinReserved(pinConfig.MosiPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.MosiPin);
                }

                if (IsPinReserved(pinConfig.MisoPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.MisoPin);
                }

                if (IsPinReserved(pinConfig.ClockPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.ClockPin);
                }



                SetModer(pinConfig.MosiPin, Moder.Alternate);
                SetModer(pinConfig.MisoPin, Moder.Alternate);
                SetModer(pinConfig.ClockPin, Moder.Alternate);

                SetAlternate(pinConfig.MosiPin, pinConfig.MosiAlternate);
                SetAlternate(pinConfig.MisoPin, pinConfig.MisoAlternate);
                SetAlternate(pinConfig.ClockPin, pinConfig.ClockAlternate);

                PinReserve(pinConfig.MosiPin);
                PinReserve(pinConfig.MisoPin);
                PinReserve(pinConfig.ClockPin);

                // load driver
                if (Directory.Exists("/sys/class/spidev")) {
                    initializedList.Add(port);
                    return;
                }

                var script = new Script("modprobe", "./", "spidev");
                script.Start();

                while (!Directory.Exists("/sys/class/spidev")) {
                    Thread.Sleep(10);
                }                
            }
            public static void UnInitialize(int port) {

                if (port != Spi1 && port != Spi4 && port != Spi5) {
                    throw new ArgumentException("Invalid Spi port.");
                }

                if (initializedList.Contains(port)) {
                    //port = port - 1;

                    var pinConfig = PinSettings[port];                 

                    SetModer(pinConfig.MosiPin, Moder.Input);
                    SetModer(pinConfig.MisoPin, Moder.Input);
                    SetModer(pinConfig.ClockPin, Moder.Input);

                    PinRelease(pinConfig.MosiPin);
                    PinRelease(pinConfig.MisoPin);
                    PinRelease(pinConfig.ClockPin);

                    initializedList.Remove(port);
                }
            }
        }
    }
}
