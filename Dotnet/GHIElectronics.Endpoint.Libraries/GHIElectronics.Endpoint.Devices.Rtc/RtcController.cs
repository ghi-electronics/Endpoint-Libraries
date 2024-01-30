using System;
using System.CodeDom;
using System.Device.Gpio;
using System.Linq;
using System.Xml.Linq;
using GHIElectronics.Endpoint.Core;
using Iot.Device.Rtc;
using UnitsNet;

namespace GHIElectronics.Endpoint.Devices.Rtc {
    public enum BatteryChargeMode {
        None = 0,
        Fast = 1,
        Slow = 2
    }

    public enum WakeupPin: uint {
        PA0 = 0,
        PA2 = 1,
        PC13 = 2,
        PI8 = 3,
        PI11 = 4,
        PC1 = 5,
        None = 0xFF


    }

    public class RtcController : RtcBase {
        static int initializeCount = 0;

        const uint PWR_CR3 = 0x50001000 + 0x0C;
        public RtcController() => this.Acquire();

        public void EnableChargeMode(BatteryChargeMode chargeMode) {
            var read = Register.Read(PWR_CR3);

            switch (chargeMode) {
                case BatteryChargeMode.None:
                    read &= ~(uint)(1 << 8);
                    Register.Write(PWR_CR3, read);

                    break;

                case BatteryChargeMode.Fast:
                    read |= (uint)(3 << 8);
                    Register.Write(PWR_CR3, read);

                    break;

                case BatteryChargeMode.Slow:
                    read |= (uint)(1 << 8);
                    read &= ~(uint)(1 << 9);
                    Register.Write(PWR_CR3, read);
                    break;
            }

        }

        public DateTime GetSystemTime() {
            //var script = new Script("hwclock", "./", "");
            var script = new Script("date", "./", "");

            script.Start();

            if (script.Output.Length > 0) {
                var elements = script.Output.Split(' ');
                var id = 0;

                // month
                var month = -1;
                id++;
                for (var i = 0; i < months.Length; i++) {
                    if (elements[id] == months[i]) {
                        month = i;
                        month++;
                        break;
                    }
                }

                // dayofmonth
                id++;
                if (elements[id] == string.Empty) // day < 10
                    id++;

                var dayofmonth = int.Parse(elements[id]);

                // hour:min:sec
                id++;
                var elements2 = elements[id].Split(':');
                var hour = int.Parse(elements2[0]);
                var minute = int.Parse(elements2[1]);
                var second = int.Parse(elements2[2]);

                //Ignore UTC
                id++;
 
                // year
                id++;
                var year = int.Parse(elements[id]);

                return new DateTime(year, month, dayofmonth, hour, minute, second);

            }

            throw new Exception("Invalid rtc time!");
        }

        private static string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"};
        public void SetSystemTime(DateTime value) {

            //date -s 2024.08.31-17:02:00
            var arg = "-s ";

            arg += value.Year + ".";
            arg += value.Month + ".";
            arg += value.Day;

            arg += "-";
            arg += value.Hour + ":";
            arg += value.Minute + ":";
            arg += value.Second;

            var script = new Script("date", "/sbin/", arg);

            script.Start();

            //arg = "-w";
            //script = new Script("hwclock", "/sbin/", arg);

            //script.Start();
        }

        public void EnableWakeup(WakeupPin wakeupPin, PinEventTypes edge) {
            this.EnableWakeup(System.DateTime.MaxValue, wakeupPin, edge); ;
        }
        public void EnableWakeup(System.DateTime dtWakeup, WakeupPin wakeupPin = WakeupPin.None, PinEventTypes edge = PinEventTypes.Rising) {
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

        public DateTime Now {
            get => this.ReadTime();
            set => this.SetTime(value);
        }

        private void Acquire() {
            if (initializeCount == 0) {
                this.LoadResources();
            }

            initializeCount++;
        }

        private void Release() {
            initializeCount--;

            if (initializeCount == 0) {
                this.UnLoadResources();
            }
        }

        private void LoadResources() {

            
           
        }

        private void UnLoadResources() {
           

           
        }

        private bool disposed = false;

        /// <exclude />
        ~RtcController() {
            this.Dispose(disposing: false);
        }

        public new void Dispose() {
            base.Dispose(); 
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
        protected new void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                
            }


            this.Release();

            this.disposed = true;
        }

        protected override void SetTime(DateTime time) {
            var arg = "-s ";

            arg += time.Year + ".";
            arg += time.Month + ".";
            arg += time.Day;

            arg += "-";
            arg += time.Hour + ":";
            arg += time.Minute + ":";
            arg += time.Second;

            var script = new Script("date", "/sbin/", arg);

            script.Start();

            arg = "-w";
            script = new Script("hwclock", "/sbin/", arg);

            script.Start();
        }
        protected override DateTime ReadTime() {
            var script = new Script("hwclock", "./", "");

            script.Start();

            if (script.Output.Length > 0) {
                var elements = script.Output.Split(' ');
                var id = 0;

                // month
                var month = -1;
                id++;
                for (var i = 0; i < months.Length; i++) {
                    if (elements[id] == months[i]) {
                        month = i;
                        month++;
                        break;
                    }
                }

                // dayofmonth
                id++;
                if (elements[id] == string.Empty) // day < 10
                    id++;

                var dayofmonth = int.Parse(elements[id]);

                // hour:min:sec
                id++;
                var elements2 = elements[id].Split(':');
                var hour = int.Parse(elements2[0]);
                var minute = int.Parse(elements2[1]);
                var second = int.Parse(elements2[2]);

                // year
                id++;
                var year = int.Parse(elements[id]);

                return new DateTime(year, month, dayofmonth, hour, minute, second);

            }

            throw new Exception("Invalid rtc time!");
        }
    }
}
