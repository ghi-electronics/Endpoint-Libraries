using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Low-level helpers backed by the native <c>nativeutils.so</c> library:
    /// a CPU wait-for-interrupt and a hardware-assisted JPEG to RGB888
    /// decoder.
    /// </summary>
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

        /// <summary>
        /// Blocks the calling thread with the ARM WFI (wait-for-interrupt)
        /// instruction. The core sleeps until the next interrupt is delivered.
        /// </summary>
        public static void WaitForInterrupt() {
            native_wfi();
        }

        /// <summary>
        /// Decodes a JPEG image to packed 24-bit RGB888 pixels. Uses the
        /// hardware JPEG decoder when the chip has one; otherwise a software
        /// decoder in the native library.
        /// </summary>
        /// <param name="src">JPEG-encoded bytes.</param>
        /// <param name="size">Number of valid bytes in <paramref name="src"/>.</param>
        /// <param name="dest">Destination buffer, large enough for width * height * 3 bytes.</param>
        public static void JpegtoRGB888(byte[] src, int size, byte[] dest) {
            native_jpegrgb888(src, size, dest); ;
        }

        [DllImport(LibNativeUtils)]
        internal static extern int native_wfi();

        [DllImport(LibNativeUtils)]
        internal static extern int native_jpegrgb888(byte[] src, int size, byte[] dest);

    }
}
