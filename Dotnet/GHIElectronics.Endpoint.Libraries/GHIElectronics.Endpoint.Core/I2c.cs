using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;


namespace GHIElectronics.Endpoint.Core {

    public static partial class EPM815 {
        public static class I2c {
            /// <summary>I2c controller.</summary>
            public const int I2c1 = 0;
            //public static int I2c2 = 2;
            //public static int I2c3 = 3;
            public const int I2c4 = 2;
            public const int I2c5 = 1;
            public const int I2c6 = 3;

            //private static int initialized = 0;
            private static List<int> initializedList = new List<int>();

            internal class I2cPinSettings {
                public int SdaPin { get; set; }
                public int SclPin { get; set; }
                public Gpio.Alternate SdaAlternate { get; set; }
                public Gpio.Alternate SclAlternate { get; set; }
            };

            internal static I2cPinSettings[] PinSettings = {
                /*i2c1*/new I2cPinSettings { SdaPin = Gpio.Pin.PD13, SclPin = Gpio.Pin.PD12, SdaAlternate = Gpio.Alternate.AF5 ,  SclAlternate = Gpio.Alternate.AF5  },
                ///*i2c2*/new I2cPinSettings { SdaPin = Gpio.Pin.NONE, SclPin = Gpio.Pin.NONE, SdaAlternate = Gpio.Alternate.AF0 ,  SclAlternate = Gpio.Alternate.AF0  },
                ///*i2c3*/new I2cPinSettings { SdaPin = Gpio.Pin.NONE, SclPin = Gpio.Pin.NONE, SdaAlternate = Gpio.Alternate.AF0 ,  SclAlternate = Gpio.Alternate.AF0  },                                
                /*i2c5*/new I2cPinSettings { SdaPin = Gpio.Pin.PZ5 , SclPin = Gpio.Pin.PZ4 , SdaAlternate = Gpio.Alternate.AF4 ,  SclAlternate = Gpio.Alternate.AF4  },
                /*i2c4*/new I2cPinSettings { SdaPin = Gpio.Pin.PF15, SclPin = Gpio.Pin.PB6 , SdaAlternate = Gpio.Alternate.AF4 ,  SclAlternate = Gpio.Alternate.AF6 },
                /*i2c6*/new I2cPinSettings { SdaPin = Gpio.Pin.PD0, SclPin = Gpio.Pin.PD1 , SdaAlternate = Gpio.Alternate.AF2 ,  SclAlternate = Gpio.Alternate.AF2 },

            };

            public static void Initialize(int port) => Initialize(port, 0); // default 400KHz
            public static void Initialize(int port, int frequency_hz) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                //if ((initialized & (1 << port)) != 0)
                //    return;

                if (initializedList.Contains(port)) {
                    return;
                    
                }
                //port = port - 1;


                var pinConfig = PinSettings[port];

                if (Gpio.IsPinReserved(pinConfig.SclPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.SclPin);
                }

                if (Gpio.IsPinReserved(pinConfig.SdaPin)) {
                    EPM815.ThrowExceptionPinInUsed(pinConfig.SdaPin);
                }


                // calculation clock:
                const uint RCC_BASE = 0x50000000U;                
                
                var base_address = 0U;
                var clock_en_address = 0U;
                var clock_en_value = 0U;
                var tpresc = 0U;

                if (frequency_hz > 0) {
                    switch (port) {
                        case I2c6:
                        case I2c4:
                            // PRESC = 15+1 = 16;
                            // i2c clock = 64MHz/16 = 4MHz;                            
                            tpresc = 250;//250ns = 1_000_000_000 / 4_000_000 = 250ns
                            clock_en_address = RCC_BASE + 0x208U; //RCC_MP_APB5ENSETR
                           
                            // enable peripheral clock to program register
                            clock_en_value = Register.Read(clock_en_address);

                            if (port == I2c6) {                                
                                clock_en_value |= 1 << 3;
                                base_address = 0x5C009000U;
                            }
                            else {
                                clock_en_value |= 1 << 2;
                                base_address = 0x5C002000U;
                            }

                            Register.Write(clock_en_address, clock_en_value);

                            break;

                        case I2c1:
                        case I2c5:

                            

                            clock_en_address = RCC_BASE + 0xA00U; //RCC_MP_APB5ENSETR
                                                                  // enable peripheral clock to program register
                            clock_en_value = Register.Read(clock_en_address);

                            if (port == I2c1) {
                                clock_en_value |= 1 << 21;
                                base_address = 0x40012000U;
                                tpresc = 250;
                            }
                            else {
                                clock_en_value |= 1 << 24;
                                base_address = 0x40015000;
                                tpresc = 325;
                            }

                            Register.Write(clock_en_address, clock_en_value);
                            break;


                    }

                    var cr1_address = base_address;
                    var timming_address = base_address + 0x10;

                    // Disable I2C - PE bit
                    var i2c_cr1_val = Register.Read(cr1_address);

                    i2c_cr1_val &= ~0x00000001U;

                    Register.Write(cr1_address, i2c_cr1_val);

                    var i2c_timming_value = Register.Read(timming_address);

                    i2c_timming_value &= ~0xF0FFFFFFU;

                    var clockrate = 1000000000 / frequency_hz / tpresc;

                    if (clockrate > 2)
                        clockrate -= 2;
                    else clockrate = 0;

                    var clockhigh = clockrate / 2;
                    i2c_timming_value |= 0xF0000000U;

                    if (clockhigh > 255) clockhigh = 255;

                    if (clockhigh > 0)
                        clockhigh -= 1;

                    i2c_timming_value |= (uint)(clockhigh);
                    i2c_timming_value |= (uint)(clockhigh << 8);                    

                    Register.Write(timming_address, i2c_timming_value);

                    // Enable I2C - PE bit
                    i2c_cr1_val |= 1;
                    Register.Write(cr1_address, i2c_cr1_val);
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

                //initialized |= (1 << port);
                initializedList.Add(port);

            }
            public static void UnInitialize(int port) {

                if (port < I2c1 || port > I2c6) {
                    throw new ArgumentException("Invalid I2c port.");
                }

                if (initializedList.Contains(port)) {

                    //port = port - 1;

                    var pinConfig = PinSettings[port];

                    Gpio.PinRelease(pinConfig.SclPin);
                    Gpio.PinRelease(pinConfig.SdaPin);

                    Gpio.SetModer(pinConfig.SclPin, Gpio.Moder.Input);
                    Gpio.SetModer(pinConfig.SdaPin, Gpio.Moder.Input);
                    //initialized &= ~(1 << port);
                    initializedList.Remove(port);
                }
            }

        }
    }
}
