namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// The processor's registers, through /dev/mem. Each 4 KB page is mapped
    /// once and kept for the life of the process, and every access is
    /// locked, so changes from several threads cannot mix.
    /// </summary>
    internal static unsafe class PhysicalMemory {
        private const ulong PageSize = 4096;

        private static readonly object Gate = new();
        private static readonly Dictionary<ulong, nint> Pages = new();
        private static int memory = -1;

        public static uint Read(ulong address) {
            lock (Gate) {
                return Volatile.Read(ref *Register(address));
            }
        }

        public static void Write(ulong address, uint value) {
            lock (Gate) {
                Volatile.Write(ref *Register(address), value);
            }
        }

        /// <summary>Clears then sets bits, as one step for other threads</summary>
        public static void Modify(ulong address, uint clear, uint set) {
            lock (Gate) {
                var register = Register(address);
                Volatile.Write(ref *register, (Volatile.Read(ref *register) & ~clear) | set);
            }
        }

        // Caller holds Gate
        private static uint* Register(ulong address) {
            if ((address & 3) != 0) {
                throw new ArgumentException($"Register address 0x{address:X} is not 4-byte aligned", nameof(address));
            }

            var page = address & ~(PageSize - 1);
            if (!Pages.TryGetValue(page, out var mapped)) {
                if (memory < 0) {
                    memory = Libc.Open("/dev/mem", Libc.O_RDWR | Libc.O_SYNC | Libc.O_CLOEXEC);
                    if (memory < 0) {
                        throw Libc.Error("Cannot open /dev/mem (the app must run as root)");
                    }
                }

                mapped = Libc.Mmap(0, (nuint)PageSize, Libc.PROT_READ | Libc.PROT_WRITE, Libc.MAP_SHARED, memory, (long)page);
                if (mapped == Libc.MAP_FAILED) {
                    throw Libc.Error($"Cannot map the registers at 0x{page:X}");
                }
                Pages[page] = mapped;
            }

            return (uint*)(mapped + (nint)(address - page));
        }
    }
}
