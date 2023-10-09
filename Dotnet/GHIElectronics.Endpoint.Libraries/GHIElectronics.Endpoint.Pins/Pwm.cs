using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Pwm controller definitions.</summary>
        public static class Pwm {
            /// <summary>Pwm controller.</summary>
            //public static int Pwm1 = 0;

            ///// <summary>Pwm channel.</summary>
            //public static int Channel1 = 0;
            //public class PwmPinSettings {
            //    public int PwmPin { get; set; }
            //    public Alternate PwmAlternate { get; set; }
            //};

            //public static PwmPinSettings[][] PinSettings = {
            //                                                                             //1                                                                            2                                                                            3                                                                            4                                                                          
            //      /* 1  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA8 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PE11, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PA10, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PA11, PwmAlternate = Alternate.AF1  } },
            //      /* 2  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA15, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PB3 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PA3 , PwmAlternate = Alternate.AF1   } },
            //      /* 3  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PC6 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PB5 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PB0 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 4  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PB7 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PD14, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PD15, PwmAlternate = Alternate.AF2  } },
            //      /* 5  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PH11, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PH12, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PI0 , PwmAlternate = Alternate.AF2  } },
            //      /* 6  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA0 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 7  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 8  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PI5 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI6 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI7 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI2 , PwmAlternate = Alternate.AF3  } },
            //      /* 9  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 10 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 11 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 12 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PH6 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PH9 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 13 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA6 , PwmAlternate = Alternate.AF9  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 14 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PF9 , PwmAlternate = Alternate.AF9  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 15 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PE5 , PwmAlternate = Alternate.AF4  }, new PwmPinSettings { PwmPin = GpioPin.PE6 , PwmAlternate = Alternate.AF4  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 16 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PB8 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            //      /* 17 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PB9 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },

            //};

            public static int ToActualController(int id) {
                switch (id) {
                    case 2 - 1: return 0;
                    case 3 - 1: return 4;
                    case 4 - 1: return 8;
                    case 5 - 1: return 12;
                    case 12 - 1: return 16;
                    case 13 - 1: return 18;
                    case 14 - 1: return 19;
                    case 1 - 1: return 20;
                    case 8 - 1: return 24;
                    case 15 - 1: return 28;
                    case 16 - 1: return 30;
                    case 17 - 1: return 31;

                }
                throw new Exception(string.Format("The controller {0} is not available.", id));

            }

            public static class Controller1 {
                public const int PA8 = (0x0000 << 16) | (GpioPin.PA8 << 8) | ((int)Alternate.AF1 << 0);
                public const int PE11 = (0x0001 << 16) | (GpioPin.PE11 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA10 = (0x0002 << 16) | (GpioPin.PA10 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA11 = (0x0003 << 16) | (GpioPin.PA11 << 8) | ((int)Alternate.AF1 << 0);
            }

            public static class Controller2 {
                public const int PA15 = (0x0100 << 16) | (GpioPin.PA15 << 8) | ((int)Alternate.AF1 << 0);
                public const int PB3 = (0x0101 << 16) | (GpioPin.PB3 << 8) | ((int)Alternate.AF1 << 0);
                public const int PA3 = (0x0103 << 16) | (GpioPin.PA3 << 8) | ((int)Alternate.AF1 << 0);
            }

            public static class Controller3 {
                public const int PC6 = (0x0200 << 16) | (GpioPin.PC6 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB5 = (0x0201 << 16) | (GpioPin.PB5 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB0 = (0x0202 << 16) | (GpioPin.PB0 << 8) | ((int)Alternate.AF2 << 0);
            }

            public static class Controller4 {
                public const int PD12 = (0x0300 << 16) | (GpioPin.PD12 << 8) | ((int)Alternate.AF2 << 0);
                public const int PB7 = (0x0301 << 16) | (GpioPin.PB7 << 8) | ((int)Alternate.AF2 << 0);
                public const int PD14 = (0x0302 << 16) | (GpioPin.PD14 << 8) | ((int)Alternate.AF2 << 0);
                public const int PD15 = (0x0303 << 16) | (GpioPin.PD15 << 8) | ((int)Alternate.AF2 << 0);
            }

            public static class Controller5 {
                public const int PA0 = (0x0400 << 16) | (GpioPin.PA0 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH11 = (0x0401 << 16) | (GpioPin.PH11 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH12 = (0x0402 << 16) | (GpioPin.PH12 << 8) | ((int)Alternate.AF2 << 0);
                public const int PI0 = (0x0403 << 16) | (GpioPin.PI0 << 8) | ((int)Alternate.AF2 << 0);
            }
         
            public static class Controller8 {
                public const int PI5 = (0x0700 << 16) | (GpioPin.PI5 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI6 = (0x0701 << 16) | (GpioPin.PI6 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI7 = (0x0702 << 16) | (GpioPin.PI7 << 8) | ((int)Alternate.AF3 << 0);
                public const int PI2 = (0x0703 << 16) | (GpioPin.PI2 << 8) | ((int)Alternate.AF3 << 0);
            }

            public static class Controller12 {
                public const int PH6 = (0x0B00 << 16) | (GpioPin.PH6 << 8) | ((int)Alternate.AF2 << 0);
                public const int PH9 = (0x0B01 << 16) | (GpioPin.PH9 << 8) | ((int)Alternate.AF2 << 0);
            }

            public static class Controller13 {
                public const int PA6 = (0x0C00 << 16) | (GpioPin.PA6 << 8) | ((int)Alternate.AF9 << 0);
            }

            public static class Controller14 {
                public const int PF9 = (0x0D00 << 16) | (GpioPin.PF9 << 8) | ((int)Alternate.AF9 << 0);
            }

            public static class Controller15 {
                public const int PE5 = (0x0E00 << 16) | (GpioPin.PE5 << 8) | ((int)Alternate.AF4 << 0);
                public const int PE6 = (0x0E01 << 16) | (GpioPin.PE6 << 8) | ((int)Alternate.AF4 << 0);
            }

            public static class Controller16 {
                public const int PB8 = (0x0F00 << 16) | (GpioPin.PB8 << 8) | ((int)Alternate.AF1 << 0);
            }

            public static class Controller17 {
                public const int PB9 = (0x1000 << 16) | (GpioPin.PB9 << 8) | ((int)Alternate.AF1 << 0);
            }
        }
    }
}
