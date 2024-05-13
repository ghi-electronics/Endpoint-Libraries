using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public class Script {
        private Process process;
        private string error = string.Empty;
        private string output = string.Empty;
        private int exit_code = 0;

        public delegate void OutputDataRecivedEventHandler(Script sender, string received);
        public event OutputDataRecivedEventHandler OutputDataRecivedEvent = null;
        private bool waitingForResponse = false;

        public bool Busy => this.waitingForResponse;
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

        private void ThreadReadLines() {
            while (this.waitingForResponse) {
                var line = this.process.StandardOutput.ReadLine();

                if (OutputDataRecivedEvent != null && line != null && line.Length > 0)
                    OutputDataRecivedEvent.Invoke(this, line);

                Thread.Sleep(100);
            }
        }
        public void Start(bool waitforexit = true) {
            this.waitingForResponse = true;

            try {
                this.process.Start();

                if (waitforexit) {
                    if (OutputDataRecivedEvent == null) {
                        this.error = this.process.StandardError.ReadToEnd();
                        this.output = this.process.StandardOutput.ReadToEnd();
                    }

                    else {
                        new Thread(this.ThreadReadLines).Start();
                    }

                    this.process.WaitForExit();
                    this.exit_code = this.process.ExitCode;
                }
            }
            catch (Exception ex) {
                this.error = "\nException: " + ex.ToString();
            }

            this.waitingForResponse = false;
        }
        public void Stop() {
            if (this.waitingForResponse) {
                this.process.Kill();

                this.waitingForResponse = false;
            }
        }
        public string Error => this.error;
        public string Output => this.output;
        public int ExitCode => this.exit_code;


    }
}
