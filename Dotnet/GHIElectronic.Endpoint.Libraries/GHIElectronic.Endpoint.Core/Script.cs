using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Core {
    public class Script {
        private Process process;
        private string error = string.Empty;
        private string output = string.Empty;
        private int exit_code = 0;
        public Script(string filename, string workingdir, string arguments) {
            this.process = new Process();
            var processStartInfo = new ProcessStartInfo() {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = filename,
                WorkingDirectory = workingdir ?? null,
                Arguments = arguments ?? null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            this.process.StartInfo = processStartInfo;
        }

        public void Start(bool waitforexit = true) {
            try {
                this.process.Start();

                if (waitforexit) {
                    this.error = this.process.StandardError.ReadToEnd();
                    this.output = this.process.StandardOutput.ReadToEnd();


                    this.process.WaitForExit();
                    this.exit_code = this.process.ExitCode;
                }
            }
            catch (Exception ex) {
                this.error = "\nException: " + ex.ToString();
            }
        }
        public string Error => this.error;
        public string Output => this.output;
        public int ExitCode => this.exit_code;


    }
}
