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
            public static int Adc0 = 0;
            public static int Adc1 = 1;
            public static int Adc2 = 2;

            /// <summary>Adc channel.</summary>
            public static int Channel0 = 0;
            public static int Channel1 = 1;
            public static int Channel2 = 2;
            public static int Channel3 = 3;
            public static int Channel4 = 4;
            public static int Channel5 = 5;
            public static int Channel6 = 6;
            public static int Channel7 = 7;
            public static int Channel8 = 8;
            public static int Channel9 = 9;
            public static int Channel10 = 10;
            public static int Channel11 = 11;
            public static int Channel12 = 12;
            public static int Channel13 = 13;
            public static int Channel14 = 14;
            public static int Channel15 = 15;
            public static int Channel16 = 16;
            public static int Channel17 = 17;
            public static int Channel18 = 18;
            public static int Channel19 = 19;

            public class AdcPinSettings {
                public int AdcPin { get; set; }                
            };

            public static AdcPinSettings[][] PinSettings = {
                  new AdcPinSettings[] {    new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 0
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 1
                                            new AdcPinSettings { AdcPin = GpioPin.PF11 }, // channel 2
                                            new AdcPinSettings { AdcPin = GpioPin.PA6  }, // channel 3
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 4
                                            new AdcPinSettings { AdcPin = GpioPin.PB1  }, // channel 5
                                            new AdcPinSettings { AdcPin = GpioPin.PF12 }, // channel 6
                                            new AdcPinSettings { AdcPin = GpioPin.PA7  }, // channel 7
                                            new AdcPinSettings { AdcPin = GpioPin.PC5  }, // channel 8
                                            new AdcPinSettings { AdcPin = GpioPin.PB0  }, // channel 9
                                            new AdcPinSettings { AdcPin = GpioPin.PC0  }, // channel 10
                                            new AdcPinSettings { AdcPin = GpioPin.PC1  }, // channel 11
                                            new AdcPinSettings { AdcPin = GpioPin.PC2  }, // channel 12
                                            new AdcPinSettings { AdcPin = GpioPin.PC3  }, // channel 13
                                            new AdcPinSettings { AdcPin = GpioPin.PA2  }, // channel 14
                                            new AdcPinSettings { AdcPin = GpioPin.PA3  }, // channel 15
                                            new AdcPinSettings { AdcPin = GpioPin.PA0  }, // channel 16
                                            new AdcPinSettings { AdcPin = GpioPin.PA1  }, // channel 17
                                            new AdcPinSettings { AdcPin = GpioPin.PA4  }, // channel 18
                                            new AdcPinSettings { AdcPin = GpioPin.PA5  }, // channel 19
                                       },
                  new AdcPinSettings[] {    new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 0
                                            new AdcPinSettings { AdcPin = GpioPin.PF13 }, // channel 1
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 2
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 3
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 4
                                            new AdcPinSettings { AdcPin = GpioPin.NONE }, // channel 5
                                            new AdcPinSettings { AdcPin = GpioPin.PF14 }, // channel 6                                            
                  },                  
            };
        }
    }
}
