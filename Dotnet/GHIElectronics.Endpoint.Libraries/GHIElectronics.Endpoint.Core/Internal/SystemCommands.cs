using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Runs the system's own tools (modprobe, ip...) where they are the
    /// standard way to do something: the arguments go straight to the tool,
    /// with no shell, and its error message becomes the exception's message.
    /// </summary>
    internal static class SystemCommands {
        /// <summary>Runs <paramref name="program"/>; throws IOException("<paramref name="what"/>: reason") if it fails</summary>
        public static void Run(string program, string what, params string[] arguments) {
            var start = new ProcessStartInfo(program) {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments) {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000)) {
                process.Kill();
                throw new IOException($"{what}: {program} took too long");
            }
            if (process.ExitCode != 0) {
                var reason = error.Result.Trim();
                throw new IOException($"{what}: {(reason.Length > 0 ? reason : output.Result.Trim())}");
            }
        }
    }
}
