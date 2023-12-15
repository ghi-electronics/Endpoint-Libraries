using System;
using System.Linq;
using System.Xml.Linq;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.Rtc {
    public enum BatteryChargeMode {
        None = 0,
        Fast = 1,
        Slow = 2
    }

    public class RtcController : IDisposable {
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

        public DateTime GetDateTime() {
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

        private static string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"};
        public void SetDateTime(DateTime value) {

            //date -s 2023.08.31-17:02:00
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

            arg = "-w";
            script = new Script("hwclock", "/sbin/", arg);

            script.Start();
        }

        public void EnableWakeup(bool wakeupPin, DateTime dtwakeup) {
            var flag = true;
            var message = string.Empty;
            Script script;
            string args;
            if (wakeupPin) {
                args = "gpio_keys.ko";
                script = new Script("insmod", "/lib/modules/5.13.0/kernel/drivers/input/keyboard", args);
                script.Start();
            }


            script = new Script("rtcwake_clear.sh", "./", "");
            script.Start();


            var dt = dtwakeup.Year.ToString("d4") + "-" + dtwakeup.Month.ToString("d2") + "-" + dtwakeup.Day.ToString("d2");

            dt += " ";
            dt += dtwakeup.Hour.ToString("d2") + ":" + dtwakeup.Minute.ToString("d2") + ":" + dtwakeup.Second.ToString("d2");

            args = $"-m no --date \"{dt}\"";

            script = new Script("rtcwake", "./", args);
            script.Start();
            if (!script.Output.Contains("wakeup using /dev/rtc0 at")) {
                flag = false;
                message = script.Error;


            }

            if (wakeupPin) {
                args = "gpio_keys.ko";
                script = new Script("rmmod", "/lib/modules/5.13.0/kernel/drivers/input/keyboard", args);
                script.Start();
            }

            if (!flag) {
                throw new Exception(message);
            }
        }

        public DateTime Now {
            get => this.GetDateTime();
            set => this.SetDateTime(value);
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
        ~RtcController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                
            }


            this.Release();

            this.disposed = true;
        }

    }
}
