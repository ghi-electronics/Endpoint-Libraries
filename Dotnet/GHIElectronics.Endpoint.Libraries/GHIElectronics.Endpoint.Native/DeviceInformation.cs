using System.Device.Gpio;
using System.Diagnostics;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Native {

    /// <summary>Reboot, shutdown and wake-up control for the Endpoint board.</summary>
    public static class Power {

        /// <summary>GPIO pins the power block can monitor to wake the system from standby.</summary>
        public enum WakeupPin : uint {
            /// <summary>Wake-up input 1 (PA0).</summary>
            PA0 = 0,
            /// <summary>Wake-up input 2 (PA2).</summary>
            PA2 = 1,
            /// <summary>Wake-up input 3 (PC13).</summary>
            PC13 = 2,
            /// <summary>Wake-up input 4 (PI8).</summary>
            PI8 = 3,
            /// <summary>Wake-up input 5 (PI11).</summary>
            PI11 = 4,
            /// <summary>Wake-up input 6 (PC1).</summary>
            PC1 = 5,
            /// <summary>No wake-up pin selected; only an RTC alarm can wake the system.</summary>
            None = 0xFF


        }
        /// <summary>Reboots the board by invoking <c>reboot</c>. The call does not return once the system restarts.</summary>
        public static void Reboot() {
            var script = new Script("reboot", "./", "");
            script.Start();
        }

        /// <summary>
        /// Puts the board into standby by programming the power and clock registers directly.
        /// Blocks forever in a wait-for-interrupt loop; execution resumes only after a wake-up event resets the processor.
        /// </summary>
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
                NativeUtils.WaitForInterrupt();
        }

        /// <summary>Arms a GPIO wake-up input so a transition on it brings the system out of standby.</summary>
        /// <param name="wakeupPin">The pin to monitor. See <see cref="WakeupPin"/>.</param>
        /// <param name="edge"><see cref="PinEventTypes.Rising"/> wakes on a high level with pull-down enabled; <see cref="PinEventTypes.Falling"/> wakes on a low level with pull-up enabled. <see cref="PinEventTypes.None"/> is not supported and throws.</param>
        public static void EnableWakeup(WakeupPin wakeupPin, PinEventTypes edge) {
            EnableWakeup(System.DateTime.MaxValue, wakeupPin, edge); ;
        }
        /// <summary>
        /// Arms an RTC alarm and, optionally, a GPIO wake-up input so either event brings the system out of standby.
        /// The RTC alarm is scheduled via <c>rtcwake</c>; pass <see cref="System.DateTime.MaxValue"/> to skip the RTC and arm only the pin.
        /// </summary>
        /// <param name="dtWakeup">Absolute time at which to wake. Use <see cref="System.DateTime.MaxValue"/> for no RTC alarm.</param>
        /// <param name="wakeupPin">Optional wake-up pin; defaults to <see cref="WakeupPin.None"/> (no pin wake).</param>
        /// <param name="edge">Active edge for the pin wake. Defaults to <see cref="PinEventTypes.Rising"/>. <see cref="PinEventTypes.None"/> is not supported and throws.</param>
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
    /// <summary>Board identity, firmware version, and boot/auto-run configuration helpers.</summary>
    public static class DeviceInformation {

        /// <summary>Firmware version string as reported by <c>get_version.sh</c>, with any trailing newline stripped.</summary>
        public static string Version => GetVersion();
        private static string GetVersion() {


            var script = new Script("get_version.sh", "./", "");
            script.Start();

            var version = script.Output.Replace("\n", "");
            return version;

        }

        /// <summary>
        /// Installs an init.d entry (<c>/etc/init.d/S29custom</c>) that starts the given deployed .NET application at boot.
        /// The application is expected at <c>/root/.epdata/{applicationName}/{applicationName}.dll</c>.
        /// </summary>
        /// <param name="applicationName">Folder and assembly name (without <c>.dll</c>) under <c>/root/.epdata/</c>.</param>
        public static void EnableAutoRun(string applicationName) {
            var application_fullpath = "/root/.epdata/" + applicationName + "/" + applicationName + ".dll";

                var script = new Script("csharp_generate_startup.sh", "./", "1 /etc/init.d/S29custom " + application_fullpath);

                script.Start();
        }

        /// <summary>Removes the auto-run init.d entry installed by <see cref="EnableAutoRun(string)"/>.</summary>
        public static void DisableAutoRun() {
            var script = new Script("csharp_generate_startup.sh", "./", "0 /etc/init.d/S29custom");

            script.Start();
        }

        /// <summary>
        /// Permanently removes the SSH (<c>S50dropbear</c>) and USB gadget (<c>S30usbgadget</c>) init.d entries,
        /// so neither service starts at the next boot. Intended for release builds only.
        /// </summary>
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

        
        /// <summary>
        /// Returns <c>true</c> when the board booted from the SD card, <c>false</c>
        /// when it booted from the on-board eMMC.
        /// </summary>
        public static bool SdBoot() {
            var script = new Script("cat", "./", "/boot/extlinux/extlinux.conf");

            script.Start();

            if (script.Output.Contains("mmcblk1"))
                return true;

            return false;

        }
    }
}
