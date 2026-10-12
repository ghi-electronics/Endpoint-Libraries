using System.Globalization;
using System.Text;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;
using Microsoft.Win32.SafeHandles;

namespace GHIElectronics.Endpoint.Devices.Dac {
    /// <summary>
    /// Endpoint's DAC (analog outputs), with the same API as TinyCLR:
    /// <code>
    /// var dac = DacController.FromName(EPM815.Dac.Id);
    /// using var output = dac.OpenChannel(EPM815.Dac.PA5);
    /// output.WriteValue(0.5);    // half of 3.3 V
    /// output.WriteValue(4095);   // 3.3 V
    /// </code>
    /// Opening a channel makes its pin an analog output; closing it switches
    /// the output off and puts the pin back as it was.
    /// </summary>
    public class DacController : IDisposable {
        private DacController() {
        }

        /// <summary>The board's DAC</summary>
        public static DacController GetDefault() => new();

        /// <summary>The DAC named <paramref name="name"/>: EPM815.Dac.Id</summary>
        public static DacController FromName(string name) =>
            name == EPM815.Dac.Id
                ? new DacController()
                : throw new ArgumentException($"There is no DAC named \"{name}\": use EPM815.Dac.Id", nameof(name));

        /// <summary>The number of channels in the chip (channel numbers start at 0)</summary>
        public int ChannelCount => EPM815.Dac.ChannelCount;

        /// <summary>Sample width in bits</summary>
        public int ResolutionInBits => 12;

        /// <summary>The value for 0 V</summary>
        public int MinValue => 0;

        /// <summary>The value for the reference voltage (3.3 V)</summary>
        public int MaxValue => 4095;

        /// <summary>Opens a channel for output: EPM815.Dac.PA5</summary>
        public DacChannel OpenChannel(int channelNumber) => new(this, channelNumber);

        /// <summary>Nothing to release: each channel is released when it is disposed</summary>
        public void Dispose() {
        }
    }

    /// <summary>
    /// A DAC channel opened with <see cref="DacController.OpenChannel(int)"/>.
    /// <see cref="WriteValue(int)"/> takes a raw value, <see cref="WriteValue(double)"/>
    /// a 0.0 to 1.0 part of the range.
    /// </summary>
    public class DacChannel : IDisposable {
        private static readonly object Gate = new();
        private readonly EPM815.Dac.Output output;
        private readonly SafeFileHandle value;
        private readonly string powerDown;
        private bool disposed;

        /// <summary>The channel number, as given to OpenChannel</summary>
        public int ChannelNumber { get; }

        /// <summary>The controller that opened the channel</summary>
        public DacController Controller { get; }

        /// <summary>The last raw value written</summary>
        public int LastWrittenValue { get; private set; }

        internal DacChannel(DacController controller, int channelNumber) {
            this.output = EPM815.Dac.Find(channelNumber) ?? throw new ArgumentOutOfRangeException(nameof(channelNumber), channelNumber,
                channelNumber is >= 0 and < EPM815.Dac.ChannelCount
                    ? $"DAC channel {channelNumber} is not enabled in this firmware"
                    : "Use EPM815.Dac.PA5");
            this.ChannelNumber = channelNumber;
            this.Controller = controller;

            var owner = this.Owner;
            if (!PeripheralPins.Connect(owner, PinFunction.Analog(this.output.Pin))) {
                throw new InvalidOperationException($"DAC channel {channelNumber} is already open");
            }
            try {
                var folder = LoadDriver(this.output.Device);
                var channel = this.output.LinuxChannel;
                this.powerDown = $"{folder}/out_voltage{channel}_powerdown";
                this.value = File.OpenHandle($"{folder}/out_voltage{channel}_raw", FileMode.Open, FileAccess.Write);
                this.Write(this.value, 0);
                File.WriteAllText(this.powerDown, "0");  // output on, at 0 V
            }
            catch {
                this.value?.Dispose();
                PeripheralPins.Disconnect(owner);
                throw;
            }
        }

        private string Owner => $"DAC channel {this.ChannelNumber} ({EPM815.Gpio.Name(this.output.Pin)})";

        /// <summary>Sets the output to a raw value from MinValue (0 V) to MaxValue (3.3 V)</summary>
        public void WriteValue(int value) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (value < this.Controller.MinValue || value > this.Controller.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"The value must be {this.Controller.MinValue} to {this.Controller.MaxValue}");
            }
            this.Write(this.value, value);
            this.LastWrittenValue = value;
        }

        /// <summary>Sets the output to a part of the range: 0.0 is 0 V, 1.0 is 3.3 V</summary>
        public void WriteValue(double ratio) =>
            this.WriteValue((int)(ratio * (this.Controller.MaxValue - this.Controller.MinValue) + this.Controller.MinValue));

        /// <summary>Switches the output off and puts the pin back as it was</summary>
        public void Dispose() {
            if (this.disposed) {
                return;
            }
            this.disposed = true;
            try {
                File.WriteAllText(this.powerDown, "1");  // output off (high impedance)
            }
            finally {
                this.value.Dispose();
                PeripheralPins.Disconnect(this.Owner);
            }
            GC.SuppressFinalize(this);
        }

        private void Write(SafeFileHandle file, int number) =>
            RandomAccess.Write(file, Encoding.ASCII.GetBytes(number.ToString(CultureInfo.InvariantCulture)), 0);

        // The DAC drivers are modules: they load the first time a program uses the DAC
        private static string LoadDriver(string device) {
            lock (Gate) {
                var folder = LinuxDevices.FindIio(device);
                if (folder is null) {
                    KernelModules.Load("stm32-dac-core");
                    KernelModules.Load("stm32-dac");
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    while ((folder = LinuxDevices.FindIio(device)) is null) {
                        if (watch.ElapsedMilliseconds > 1000) {
                            throw new InvalidOperationException("The DAC did not start: update the board's firmware");
                        }
                        Thread.Sleep(5);
                    }
                }
                return folder;
            }
        }
    }
}
