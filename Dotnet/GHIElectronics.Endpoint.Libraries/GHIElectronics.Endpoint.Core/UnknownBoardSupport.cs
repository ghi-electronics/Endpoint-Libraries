#nullable enable
using System;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Default <see cref="IBoardSupport"/> that sits in <see cref="BoardSupport.Current"/>
    /// at process start, before any <c>Pins.*</c> or <c>Drivers.*</c> package
    /// has registered its own implementation. Every method throws a
    /// <see cref="NotSupportedException"/> whose message names the package the
    /// caller is missing.
    ///
    /// Concrete board support classes (<c>EPM815BoardSupport</c>,
    /// <c>EPN256BoardSupport</c>, <c>EPN128BoardSupport</c>, <c>Zero2WBoardSupport</c>)
    /// inherit from this and override only the methods their board actually
    /// implements. Any method the board doesn't override keeps the throwing
    /// default, which is the right answer for peripherals that don't exist
    /// on that board (e.g. CAN on Pi, DAC on Nuvoton).
    ///
    /// Also serves as the instance registered via <c>Chip.Unknown</c> on an
    /// unrecognized host (dev machine, unsupported SoC) — all peripheral
    /// calls fail fast with a diagnostic message instead of silently doing
    /// the wrong thing.
    /// </summary>
    internal class UnknownBoardSupport : IBoardSupport {
        /// <summary>The chip name / board identifier. "Unknown" by default.</summary>
        public virtual string Name => "Unknown";

        // ---------- GPIO introspection — throws by default ----------

        public virtual int MaxPort => 0;
        public virtual string GpioChipLabel(int port) => NotSupported<string>("GPIO introspection");
        public virtual int GpioChipId(int pin) => NotSupported<int>("GPIO introspection");
        public virtual int GpioLineId(int pin) => NotSupported<int>("GPIO introspection");
        public virtual string GpioName(int pin) => NotSupported<string>("GPIO introspection");

        // ---------- Pin muxing — throws by default ----------

        public virtual (int Port, int Line) PinDecode(int pin) => NotSupported<(int, int)>("pin muxing");
        public virtual object PinSave(int port, int line) => NotSupported<object>("pin muxing");
        public virtual void PinSetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed) =>
            NotSupported("pin muxing");
        public virtual void PinSetAnalog(int port, int line) => NotSupported("pin muxing");
        public virtual void PinRestore(int port, int line, object state) => NotSupported("pin muxing");

        // ---------- Peripheral dispatch — throws by default ----------

        public virtual void I2cInitialize(int bus, int frequency) => NotSupported("I2C");
        public virtual void I2cUnInitialize(int bus) => NotSupported("I2C");
        public virtual int I2cGetFrequency(int bus) => NotSupported<int>("I2C");

        public virtual void SpiInitialize(int bus, int bufferSize) => NotSupported("SPI");
        public virtual void SpiUnInitialize(int bus) => NotSupported("SPI");

        public virtual void PwmInitialize(int pin) => NotSupported("PWM");
        public virtual void PwmUnInitialize(int pin) => NotSupported("PWM");
        public virtual int PwmGetChipId(int pin) => NotSupported<int>("PWM");
        public virtual int PwmGetChannelId(int pin) => NotSupported<int>("PWM");

        public virtual void CanInitialize(string port, int bitrate) => NotSupported("CAN");
        public virtual void CanUnInitialize(string port) => NotSupported("CAN");

        public virtual void SerialPortInitialize(string portName, bool enableHardwareFlowControl) =>
            NotSupported("serial port");
        public virtual void SerialPortUnInitialize(string portName) => NotSupported("serial port");

        // ---------- Peripheral info tables — safe empty defaults ----------

        public virtual AnalogInput[] AdcInputs => Array.Empty<AnalogInput>();
        public virtual AnalogInput? AdcFind(int pin) => null;

        public virtual ulong WatchdogAddress => 0;
        public virtual string WatchdogId => string.Empty;

        // ---------- helpers ----------

        private T NotSupported<T>(string what) => throw Throw(what);
        private void NotSupported(string what) => throw Throw(what);

        private NotSupportedException Throw(string what) => new NotSupportedException(
            $"{what} is not supported on this board ('{Name}'). " +
            "If this is an Endpoint board (EPM815 / EPDomino24 / EPN256 / EPN128), add " +
            "GHIElectronics.Endpoint.Pins to your project. " +
            "If this is a Raspberry Pi Zero 2 W, add " +
            "GHIElectronics.Endpoint.Drivers.RaspberryPi.");
    }
}
