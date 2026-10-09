using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>The processor's registers, for the libraries (see Internal/PhysicalMemory.cs)</summary>
    internal static class Register {
        public static uint Read(uint register) => PhysicalMemory.Read(register);

        public static void Write(uint register, uint value) => PhysicalMemory.Write(register, value);

        public static void SetBits(uint register, uint bits) => PhysicalMemory.Modify(register, 0, bits);

        public static void ClearBits(uint register, uint bits) => PhysicalMemory.Modify(register, bits, 0);
    }
}
