using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.Watchdog {
    public class WatchdogController {


        private const string LibNativeWatchdog = "nativewatchdog.so";
        internal IntPtr invalidHandleValue;

        public WatchdogController() {
            this.invalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(WatchdogController).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libname) => {
                if (assembly != currentAssembly || libname != LibNativeWatchdog) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };

        }

        public uint MaxTimeout { get; } = 32;
        public uint Timeout { get; private set; }
        public bool Start(uint second) {
            if (second == 0 || second > this.MaxTimeout) {
                throw new Exception("Invalid Timeout");
            }

            this.Timeout = second;

            return native_wd_enable((int)second) >=0;


        }

        public void Reset() {
            native_wd_reset(); ;
        }

        

        [DllImport(LibNativeWatchdog)]
        internal static extern int native_wd_enable(int timeout);

        [DllImport(LibNativeWatchdog)]
        internal static extern int native_wd_reset();     

    }
}
