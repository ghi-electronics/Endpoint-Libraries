using System.Device.Gpio;
using System.Diagnostics;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Native {

    public static class Power {

        public enum WakeupPin : uint {
            PA0 = 0,
            PA2 = 1,
            PC13 = 2,
            PI8 = 3,
            PI11 = 4,
            PC1 = 5,
            None = 0xFF


        }
        public static void Reboot() {
            var script = new Script("reboot", "./", "");
            script.Start();
        }

        public static void Shutdown() {
            const uint RCC_BASE = 0x50000000U; // page 157
            const uint RCC_MP_SREQSETR = RCC_BASE + 0x104U; 
            //const uint RCC_MP_GRSTCSETR = RCC_BASE + 0x404U; 
            const uint RCC_MP_RSTSCLRR = RCC_BASE + 0x408U; 
            const uint RCC_PWRLPDLYCR = RCC_BASE + 0x41CU; 
            const uint RCC_MP_CIFR = RCC_BASE + 0x418U; 
            const uint RCC_MP_CIER = RCC_BASE + 0x414U; 

            const uint PWR_BASE = 0x50001000U; 

            const uint PWR_CR1 = PWR_BASE + 0U; // 442
            const uint PWR_MPUCR = PWR_BASE + 0x10U; //
            const uint PWR_MCUCR = PWR_BASE + 0x14U; //

            const uint RCC_PWRLPDLYCR_PWRLP_DLY_MASK = 0x3FFFFFU;

            Register.ClearBits(RCC_PWRLPDLYCR, RCC_PWRLPDLYCR_PWRLP_DLY_MASK);
            Register.SetBits(RCC_PWRLPDLYCR, 5); //Wait 6 HSI periods

            /* Clear RCC interrupt before enabling it */
            Register.SetBits(RCC_MP_CIFR, 1 << 20); //RCC_MP_CIFR_WKUPF

            /* Enable RCC Wake-up */
            Register.SetBits(RCC_MP_CIER, 1 << 20); //RCC_MP_CIFR_WKUPF

            Register.SetBits(PWR_MPUCR, 1 << 9); //PWR_MPUCR_CSSF
            Register.SetBits(PWR_MPUCR, 1 << 3); //PWR_MPUCR_CSTBYDIS
            Register.SetBits(PWR_MPUCR, 1 << 0); //PWR_MPUCR_PDDS

            Register.SetBits(PWR_MCUCR, 1 << 0); //PWR_MCUCR_PDDS

            Register.SetBits(PWR_CR1, 1 << 0); //LPDS
            Register.SetBits(PWR_CR1, 1 << 1); //LPCFG
            Register.SetBits(PWR_CR1, 1 << 2); //LVDS

            Register.Write(RCC_MP_RSTSCLRR, 0x7FF);//RCC_MP_RSTSCLRR
            Register.Write(RCC_MP_SREQSETR, 1 << 0 | 1 << 1);//RCC_MP_SREQSETR_STPREQ

            for (; ; )
                NativeUtils.WFI();
        }

        public static void EnableWakeup(WakeupPin wakeupPin, PinEventTypes edge) {
            EnableWakeup(System.DateTime.MaxValue, wakeupPin, edge); ;
        }
        public static void EnableWakeup(System.DateTime dtWakeup, WakeupPin wakeupPin = WakeupPin.None, PinEventTypes edge = PinEventTypes.Rising) {
            var flag = true;
            var message = string.Empty;
            Script script;
            string args;

            if (wakeupPin != WakeupPin.None) {

                const uint PWR_BASE = 0x50001000U; // page 157
                const uint PWR_WKUPCR = PWR_BASE + 0x20U; // page 157
                const uint PWR_MPUWKUPENR = PWR_BASE + 0x28U; // page 157

                if (edge == PinEventTypes.None)
                    throw new Exception("PinEventTypes edge supports Rising or Falling only.");


                Register.Write(PWR_MPUWKUPENR, 0); // clear

                var cr = 0x3FU; // clear wakeup flag

                var pin = (int)wakeupPin;
                var polarity = edge == PinEventTypes.Falling ? 0 : 1;

                if (polarity == 0) // detect on low
                {
                    cr |= (uint)((1 << 8) << pin); // detect on low
                    cr |= (uint)((1 << 16) << pin); // pull up
                }
                else {
                    cr |= (uint)((2 << 16) << pin); // pull down
                }

                Register.Write(PWR_WKUPCR, cr); // set
                Register.Write(PWR_MPUWKUPENR, (uint)(1 << pin)); // set
            }

            if (dtWakeup != System.DateTime.MaxValue) {
                script = new Script("rtcwake_clear.sh", "./", "");
                script.Start();


                var dt = dtWakeup.Year.ToString("d4") + "-" + dtWakeup.Month.ToString("d2") + "-" + dtWakeup.Day.ToString("d2");

                dt += " ";
                dt += dtWakeup.Hour.ToString("d2") + ":" + dtWakeup.Minute.ToString("d2") + ":" + dtWakeup.Second.ToString("d2");

                args = $"-m no --date \"{dt}\"";

                script = new Script("rtcwake", "./", args);
                script.Start();
                if (!script.Output.Contains("wakeup using /dev/rtc0 at")) {
                    flag = false;
                    message = script.Error;


                }
            }

            if (!flag) {
                throw new Exception(message);
            }
        }
    }
    public static class DeviceInformation {

        public static string Version => GetVersion();
        private static string GetVersion() {


            var script = new Script("get_version.sh", "./", "");
            script.Start();

            var version = script.Output.Replace("\n", "");
            return version;

        }

        public static void EnableAutoRun(string applicationName) {
            var application_fullpath = "/root/.epdata/" + applicationName + "/" + applicationName + ".dll";
            
                var script = new Script("csharp_generate_startup.sh", "./", "1 /etc/init.d/S29custom " + application_fullpath);

                script.Start();                                                
        }

        public static void DisableAutoRun() {
            var script = new Script("csharp_generate_startup.sh", "./", "0 /etc/init.d/S29custom");

            script.Start();
        }

        public static void DisableSSH() {

            var script = new Script("rm", "./", "/etc/init.d/S50dropbear");

            script.Start();

            script = new Script("rm", "./", "/etc/init.d/S30usbgadget");

            script.Start();

        }

        //public static double GetCpuUsageStatistic() {
        //    var script = new Script("get_cpu_usage.sh", "./", "");

        //    script.Start();

        //    var ret = double.Parse(script.Output);

        //    return ret;
        //}

        
        public static bool SdBoot() {
            var script = new Script("cat", "./", "/boot/extlinux/extlinux.conf");

            script.Start();

            if (script.Output.Contains("mmcblk1"))
                return true;

            return false;

        }
    }
}
