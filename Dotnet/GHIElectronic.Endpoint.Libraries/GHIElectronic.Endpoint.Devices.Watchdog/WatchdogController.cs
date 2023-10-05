using GHIElectronic.Endpoint.Core;

namespace GHIElectronic.Endpoint.Devices.Watchdog {
    public class WatchdogController {
        
        public uint MaxTimeout { get; } = 32;
        public uint Timeout { get; private set; }
        public bool Start(uint timeout) {
            if (timeout == 0 || timeout > this.MaxTimeout) {
                throw new Exception("Invalid Timeout");
            }            

            var arg = string.Format("-T {0} /dev/watchdog", timeout);
            var script = new Script("watchdog", "./", arg);

            script.Start();

            if (script.ExitCode == 0 && script.Output == "") {
                this.Timeout = timeout;
                return true;
            }

            return false;
        }

        public bool Started {

            get  {
                var arg = "watchdog";

                var script = new Script("ghi_ps.sh", "./", arg);

                script.Start();

                if (script.Output.Contains("-T") && script.Output.Contains("/dev/watchdog"))
                    return true;

                return false;
            }

               
        }

        public bool Stop() {
            if (this.Started) {
                var arg = "watchdog";

                var script = new Script("killall", "./", arg);

                script.Start();

                if ((script.Error == null || script.Error.Length == 0) && script.ExitCode == 0 && script.Output.Length == 0)
                    return true;

               
            }

            return false;

        }

    }
}
