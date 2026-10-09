using System.Diagnostics;
using System.Text;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Runs a program and collects what it prints. For the libraries'
    /// remaining uses of Linux tools; prefer a direct API where there is one.
    /// </summary>
    public class Script {
        private readonly ProcessStartInfo startInfo;
        private readonly StringBuilder output = new();
        private readonly StringBuilder error = new();
        private readonly object gate = new();
        private Process process;
        private int exitCode;
        private volatile bool running;

        public delegate void OutputDataRecivedEventHandler(Script sender, string received);

        /// <summary>Each line the program prints, as it prints it</summary>
        public event OutputDataRecivedEventHandler OutputDataRecivedEvent;

        public bool Busy => this.running;

        public Script(string filename, string workingdir, string arguments) =>
            this.startInfo = new ProcessStartInfo(filename, arguments ?? string.Empty) {
                WorkingDirectory = workingdir ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

        /// <summary>Starts the program and, unless told otherwise, waits until it ends</summary>
        public void Start(bool waitforexit = true) {
            lock (this.gate) {
                this.output.Clear();
                this.error.Clear();
                this.exitCode = 0;
            }

            var started = new Process { StartInfo = this.startInfo };
            // Both outputs are read as they come: a program that prints a lot
            // cannot fill a pipe and block
            started.OutputDataReceived += (_, e) => {
                if (e.Data is null) {
                    return;
                }
                lock (this.gate) {
                    this.output.Append(e.Data).Append('\n');
                }
                if (e.Data.Length > 0) {
                    this.OutputDataRecivedEvent?.Invoke(this, e.Data);
                }
            };
            started.ErrorDataReceived += (_, e) => {
                if (e.Data is not null) {
                    lock (this.gate) {
                        this.error.Append(e.Data).Append('\n');
                    }
                }
            };

            try {
                started.Start();
            }
            catch (Exception e) {
                // For example the program does not exist: not a success
                lock (this.gate) {
                    this.error.Append(e.Message);
                    this.exitCode = -1;
                }
                started.Dispose();
                return;
            }

            lock (this.gate) {
                this.process = started;
            }
            this.running = true;
            started.BeginOutputReadLine();
            started.BeginErrorReadLine();

            if (waitforexit) {
                this.WaitForEnd(started);
            }
            else {
                Task.Run(() => this.WaitForEnd(started));
            }
        }

        /// <summary>Ends the program (and what it started)</summary>
        public void Stop() {
            lock (this.gate) {
                if (this.running && this.process is not null) {
                    try {
                        this.process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) {
                        // It ended meanwhile
                    }
                }
            }
        }

        public string Error {
            get {
                lock (this.gate) {
                    return this.error.ToString();
                }
            }
        }

        public string Output {
            get {
                lock (this.gate) {
                    return this.output.ToString();
                }
            }
        }

        /// <summary>The program's exit code; -1 if it could not be started</summary>
        public int ExitCode {
            get {
                lock (this.gate) {
                    return this.exitCode;
                }
            }
        }

        private void WaitForEnd(Process started) {
            started.WaitForExit();  // also waits for the last output
            lock (this.gate) {
                this.exitCode = started.ExitCode;
                this.process = null;
            }
            this.running = false;
            started.Dispose();
        }
    }
}
