using System.Diagnostics;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// Loads Linux drivers that the firmware builds as modules: they cost
    /// nothing at boot and load the first time a program needs them.
    /// </summary>
    internal static class KernelModules {
        /// <summary>Loads <paramref name="module"/> (nothing happens if it is loaded already)</summary>
        public static void Load(string module, string parameter = null) {
            var what = $"Cannot load the {module} driver";
            if (parameter is null) {
                SystemCommands.Run("modprobe", what, module);
            }
            else {
                SystemCommands.Run("modprobe", what, module, parameter);
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
