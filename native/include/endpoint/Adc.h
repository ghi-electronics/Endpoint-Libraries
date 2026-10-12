// The analog inputs of Endpoint, with the same API as .NET's
// System.Device.Analog and Endpoint's AdcController:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Adc;
//
//   AdcController adc;
//   auto pin = adc.OpenPin(EPM815::Adc::Pin::PA0);
//   double volts = pin.ReadVoltage().Volts();
//
// Opening a pin makes it an analog input; closing it puts the pin back as it was.
#pragma once

#include <cstdint>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Adc {

// A voltage, as .NET's UnitsNet.ElectricPotential
class ElectricPotential {
public:
    static ElectricPotential FromVolts(double volts) { return ElectricPotential(volts); }
    static ElectricPotential FromMillivolts(double millivolts) { return ElectricPotential(millivolts / 1000); }

    double Volts() const { return volts_; }
    double Millivolts() const { return volts_ * 1000; }

    bool operator==(const ElectricPotential&) const = default;

private:
    explicit ElectricPotential(double volts) : volts_(volts) {}
    double volts_;
};

class AdcController;

// An open analog input: the pin is in analog mode until it is disposed
class AnalogInputPin {
public:
    AnalogInputPin(int pinNumber, ElectricPotential voltageReference)
        : pinNumber_(pinNumber), voltageReference_(voltageReference) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_adc_open_pin(pinNumber, &handle));
        handle_ = Handle(handle);
    }

    int PinNumber() const { return pinNumber_; }

    int AdcResolutionBits() const {
        int bits = 0;
        Core::detail::Check(endpoint_adc_resolution_bits(h(), &bits));
        return bits;
    }

    ElectricPotential VoltageReference() const { return voltageReference_; }
    ElectricPotential MinVoltage() const { return ElectricPotential::FromVolts(0); }
    ElectricPotential MaxVoltage() const { return voltageReference_; }

    // A new sample, 0 to 2^AdcResolutionBits - 1
    uint32_t ReadRaw() {
        uint32_t value = 0;
        Core::detail::Check(endpoint_adc_read_raw(h(), &value));
        return value;
    }

    // A new sample (the driver's exact scale)
    ElectricPotential ReadVoltage() {
        double millivolts = 0;
        Core::detail::Check(endpoint_adc_read_millivolts(h(), &millivolts));
        return ElectricPotential::FromMillivolts(millivolts);
    }

    // Not available: Endpoint's converters signal no value changes
    void EnableAnalogValueChangedEvent() {
        throw Core::NotSupportedException("Endpoint's analog inputs have no value-changed event: call ReadRaw or ReadVoltage");
    }
    void DisableAnalogValueChangedEvent() {}

    // Puts the pin back as it was before it was opened
    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_adc_close_pin>;

    void* h() const { return handle_.Get("AnalogInputPin"); }

    int pinNumber_;
    ElectricPotential voltageReference_;
    Handle handle_;
};

// All of Endpoint's analog inputs (both converters)
class AdcController {
public:
    AdcController() : voltageReference_(Reference()) {}

    int PinCount() const {
        int count = 0;
        Core::detail::Check(endpoint_adc_pin_count(&count));
        return count;
    }

    // The inputs measure 0 V to this (3.3 V)
    ElectricPotential VoltageReference() const { return voltageReference_; }

    bool SupportsAnalogInput(int pin) const {
        int supported = 0;
        Core::detail::Check(endpoint_adc_supports_analog_input(pin, &supported));
        return supported != 0;
    }

    // The pin's channel in its converter (ADC1 or ADC2)
    int ConvertPinNumberToAnalogChannelNumber(int pinNumber) const {
        int channel = 0;
        Core::detail::Check(endpoint_adc_convert_pin_to_channel(pinNumber, &channel));
        return channel;
    }

    AnalogInputPin OpenPin(int pinNumber) { return AnalogInputPin(pinNumber, voltageReference_); }

    void ClosePin(AnalogInputPin& pin) { pin.Dispose(); }

    void Dispose() {}

private:
    static ElectricPotential Reference() {
        double millivolts = 0;
        Core::detail::Check(endpoint_adc_voltage_reference(&millivolts));
        return ElectricPotential::FromMillivolts(millivolts);
    }

    ElectricPotential voltageReference_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Adc
