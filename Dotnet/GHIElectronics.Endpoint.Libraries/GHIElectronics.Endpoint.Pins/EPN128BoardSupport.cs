using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins {
    /// <summary>
    /// Wires the EPN128 (Chili Pro) board into Core. EPN128 does not bond
    /// out SPI or EPWM pins, so those paths throw
    /// <see cref="PlatformNotSupportedException"/>. Registered at assembly
    /// load via <see cref="ModuleInit"/>.
    /// </summary>
    internal sealed class EPN128BoardSupport : IBoardSupport {
        public string Name => "EPN128";

        public int MaxPort => (int)EPN128.Gpio.Port.N;

        public string GpioChipLabel(int port) => $"gpio-{(char)('a' + port)}";

        public int GpioChipId(int pin) => EPN128.Gpio.GetChipId(pin);
        public int GpioLineId(int pin) => EPN128.Gpio.GetLineId(pin);
        public string GpioName(int pin) =>
            pin >= 0 ? $"P{(char)('A' + pin / 16)}{pin % 16}" : $"#{pin}";

        public (int Port, int Line) PinDecode(int pin) => Ma35dPins.FromPin(pin);
        public object PinSave(int port, int line) => Ma35dPins.Save(port, line);

        public void PinSetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed) =>
            Ma35dPins.SetFunction(port, line, function, pull, type, speed);

        public void PinSetAnalog(int port, int line) {
            // MA35 has no analog mode; ADC pins select via MFP like any other
            // peripheral function, through PinSetFunction instead.
        }

        public void PinRestore(int port, int line, object state) =>
            Ma35dPins.Restore(port, line, (Ma35dPins.State)state);

        public void I2cInitialize(int bus, int frequency) => EPN128.I2c.Initialize(bus, frequency);
        public void I2cUnInitialize(int bus) => EPN128.I2c.UnInitialize(bus);
        public int I2cGetFrequency(int bus) => EPN128.I2c.GetFrequency(bus);

        public void SpiInitialize(int bus, int bufferSize) =>
            throw new PlatformNotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out SPI pins.");
        public void SpiUnInitialize(int bus) { /* no-op: never initialized */ }

        public void PwmInitialize(int pin) =>
            throw new PlatformNotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
        public void PwmUnInitialize(int pin) { /* no-op: never initialized */ }
        public int PwmGetChipId(int pin) =>
            throw new PlatformNotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
        public int PwmGetChannelId(int pin) =>
            throw new PlatformNotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");

        public void CanInitialize(string port, int bitrate) => EPN128.Can.Initialize(port, bitrate);
        public void CanUnInitialize(string port) => EPN128.Can.UnInitialize(port);

        public void SerialPortInitialize(string portName, bool enableHardwareFlowControl) =>
            EPN128.SerialPort.Initialize(portName, enableHardwareFlowControl);
        public void SerialPortUnInitialize(string portName) =>
            EPN128.SerialPort.UnInitialize(portName);

        public AnalogInput[] AdcInputs => EPN128.Adc.Inputs;
        public AnalogInput? AdcFind(int pin) => EPN128.Adc.Find(pin);

        public ulong WatchdogAddress => EPN128.Watchdog.Address;
        public string WatchdogId => EPN128.Watchdog.Id;
    }
}
