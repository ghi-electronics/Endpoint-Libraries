using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins {
    /// <summary>
    /// Wires the EPN256 board into Core. Core's chip-neutral controllers
    /// dispatch through <see cref="BoardSupport.Current"/>; this class is
    /// what they call. Registered at assembly load via <see cref="ModuleInit"/>.
    /// </summary>
    internal sealed class EPN256BoardSupport : IBoardSupport {
        public string Name => "EPN256";

        public int MaxPort => (int)EPN256.Gpio.Port.N;

        public string GpioChipLabel(int port) => $"gpio-{(char)('a' + port)}";

        public int GpioChipId(int pin) => EPN256.Gpio.GetChipId(pin);
        public int GpioLineId(int pin) => EPN256.Gpio.GetLineId(pin);
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

        public void I2cInitialize(int bus, int frequency) => EPN256.I2c.Initialize(bus, frequency);
        public void I2cUnInitialize(int bus) => EPN256.I2c.UnInitialize(bus);
        public int I2cGetFrequency(int bus) => EPN256.I2c.GetFrequency(bus);

        public void SpiInitialize(int bus, int bufferSize) => EPN256.Spi.Initialize(bus);
        public void SpiUnInitialize(int bus) => EPN256.Spi.UnInitialize(bus);

        public void PwmInitialize(int pin) => EPN256.Pwm.Initialize(pin);
        public void PwmUnInitialize(int pin) => EPN256.Pwm.UnInitialize(pin);
        public int PwmGetChipId(int pin) => EPN256.Pwm.GetChipId(pin);
        public int PwmGetChannelId(int pin) => EPN256.Pwm.GetChannelId(pin);

        public void CanInitialize(string port, int bitrate) => EPN256.Can.Initialize(port, bitrate);
        public void CanUnInitialize(string port) => EPN256.Can.UnInitialize(port);

        public void SerialPortInitialize(string portName, bool enableHardwareFlowControl) =>
            EPN256.SerialPort.Initialize(portName, enableHardwareFlowControl);
        public void SerialPortUnInitialize(string portName) =>
            EPN256.SerialPort.UnInitialize(portName);

        public AnalogInput[] AdcInputs => EPN256.Adc.Inputs;
        public AnalogInput? AdcFind(int pin) => EPN256.Adc.Find(pin);

        public ulong WatchdogAddress => EPN256.Watchdog.Address;
        public string WatchdogId => EPN256.Watchdog.Id;
    }
}
