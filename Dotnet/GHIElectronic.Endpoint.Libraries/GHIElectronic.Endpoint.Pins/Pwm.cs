using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Pins {
    public static partial class STM32MP1 {
        /// <summary>Pwm controller definitions.</summary>
        public static class Pwm {
            /// <summary>Pwm controller.</summary>
            public static int Pwm1 = 0;

            /// <summary>Pwm channel.</summary>
            public static int Channel1 = 0;
            public class PwmPinSettings {
                public int PwmPin { get; set; }
                public Alternate PwmAlternate { get; set; }
            };

            public static PwmPinSettings[][] PinSettings = {
                                                                                         //1                                                                            2                                                                            3                                                                            4                                                                          
                  /* 1  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA8 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PE11, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PA10, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PA11, PwmAlternate = Alternate.AF1  } },
                  /* 2  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA15, PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.PB3 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PA3 , PwmAlternate = Alternate.AF1   } },
                  /* 3  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PC6 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PB5 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PB0 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 4  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PB7 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PD14, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PD15, PwmAlternate = Alternate.AF2  } },
                  /* 5  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.PH11, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PH12, PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PI0 , PwmAlternate = Alternate.AF2  } },
                  /* 6  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA0 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 7  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 8  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PI5 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI6 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI7 , PwmAlternate = Alternate.AF3  }, new PwmPinSettings { PwmPin = GpioPin.PI2 , PwmAlternate = Alternate.AF3  } },
                  /* 9  */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 10 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 11 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 12 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PH6 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.PH9 , PwmAlternate = Alternate.AF2  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 13 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PA6 , PwmAlternate = Alternate.AF9  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 14 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PF9 , PwmAlternate = Alternate.AF9  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 15 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PE5 , PwmAlternate = Alternate.AF4  }, new PwmPinSettings { PwmPin = GpioPin.PE6 , PwmAlternate = Alternate.AF4  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 16 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PB8 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
                  /* 17 */new PwmPinSettings[] { new PwmPinSettings { PwmPin = GpioPin.PB9 , PwmAlternate = Alternate.AF1  }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE }, new PwmPinSettings { PwmPin = GpioPin.NONE, PwmAlternate = Alternate.NONE } },
            
            };

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
                    case 15 - 1: return 24;
                    case 16 - 1: return 26;
                    case 17 - 1: return 27;

                }
                throw new Exception(string.Format("The controller {0} is not available.", id));

            }
        }
    }
}
