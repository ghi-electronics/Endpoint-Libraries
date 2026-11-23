#nullable enable
using System.Reflection;
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
    /// The one place Core looks up the current board. Each <c>Pins.*</c> or
    /// <c>Drivers.*</c> package calls <see cref="Register"/> from its module
    /// initializer; user code never touches this type.
    /// </summary>
    internal static class BoardSupport {
        private static IBoardSupport current = new UnknownBoardSupport();
        private static bool registered;
        private static bool autoLoadDone;
        private static readonly object AutoLoadGate = new object();

        // The assemblies whose [ModuleInitializer] calls Register.
        private const string PinsAssembly = "GHIElectronics.Endpoint.Pins";
        private const string RaspberryPiAssembly = "GHIElectronics.Endpoint.Drivers.RaspberryPi";

        /// <summary>
        /// The registered board. Starts as <see cref="UnknownBoardSupport"/>
        /// so any peripheral call before registration throws a clear "add the
        /// right package" message instead of a null-reference crash.
        /// </summary>
        internal static IBoardSupport Current {
            get {
                if (!registered) EnsureBoardLoaded();
                return current;
            }
        }

        /// <summary>True if a board package has registered its implementation.</summary>
        internal static bool IsRegistered {
            get {
                if (!registered) EnsureBoardLoaded();
                return registered;
            }
        }

        // A board package registers itself from a [ModuleInitializer], which
        // the CLR runs only once something loads that assembly. Pin constants
        // are `const`, so the compiler bakes their values into the call site
        // and the IL never names the package: an app whose first line is
        // I2cController.Initialize(EPDomino24.I2c.I2c1) leaves the package on
        // disk, unloaded, and every peripheral call throws "not supported on
        // this board". Loading by name here is what makes that first line work.
        private static void EnsureBoardLoaded() {
            lock (AutoLoadGate) {
                if (autoLoadDone) return;
                autoLoadDone = true;
                // ChipDetect has already read the device tree, so probe the
                // package that can actually register first. Getting the order
                // wrong costs a thrown-and-caught FileNotFoundException, which
                // is the one part of this that is not free.
                if (ChipDetect.Current == Chip.Unknown) {
                    TryLoad(RaspberryPiAssembly);
                    if (!registered) TryLoad(PinsAssembly);
                }
                else {
                    TryLoad(PinsAssembly);
                    if (!registered) TryLoad(RaspberryPiAssembly);
                }
            }
        }

        // Loading the assembly runs its module initializer, which calls
        // Register on this class before Load returns.
        private static void TryLoad(string assemblyName) {
            try { Assembly.Load(assemblyName); }
            catch { /* package not referenced by this project */ }
        }

        /// <summary>
        /// Called from the board's <c>[ModuleInitializer]</c>. Replacing the
        /// default <see cref="UnknownBoardSupport"/> is always allowed;
        /// replacing with the same-named board is a no-op; replacing with a
        /// different real board throws (project must reference at most one
        /// board package).
        /// </summary>
        internal static void Register(IBoardSupport board) {
            if (board == null) throw new ArgumentNullException(nameof(board));
            // Tracked by a flag, not by `current is UnknownBoardSupport`:
            // Zero2WBoardSupport derives from UnknownBoardSupport, so the type
            // test would report "nothing registered" on a Pi and let a second
            // board silently overwrite the first.
            if (!registered || current.Name == board.Name) {
                current = board;
                registered = true;
                return;
            }
            throw new InvalidOperationException(
                $"Board '{current.Name}' is already registered; cannot also register '{board.Name}'. " +
                "A project must reference only one board package (Pins.* for Endpoint, Drivers.RaspberryPi for Pi).");
        }
    }
}
