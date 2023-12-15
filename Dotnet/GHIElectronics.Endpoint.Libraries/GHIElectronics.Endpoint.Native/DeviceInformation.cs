using System.Diagnostics;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Native {

    public static class Power {
        public static void Reboot() {
            var script = new Script("reboot", "./", "");
            script.Start();
        }

        public static void Shutdown() {
            const uint RCC_BASE = 0x50000000U; // page 157
            const uint RCC_MP_SREQSETR = RCC_BASE + 0x104U; // page 157
            const uint RCC_MP_GRSTCSETR = RCC_BASE + 0x404U; // page 157
            const uint RCC_MP_RSTSCLRR = RCC_BASE + 0x408U; // page 157

            const uint PWR_BASE = 0x50001000U; // page 157

            const uint PWR_CR1 = PWR_BASE + 0U; // 442
            const uint PWR_MPUCR = PWR_BASE + 0x10U; //
            const uint PWR_MCUCR = PWR_BASE + 0x14U; //

            Register.SetBits(PWR_MPUCR, 1 << 9); //PWR_MPUCR_CSSF
            Register.SetBits(PWR_MPUCR, 1 << 3); //PWR_MPUCR_CSTBYDIS
            Register.SetBits(PWR_MPUCR, 1 << 0); //PWR_MPUCR_PDDS

            Register.SetBits(PWR_MCUCR, 1 << 0); //PWR_MCUCR_PDDS

            Register.SetBits(PWR_CR1, 1 << 0); //LPDS
            Register.SetBits(PWR_CR1, 1 << 1); //LPCFG
            Register.SetBits(PWR_CR1, 1 << 2); //LVDS

            Register.Write(RCC_MP_RSTSCLRR, 0x7FF);//RCC_MP_RSTSCLRR
            Register.Write(RCC_MP_SREQSETR, 1 << 0 | 1 << 1);//RCC_MP_SREQSETR_STPREQ
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
            
                var script = new Script("csharp_generate_startup.sh", "./", "1 /etc/init.d/S90custom " + application_fullpath);

                script.Start();                                                
        }

        public static void DisableAutoRun() {
            var script = new Script("csharp_generate_startup.sh", "./", "0 /etc/init.d/S90custom");

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
