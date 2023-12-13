using System.Diagnostics;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Native {

    public static class Power {
        public static void Reboot() {
            var script = new Script("reboot", "./", "");
            script.Start();
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
