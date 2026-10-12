using System.Buffers.Text;
using System.Device.Analog;
using System.Device.Gpio;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;
using Microsoft.Win32.SafeHandles;
using UnitsNet;

namespace GHIElectronics.Endpoint.Devices.Adc {
    /// <summary>An open analog input: the pin is in analog mode until it is disposed</summary>
    internal sealed class AdcInputPin : AnalogInputPin {
        private readonly EPM815.Adc.Input input;
        private readonly Converter converter;
        private readonly SafeFileHandle file;
        private readonly byte[] buffer = new byte[16];
        private bool disposed;

        public AdcInputPin(AdcController controller, EPM815.Adc.Input input, Converter converter)
            : base(controller, input.Pin, controller.VoltageReference) {
            this.input = input;
            this.converter = converter;

            var path = $"{converter.Folder}/in_voltage{input.Channel}_raw";
            if (!File.Exists(path)) {
                throw new InvalidOperationException($"The analog input on {input.Name} (ADC{input.Converter} channel {input.Channel}) " +
                                                    "is not enabled in this firmware: update the board's firmware");
            }

            if (input.IsGpio && !PeripheralPins.Connect(this.Owner, PinFunction.Analog(input.Pin))) {
                throw new InvalidOperationException($"The analog input on {input.Name} is already open");
            }
            try {
                this.file = File.OpenHandle(path);
            }
            catch {
                if (input.IsGpio) {
                    PeripheralPins.Disconnect(this.Owner);
                }
                throw;
            }
        }

        private string Owner => "ADC " + this.input.Name;

        /// <inheritdoc/>
        public override int AdcResolutionBits => this.converter.Bits;

        /// <inheritdoc/>
        public override ElectricPotential MinVoltage => ElectricPotential.Zero;

        /// <inheritdoc/>
        public override ElectricPotential MaxVoltage => this.VoltageReference;

        /// <summary>A new sample, 0 to 2^<see cref="AdcResolutionBits"/> - 1</summary>
        public override uint ReadRaw() {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            lock (this.buffer) {
                // Every read from the start makes Linux take a new sample
                var length = RandomAccess.Read(this.file, this.buffer, 0);
                if (!Utf8Parser.TryParse(this.buffer.AsSpan(0, length), out uint value, out _)) {
                    throw new IOException($"Unexpected reading from the analog input on {this.input.Name}");
                }
                return value;
            }
        }

        /// <summary>The driver's exact scale, not an assumed 3.3 V</summary>
        protected override ElectricPotential ConvertToVoltage(uint rawValue) =>
            ElectricPotential.FromMillivolts((rawValue + this.converter.Offset) * this.converter.ScaleMillivolts);

        /// <summary>Not available: Endpoint's converters signal no value changes</summary>
        public override void EnableAnalogValueChangedEvent(GpioController masterController, int masterPin) =>
            throw new NotSupportedException("Endpoint's analog inputs have no value-changed event: call ReadRaw or ReadVoltage");

        /// <inheritdoc/>
        public override void DisableAnalogValueChangedEvent() {
        }

        /// <summary>Puts the pin back as it was before it was opened</summary>
        protected override void Dispose(bool disposing) {
            if (!this.disposed) {
                this.disposed = true;
                if (disposing) {
                    this.file?.Dispose();
                    if (this.input.IsGpio) {
                        PeripheralPins.Disconnect(this.Owner);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
