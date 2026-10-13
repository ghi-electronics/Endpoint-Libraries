using System.Runtime.InteropServices;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// libendpoint, the board's library that .NET, Python and C++ share for the
    /// features with real logic (storage). It comes in this package
    /// (runtimes/linux-arm/native). Every function returns 0, or the kind of
    /// error: <see cref="Check"/> throws the matching .NET exception.
    /// </summary>
    internal static unsafe partial class Libendpoint {
        private const string Library = "libendpoint";

        [LibraryImport(Library, EntryPoint = "endpoint_last_error")]
        private static partial byte* LastError();

        public static void Check(int result) {
            if (result == 0) {
                return;
            }
            var message = Marshal.PtrToStringUTF8((nint)LastError()) ?? "";
            throw result switch {
                1 => new ArgumentException(message),
                2 => new ArgumentOutOfRangeException(null, message),
                3 => new InvalidOperationException(message),
                5 => new NotSupportedException(message),
                6 => new ObjectDisposedException(null, message),
                7 => new NotImplementedException(message),
                8 => new TimeoutException(message),
                _ => new IOException(message),
            };
        }

        // SD cards and USB drives: the callback (context, hdc, status, volume label) runs on the library's storage thread

        [LibraryImport(Library, EntryPoint = "endpoint_storage_create", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int StorageCreate(string name, delegate* unmanaged<nint, nint, int, byte*, void> callback,
                                                nint context, out nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_dispose")]
        public static partial int StorageDispose(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_enable")]
        public static partial int StorageEnable(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_disable")]
        public static partial int StorageDisable(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_mount")]
        public static partial int StorageMount(nint hdc, byte* name, int nameSize);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_flush")]
        public static partial int StorageFlush(nint hdc);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_unmount")]
        public static partial int StorageUnmount(nint hdc);
    }
}
