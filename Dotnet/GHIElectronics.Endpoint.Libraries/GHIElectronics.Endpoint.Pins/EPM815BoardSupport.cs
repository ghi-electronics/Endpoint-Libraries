using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins {
    /// <summary>
    /// Wires the EPM815 module into Core. Core's chip-neutral controllers
    /// (<see cref="I2cController"/>, <see cref="SpiController"/>,
    /// <see cref="PwmController"/>, <see cref="CanController"/>,
    /// <see cref="SerialPortController"/>) dispatch through
    /// <see cref="BoardSupport.Current"/>; this class is what they call.
    /// Registered at assembly load via <see cref="ModuleInit"/>.
    /// </summary>
    internal sealed class EPM815BoardSupport : IBoardSupport {
        public string Name => "EPM815";

        public int MaxPort => (int)EPM815.Gpio.Port.Z;

        public string GpioChipLabel(int port) =>
            "GPIO" + (port == Stm32Mp1Pins.PortZ ? 'Z' : (char)('A' + port));

        public int GpioChipId(int pin) => EPM815.Gpio.GetChipId(pin);
        public int GpioLineId(int pin) => EPM815.Gpio.GetLineId(pin);
        public string GpioName(int pin) => EPM815.Gpio.Name(pin);

        public (int Port, int Line) PinDecode(int pin) => Stm32Mp1Pins.FromPin(pin);
        public object PinSave(int port, int line) => Stm32Mp1Pins.Save(port, line);

        public void PinSetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed) =>
            Stm32Mp1Pins.SetFunction(port, line, function, pull, type, speed);

        public void PinSetAnalog(int port, int line) {
            Stm32Mp1Pins.SetPull(port, line, Pull.None);
            Stm32Mp1Pins.SetMode(port, line, Stm32Mp1Pins.Mode.Analog);
        }

        public void PinRestore(int port, int line, object state) =>
            Stm32Mp1Pins.Restore(port, line, (Stm32Mp1Pins.State)state);

        public void I2cInitialize(int bus, int frequency) => EPM815.I2c.Initialize(bus, frequency);
        public void I2cUnInitialize(int bus) => EPM815.I2c.UnInitialize(bus);
        public int I2cGetFrequency(int bus) => EPM815.I2c.GetFrequency(bus);

        public void SpiInitialize(int bus, int bufferSize) => EPM815.Spi.Initialize(bus, bufferSize);
        public void SpiUnInitialize(int bus) => EPM815.Spi.UnInitialize(bus);

        public void PwmInitialize(int pin) => EPM815.Pwm.Initialize(pin);
        public void PwmUnInitialize(int pin) => EPM815.Pwm.UnInitialize(pin);
        public int PwmGetChipId(int pin) => EPM815.Pwm.GetChipId(pin);
        public int PwmGetChannelId(int pin) => EPM815.Pwm.GetChannelId(pin);

        public void CanInitialize(string port, int bitrate) => EPM815.Can.Initialize(port, bitrate);
        public void CanUnInitialize(string port) => EPM815.Can.UnInitialize(port);

        public void SerialPortInitialize(string portName, bool enableHardwareFlowControl) =>
            EPM815.SerialPort.Initialize(portName, enableHardwareFlowControl);
        public void SerialPortUnInitialize(string portName) =>
            EPM815.SerialPort.UnInitialize(portName);

        public AnalogInput[] AdcInputs => EPM815.Adc.Inputs;
        public AnalogInput? AdcFind(int pin) => EPM815.Adc.Find(pin);

        public ulong WatchdogAddress => EPM815.Watchdog.Address;
        public string WatchdogId => EPM815.Watchdog.Id;
    }
}
