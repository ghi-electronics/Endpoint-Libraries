using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Adc controller definitions.</summary>
        public static class Adc {
            /// <summary>Adc controller.</summary>
            //public static int Adc0 = 0;
            //public static int Adc1 = 1;
            //public static int Adc2 = 2;

            ///// <summary>Adc channel.</summary>
            //public static int Channel0 = 0;
            //public static int Channel1 = 1;
            //public static int Channel2 = 2;
            //public static int Channel3 = 3;
            //public static int Channel4 = 4;
            //public static int Channel5 = 5;
            //public static int Channel6 = 6;
            //public static int Channel7 = 7;
            //public static int Channel8 = 8;
            //public static int Channel9 = 9;
            //public static int Channel10 = 10;
            //public static int Channel11 = 11;
            //public static int Channel12 = 12;
            //public static int Channel13 = 13;
            //public static int Channel14 = 14;
            //public static int Channel15 = 15;
            //public static int Channel16 = 16;
            //public static int Channel17 = 17;
            //public static int Channel18 = 18;
            //public static int Channel19 = 19;

            public static class Pin {
                /// <summary>Adc pin.</summary>
                public const int ANA0 = 0x0000_FFFF; // channel 0
                public const int ANA1 = 0x0001_FFFF; // channel 1
                public const int PF11 = 0x0002_005B; // channel 2
                public const int PA6 = 0x0003_0006; // channel 3
                //public const int NONE = 0x0004; // channel 4
                //public const int PB1 = 0x0005; // channel 5 => this is reserved for Eth_1G
                public const int PF12 = 0x0006_005C; // channel 6
                //public const int PA7 = 0x0007; // channel 7 => This is used for ETH
                //public const int PC5 = 0x0008; // channel 8 => this is used for ETH
                public const int PB0 = 0x0009_0010; // channel 9
                public const int PC0 = 0x000A_0020; // channel 10
                //public const int PC1 = 0x000B; // channel 11 => This is used for ETH
                //public const int PC2 = 0x000C; // channel 12 => this is reserved for Eth_1G
                public const int PC3 = 0x000D_0023; // channel 13
                //public const int PA2 = 0x000E; // channel 14 => this is reserved for Eth_1G
                public const int PA3 = 0x000F_0003; // channel 15
                public const int PA0 = 0x0010_0000; // channel 16
                //public const int PA1 = 0x0011; // channel 17 => This is used for ETH
                public const int PA4 = 0x0012_0004; // channel 18
                public const int PA5 = 0x0013_0005; // channel 19

                public const int PF13 = 0x0102_005D; // channel 2
                public const int PF14 = 0x0106_005E; // channel 6
 

                
            }


            //public class AdcPinSettings {
            //    public int AdcPin { get; set; }
            //};

            //public static AdcPinSettings[][] PinSettings = {
            //      new AdcPinSettings[] {    new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 0
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 1
            //                                new AdcPinSettings { AdcPin = GpioPin.PF11 }, // channel 2
            //                                new AdcPinSettings { AdcPin = GpioPin.PA6  }, // channel 3
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 4
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 5
            //                                new AdcPinSettings { AdcPin = GpioPin.PF12 }, // channel 6
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 7
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 8
            //                                new AdcPinSettings { AdcPin = GpioPin.PB0  }, // channel 9
            //                                new AdcPinSettings { AdcPin = GpioPin.PC0  }, // channel 10
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 11
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 12
            //                                new AdcPinSettings { AdcPin = GpioPin.PC3  }, // channel 13
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 14
            //                                new AdcPinSettings { AdcPin = GpioPin.PA3  }, // channel 15
            //                                new AdcPinSettings { AdcPin = GpioPin.PA0  }, // channel 16
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE  }, // channel 17
            //                                new AdcPinSettings { AdcPin = GpioPin.PA4  }, // channel 18
            //                                new AdcPinSettings { AdcPin = GpioPin.PA5  }, // channel 19
            //                           },
            //      new AdcPinSettings[] {    new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 0
            //                                new AdcPinSettings { AdcPin = GpioPin.PF13 }, // channel 1
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 2
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 3
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 4
            //                                new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 5
            //                                new AdcPinSettings { AdcPin = GpioPin.PF14 }, // channel 6                                            
            //      },
            //};
        }
    }
}
