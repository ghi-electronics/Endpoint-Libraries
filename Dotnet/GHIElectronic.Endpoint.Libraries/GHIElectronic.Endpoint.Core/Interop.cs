using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Libraries {    
    public partial class Interop {
        public enum FileOpenFlags
        {
            O_RDONLY = 0x00,
            O_RDWR = 0x02,
            O_NONBLOCK = 0x800,
            O_SYNC = 0x101000
        }

        public enum SeekFlags
        {
            SEEK_SET = 0
        }

        [Flags]
        public enum MemoryMappedProtections
        {
            PROT_NONE = 0x0,
            PROT_READ = 0x1,
            PROT_WRITE = 0x2,
            PROT_EXEC = 0x4
        }

        [Flags]
        public enum MemoryMappedFlags
        {
            MAP_SHARED = 0x01,
            MAP_PRIVATE = 0x02,
            MAP_FIXED = 0x10
        }
        public static int Open(string pathname, FileOpenFlags flags) => open(pathname, flags);
        public static int Read(int fd, IntPtr buf, int count) => read(fd, buf, count);
        public static int Write(int fd, IntPtr buf, int count) => write(fd, buf, count);
        public static int Seek(int fd, int offset, SeekFlags whence) => lseek(fd, offset, whence);
        public static int Close(int fd) => close(fd);
        public static int Ioctl(int fd, uint request, IntPtr argp) => ioctl(fd, request, argp);
        public static int Ioctl(int fd, uint request, ulong argp) => ioctl(fd, request, argp);
        public static IntPtr Mmap(IntPtr addr, int length, MemoryMappedProtections prot, MemoryMappedFlags flags, int fd, int offset) => mmap(addr, length, prot, flags, fd, offset);
        public static int Munmap(IntPtr addr, int length) => munmap(addr, length);

        private const string LibcLibrary = "libc";

        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, FileOpenFlags flags);


        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern int read(int fd, IntPtr buf, int count);

        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern int write(int fd, IntPtr buf, int count);

        [DllImport(LibcLibrary)]
        internal static extern int lseek(int fd, int offset, SeekFlags whence);

        [DllImport(LibcLibrary)]
        internal static extern int close(int fd);

        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern int ioctl(int fd, uint request, IntPtr argp);

        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern int ioctl(int fd, uint request, ulong argp);

        [DllImport(LibcLibrary, SetLastError = true)]
        internal static extern IntPtr mmap(IntPtr addr, int length, MemoryMappedProtections prot, MemoryMappedFlags flags, int fd, int offset);

        [DllImport(LibcLibrary)]
        internal static extern int munmap(IntPtr addr, int length);
    }
}
