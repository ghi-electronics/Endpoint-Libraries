using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static partial class Configuration {
        public static class Adc {

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

        }
    }
}
