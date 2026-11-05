using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Chip-neutral entry point for I²C setup. Call once before using
    /// <see cref="System.Device.I2c.I2cDevice"/>: it brings up the bus and
    /// muxes its pins. Dispatches to the right chip helper based on the
    /// board detected at startup.
    /// </summary>
    /// <example>
    /// <code>
    /// I2cController.Initialize(EPN256.I2c.I2c5);
    /// using var dev = I2cDevice.Create(new I2cConnectionSettings(EPN256.I2c.I2c5, 0x50));
    /// dev.WriteRead(stackalloc byte[] { 0x00 }, buffer);
    /// </code>
    /// Change <c>EPN256</c> to <c>EPM815</c> or <c>EPN128</c> to target a
    /// different board; nothing else has to change.
    /// </example>
    public static class I2cController {
        /// <summary>
        /// Connects the bus's pins and sets its speed. Only the two pins of
        /// this bus are claimed; every other bus's pins stay free.
        /// </summary>
        /// <param name="bus">A bus constant (for example <c>EPN256.I2c.I2c5</c>).</param>
        /// <param name="frequency">Bus speed in Hz (default 400 kHz).</param>
        public static void Initialize(int bus, int frequency = 400_000) {
            if (ChipDetect.IsMa35) EPN256.I2c.Initialize(bus, frequency);
            else                   EPM815.I2c.Initialize(bus, frequency);
        }

        /// <summary>Releases the bus's pins back to free GPIO.</summary>
        public static void UnInitialize(int bus) {
            if (ChipDetect.IsMa35) EPN256.I2c.UnInitialize(bus);
            else                   EPM815.I2c.UnInitialize(bus);
        }

        /// <summary>Current bus speed in Hz.</summary>
        public static int GetFrequency(int bus) =>
            ChipDetect.IsMa35 ? EPN256.I2c.GetFrequency(bus) : EPM815.I2c.GetFrequency(bus);
    }
}
