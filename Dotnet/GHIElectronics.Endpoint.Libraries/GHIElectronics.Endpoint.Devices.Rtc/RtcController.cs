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
            var arg = string.Empty;
            var ret = true;
            var ret_output = string.Empty;

            Script script;

            // for wakeup pin
            if (wakeupPin) {
                arg = "gpio_keys.ko";

                script = new Script("insmod", "/lib/modules/5.13.0/kernel/drivers/input/keyboard", arg);

                script.Start();
            }
            

            arg = "--date ";

            arg += dtwakeup.Year.ToString("d4");
            arg += dtwakeup.Month.ToString("d2");
            arg += dtwakeup.Day.ToString("d2");
            arg += dtwakeup.Hour.ToString("d2");
            arg += dtwakeup.Minute.ToString("d2");
            arg += dtwakeup.Second.ToString("d2");

            script = new Script("rtcwake", "./", arg);

            script.Start();

            if (script.Output.Contains("wakeup from \"suspend\" using /dev/rtc0 at") == false) {
                ret = false;
                //throw new Exception(script.Output);
                ret_output  = script.Output;
            }

            // remove wakeup
            if (wakeupPin) {
                arg = "gpio_keys.ko";

                script = new Script("rmmod", "/lib/modules/5.13.0/kernel/drivers/input/keyboard", arg);

                script.Start();
            }

            if (!ret) {
                throw new Exception(ret_output);
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
