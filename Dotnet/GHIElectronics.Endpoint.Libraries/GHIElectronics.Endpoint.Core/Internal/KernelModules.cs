using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Loads Linux drivers that the firmware builds as modules: they cost
    /// nothing at boot and load the first time a program needs them.
    /// </summary>
    internal static class KernelModules {
        /// <summary>Loads <paramref name="module"/> (nothing happens if it is loaded already)</summary>
        public static void Load(string module, string parameter = null) {
            var start = new ProcessStartInfo("modprobe") {
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(module);
            if (parameter is not null) {
                start.ArgumentList.Add(parameter);
            }

            using var process = Process.Start(start);
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000)) {
                process.Kill();
                throw new IOException($"Loading the {module} driver took too long");
            }
            if (process.ExitCode != 0) {
                throw new IOException($"Cannot load the {module} driver: {error.Result.Trim()}");
            }
        }

        /// <summary>Waits until a driver that just loaded has made <paramref name="path"/></summary>
        public static void WaitFor(string path) {
            var watch = Stopwatch.StartNew();
            while (!File.Exists(path) && !Directory.Exists(path)) {
                if (watch.ElapsedMilliseconds > 1000) {
                    throw new IOException($"{path} did not appear");
                }
                Thread.Sleep(5);
            }
        }
    }
}
