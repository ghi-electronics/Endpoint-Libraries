using System.Device.Analog;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;
using UnitsNet;

namespace GHIElectronics.Endpoint.Devices.Adc {
    /// <summary>
    /// Endpoint's analog inputs, for .NET's <c>System.Device.Analog</c>. The pin
    /// argument is a per-board <c>Adc.Pin</c> constant (for example
    /// <c>EPM815.Adc.Pin.PA0</c> or <c>EPN256.Adc.Pin.PB0</c>). Opening a pin
    /// makes it an analog input; closing it returns the pin as it was. The
    /// inputs measure 0 V to <see cref="AnalogController.VoltageReference"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var adc = new AdcController();
    /// using var input = adc.OpenPin(EPN256.Adc.Pin.PB0);
    /// Console.WriteLine($"{input.ReadVoltage().Volts:0.000} V (raw {input.ReadRaw()})");
    /// </code>
    /// </example>
    public sealed class AdcController : AnalogController {
        /// <summary>A controller for all of Endpoint's analog inputs.</summary>
        public AdcController() {
            // Voltage reference comes from the chip's primary converter: ADC1
            // on EPM815, EADC0 on EPN256 / EPN128. Either chip reports 3.3 V
            // in practice; we read it from the IIO driver so there is no
            // hidden assumption.
            var firstName = Inputs[0].IioName;
            this.VoltageReference = ElectricPotential.FromMillivolts(Converter.Get(firstName).ReferenceMillivolts);
        }

        /// <summary>The number of analog inputs available.</summary>
        public override int PinCount => Inputs.Length;

        /// <summary>Whether <paramref name="pin"/> is one of the board's analog input constants.</summary>
        public override bool SupportsAnalogInput(int pin) => Find(pin) is not null;

        /// <summary>The pin's channel within its converter.</summary>
        public override int ConvertPinNumberToAnalogChannelNumber(int pinNumber) => Get(pinNumber).Channel;

        /// <inheritdoc/>
        protected override AnalogInputPin OpenPinCore(int pinNumber) {
            var input = Get(pinNumber);
            return new AdcInputPin(this, input, Converter.Get(input.IioName));
        }

        // Chip-dispatched access to the pin table. The whole rest of this
        // class only touches these two members, so a new chip adds one case.
        private static AnalogInput[] Inputs => ChipDetect.Current switch {
            Chip.Epn128 => EPN128.Adc.Inputs,
            Chip.Epn256 => EPN256.Adc.Inputs,
            _           => EPM815.Adc.Inputs,
        };

        private static AnalogInput? Find(int pin) => ChipDetect.Current switch {
            Chip.Epn128 => EPN128.Adc.Find(pin),
            Chip.Epn256 => EPN256.Adc.Find(pin),
            _           => EPM815.Adc.Find(pin),
        };

        private static AnalogInput Get(int pin) =>
            Find(pin) ?? throw new ArgumentException(
                $"Pin {pin} has no analog input: use the chip's Adc.Pin constants", nameof(pin));
    }
}
