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
        //public static string GetUntil(this string that, char @char) {
        //    return that[..(IndexOf() == -1 ? that.Length : IndexOf())];
        //    int IndexOf() => that.IndexOf(@char);
        //}

        //public static string FindAndSplitUntil(string str, string substr, char until) {
        //    var pos_start = str.IndexOf(substr);

        //    if (pos_start == -1) {
        //        return string.Empty;
        //    }

        //    var pos_end = pos_start + substr.Length;

        //    while (pos_end < str.Length) {
        //        if (str[pos_end] == until) {
        //            break;
        //        }

        //        pos_end++;
        //    }

        //    return str.Substring(pos_start, pos_end - pos_start);

        //}
    }
}
