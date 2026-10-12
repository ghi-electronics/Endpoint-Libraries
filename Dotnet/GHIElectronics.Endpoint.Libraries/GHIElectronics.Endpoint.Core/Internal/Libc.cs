using System.Runtime.InteropServices;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>The C library calls the libraries need, for 32-bit and 64-bit ARM alike</summary>
    internal static partial class Libc {
        private const string Library = "libc";

        public const int O_RDONLY = 0x0000;
        public const int O_WRONLY = 0x0001;
        public const int O_RDWR = 0x0002;
        public const int O_CLOEXEC = 0x80000;
        public const int O_SYNC = 0x101000;

        public const int PROT_READ = 0x1;
        public const int PROT_WRITE = 0x2;
        public const int MAP_SHARED = 0x01;
        public static readonly nint MAP_FAILED = -1;

        public const int EBUSY = 16;

        [LibraryImport(Library, EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int Open(string path, int flags);

        [LibraryImport(Library, EntryPoint = "close", SetLastError = true)]
        public static partial int Close(int fd);

        [LibraryImport(Library, EntryPoint = "ioctl", SetLastError = true)]
        public static partial int Ioctl(int fd, nuint request, nint argument);

        [LibraryImport(Library, EntryPoint = "write", SetLastError = true)]
        public static partial nint Write(int fd, nint buffer, nuint count);

        // mmap64 takes a 64-bit file offset on 32-bit and 64-bit ARM alike
        [LibraryImport(Library, EntryPoint = "mmap64", SetLastError = true)]
        public static partial nint Mmap(nint address, nuint length, int protection, int flags, int fd, long offset);

        [LibraryImport(Library, EntryPoint = "munmap", SetLastError = true)]
        public static partial int Munmap(nint address, nuint length);

        /// <summary>An IOException with the reason of the last failed call ("No such device", ...)</summary>
        public static IOException Error(string what) {
            var errno = Marshal.GetLastPInvokeError();
            return new IOException($"{what}: {Marshal.GetPInvokeErrorMessage(errno)}", errno);
        }
    }
}
