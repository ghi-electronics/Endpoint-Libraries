using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static class Register {
        const string PATH = "/dev/mem";

        const int MAP_SIZE = 4096;
        const int MAP_MASK = MAP_SIZE - 1;

        static unsafe uint MemRead(uint register) {

            var fd = Interop.Open("/dev/mem", Interop.FileOpenFlags.O_RDWR | Interop.FileOpenFlags.O_RDWR);

            if (fd < 1) {
                throw new Exception("Not supported!");
            }

            var target = (int)register;            

            var mem_base = Interop.Mmap(IntPtr.Zero, MAP_SIZE, Interop.MemoryMappedProtections.PROT_READ | Interop.MemoryMappedProtections.PROT_WRITE, Interop.MemoryMappedFlags.MAP_SHARED, fd, target & ~MAP_MASK);

            var virtual_addr = mem_base + (target & MAP_MASK);

            var value = *(uint*)virtual_addr;

            Interop.Munmap(mem_base, target);

            Interop.Close(fd);

            return value;
        }

        static unsafe void MemWrite(uint register, uint value) {
            var fd = Interop.Open("/dev/mem", Interop.FileOpenFlags.O_RDWR | Interop.FileOpenFlags.O_RDWR);

            if (fd < 1) {
                throw new Exception("Not supported!");
            }

            var target = (int)register;

            var mem_base = Interop.Mmap(IntPtr.Zero, MAP_SIZE, Interop.MemoryMappedProtections.PROT_READ | Interop.MemoryMappedProtections.PROT_WRITE, Interop.MemoryMappedFlags.MAP_SHARED, fd, target & ~MAP_MASK);

            var virtual_addr = mem_base + (target & MAP_MASK);

            *(uint*)virtual_addr = value;

            Interop.Munmap(mem_base, target);

            Interop.Close(fd);

        }

        public static uint Read(uint register) => MemRead(register);
        public static void Write(uint register, uint value) => MemWrite(register, value);


    }
}
