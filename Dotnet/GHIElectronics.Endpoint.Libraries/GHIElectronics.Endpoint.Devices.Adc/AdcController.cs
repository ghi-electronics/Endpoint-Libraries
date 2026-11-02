using System.Device.Analog;
using GHIElectronics.Endpoint.Core;
using UnitsNet;

namespace GHIElectronics.Endpoint.Devices.Adc {
    /// <summary>
    /// Endpoint's analog inputs, for .NET's <c>System.Device.Analog</c>. The pins
    /// are the per-board <c>Adc.Pin</c> constants (for example <c>EPM815.Adc.Pin</c>).
    /// Opening a pin makes it an analog input; closing it puts the pin back as it
    /// was. The inputs measure 0 V to <see cref="AnalogController.VoltageReference"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var adc = new AdcController();
    /// using var input = adc.OpenPin(EPM815.Adc.Pin.PA0);
    /// Console.WriteLine($"{input.ReadVoltage().Volts:0.000} V (raw {input.ReadRaw()})");
    /// </code>
    /// </example>
    public sealed class AdcController : AnalogController {
        /// <summary>A controller for all of Endpoint's analog inputs.</summary>
        public AdcController() =>
            this.VoltageReference = ElectricPotential.FromMillivolts(Converter.Get(1).ReferenceMillivolts);

        /// <summary>The number of analog inputs available.</summary>
        public override int PinCount => EPM815.Adc.Inputs.Length;

        /// <summary>Whether <paramref name="pin"/> is one of the board's analog input constants.</summary>
        public override bool SupportsAnalogInput(int pin) => EPM815.Adc.Find(pin) is not null;

        /// <summary>The pin's channel within its converter.</summary>
        public override int ConvertPinNumberToAnalogChannelNumber(int pinNumber) => Get(pinNumber).Channel;

        /// <inheritdoc/>
        protected override AnalogInputPin OpenPinCore(int pinNumber) {
            var input = Get(pinNumber);
            return new AdcInputPin(this, input, Converter.Get(input.Converter));
        }

        private static EPM815.Adc.Input Get(int pin) =>
            EPM815.Adc.Find(pin) ?? throw new ArgumentException($"Pin {pin} has no analog input: use the EPM815.Adc.Pin constants", nameof(pin));
    }
}
