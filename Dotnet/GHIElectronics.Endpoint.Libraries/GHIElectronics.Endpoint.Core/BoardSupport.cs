#nullable enable
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// What a board's <c>Pins.*</c> package registers with Core at startup
    /// so Core's chip-neutral controllers (<see cref="I2cController"/>,
    /// <see cref="SpiController"/>, <see cref="PwmController"/>,
    /// <see cref="CanController"/>, <see cref="SerialPortController"/>) can
    /// drive the board without naming any chip.
    ///
    /// A single implementation lives in the board's Pins package
    /// (<c>Pins.EPM815</c>, <c>Pins.EPN256</c>, ...) and is handed to
    /// <see cref="BoardSupport.Register"/> from a
    /// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>
    /// so adding the NuGet reference is enough to wire everything up.
    ///
    /// Internal because the contract references internal types
    /// (<see cref="Pull"/>, <see cref="OutputType"/>, <see cref="Speed"/>,
    /// <see cref="AnalogInput"/>) and is not meant for customer implementation.
    /// Board packages get access via <c>InternalsVisibleTo</c> in this project.
    /// </summary>
    internal interface IBoardSupport {
        /// <summary>The board's name ("EPM815", "EPDomino24", "EPN256", "EPN128").</summary>
        string Name { get; }

        // ---------- GPIO introspection ----------

        /// <summary>Highest port number this board exposes (STM32 bonus port Z = 25; MA35 N = 13).</summary>
        int MaxPort { get; }

        /// <summary>Linux /dev/gpiochipN label for a port ("GPIOA" ... "GPIOZ" on STM32).</summary>
        string GpioChipLabel(int port);

        /// <summary>The /dev/gpiochip number for the given pin.</summary>
        int GpioChipId(int pin);

        /// <summary>The line within <see cref="GpioChipId"/> for the given pin.</summary>
        int GpioLineId(int pin);

        /// <summary>Human name of a pin ("PA3", "PZ7") for error messages.</summary>
        string GpioName(int pin);

        // ---------- Pin muxing (used by Internal.PeripheralPins) ----------

        /// <summary>Decode a packed pin number into (port, line) in the chip's register layout.</summary>
        (int Port, int Line) PinDecode(int pin);

        /// <summary>Snapshot a pin's current register state so it can be restored on UnInitialize.</summary>
        object PinSave(int port, int line);

        /// <summary>Set a pin to a peripheral function (alternate / MFP) with the given electrical options.</summary>
        void PinSetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed);

        /// <summary>Set a pin to analog mode (STM32 only; no-op on MA35 where ADC pins go through PinSetFunction).</summary>
        void PinSetAnalog(int port, int line);

        /// <summary>Put a pin back to the state captured by PinSave.</summary>
        void PinRestore(int port, int line, object state);

        // ---------- Peripheral dispatch (called by Core's *Controller classes) ----------

        void I2cInitialize(int bus, int frequency);
        void I2cUnInitialize(int bus);
        int I2cGetFrequency(int bus);

        /// <exception cref="PlatformNotSupportedException">Thrown on boards without SPI (EPN128).</exception>
        void SpiInitialize(int bus, int bufferSize);
        void SpiUnInitialize(int bus);

        /// <exception cref="PlatformNotSupportedException">Thrown on boards without PWM (EPN128).</exception>
        void PwmInitialize(int pin);
        void PwmUnInitialize(int pin);
        int PwmGetChipId(int pin);
        int PwmGetChannelId(int pin);

        void CanInitialize(string port, int bitrate);
        void CanUnInitialize(string port);

        void SerialPortInitialize(string portName, bool enableHardwareFlowControl);
        void SerialPortUnInitialize(string portName);

        // ---------- Peripheral info tables (consumed by Devices.* packages) ----------

        /// <summary>Flat list of this board's ADC input pins (empty if none bonded).</summary>
        AnalogInput[] AdcInputs { get; }

        /// <summary>Find the ADC input matching <paramref name="pin"/>, or null.</summary>
        AnalogInput? AdcFind(int pin);

        /// <summary>The platform-device MMIO address identifying the watchdog kernel node. 0 if the board has no exposed watchdog.</summary>
        ulong WatchdogAddress { get; }

        /// <summary>Stable identifier to pass to <c>WatchdogController.FromName</c>.</summary>
        string WatchdogId { get; }
    }

    /// <summary>
    /// The one place Core looks up the current board. Each <c>Pins.*</c>
    /// package calls <see cref="Register"/> from its module initializer; user
    /// code never touches this type.
    /// </summary>
    internal static class BoardSupport {
        private static IBoardSupport? current;

        /// <summary>The registered board. Throws if no <c>Pins.*</c> package has been added to the project.</summary>
        internal static IBoardSupport Current =>
            current ?? throw new InvalidOperationException(
                "No GHIElectronics.Endpoint.Pins package is referenced. " +
                "Add it to your project.");

        /// <summary>True if a board has registered.</summary>
        internal static bool IsRegistered => current != null;

        /// <summary>
        /// Called from the board's <c>[ModuleInitializer]</c>. Same-name
        /// registrations replace the current one (so a specific Pins.*
        /// package can supersede the transitional legacy adapter during the
        /// refactor); different-name registrations throw.
        /// </summary>
        internal static void Register(IBoardSupport board) {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (current == null || current.Name == board.Name) {
                current = board;
                return;
            }
            throw new InvalidOperationException(
                $"Board '{current.Name}' is already registered; cannot also register '{board.Name}'. " +
                "A project must reference only one Pins.* board package.");
        }
    }
}
