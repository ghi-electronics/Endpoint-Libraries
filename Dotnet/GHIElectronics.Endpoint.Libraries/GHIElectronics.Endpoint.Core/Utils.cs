using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static class NativeUtils {

        private const string LibNativeUtils = "nativeutils.so";
        internal static IntPtr InvalidHandleValue;

        static NativeUtils() {
            InvalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(NativeUtils).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libmytestlibName) => {
                if (assembly != currentAssembly || libmytestlibName != LibNativeUtils) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };

        }

        public static void WFI() {
            native_wfi(); ;
        }

        [DllImport(LibNativeUtils)]
        internal static extern int native_wfi();
        
    }
}
