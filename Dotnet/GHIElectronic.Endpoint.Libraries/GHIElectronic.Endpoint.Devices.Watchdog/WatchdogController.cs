using GHIElectronic.Endpoint.Core;

namespace GHIElectronic.Endpoint.Devices.Watchdog {
    public class WatchdogController {
        public static bool Started { get; private set; }
        public uint MaxTimeout { get; } = 32;
        public uint Timeout { get; set; }
        public bool Start() {
            if (this.Timeout == 0 || this.Timeout > this.MaxTimeout) {
                throw new Exception("Invalid Timeout");
            }

            var arg = string.Format("-T {0} /dev/watchdog", this.Timeout);
            var script = new Script("watchdog", "./", arg);

            if (script.ExitCode == 0 && script.Output == "") {
                Started = true;
                return true;
            }

            return false;
        }

    }
}
