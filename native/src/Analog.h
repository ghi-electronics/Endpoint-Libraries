#pragma once

#include <cstdint>
#include <mutex>
#include <string>

namespace GHIElectronics::Endpoint::Internal {

// The EPM815's analog inputs, the same as the .NET library's EPM815.Adc.Pin:
// GPIO pin numbers, plus ANA0 and ANA1 (dedicated analog inputs, no GPIO)
namespace AdcPins {
constexpr int ANA0 = 0x1000;
constexpr int ANA1 = 0x1001;
}  // namespace AdcPins

// One of the chip's two converters (ADC1, ADC2) as the system's ADC driver
// shows it. The driver calibrates the converter and takes the samples; its
// scale and resolution give the exact voltage.
struct Converter {
    std::string folder;
    double scaleMillivolts = 0;  // per step
    int offset = 0;
    int bits = 16;

    double ReferenceMillivolts() const { return scaleMillivolts * (1LL << bits); }

    static const Converter& Get(int number);
};

// As .NET's System.Device.Analog.AnalogInputPin from the Endpoint's
// AdcController: the pin is in analog mode until it is closed
class AnalogInputPinImpl {
public:
    explicit AnalogInputPinImpl(int pin);
    ~AnalogInputPinImpl();

    AnalogInputPinImpl(const AnalogInputPinImpl&) = delete;
    AnalogInputPinImpl& operator=(const AnalogInputPinImpl&) = delete;

    int AdcResolutionBits() const { return converter_.bits; }
    uint32_t ReadRaw();
    double ReadMillivolts();

private:
    std::string name_;
    bool gpio_;
    const Converter& converter_;
    int fd_ = -1;
    std::mutex gate_;
};

// The AdcController's PinCount, SupportsAnalogInput and VoltageReference
namespace AdcController {
int PinCount();
bool SupportsAnalogInput(int pin);
int ConvertPinNumberToAnalogChannelNumber(int pin);
double VoltageReferenceMillivolts();
}  // namespace AdcController

// As TinyCLR's DacChannel (Endpoint's GHIElectronics.Endpoint.Devices.Dac)
class DacChannelImpl {
public:
    static constexpr int ChannelCount = 2, ResolutionInBits = 12, MinValue = 0, MaxValue = 4095;

    explicit DacChannelImpl(int channel);
    ~DacChannelImpl();

    DacChannelImpl(const DacChannelImpl&) = delete;
    DacChannelImpl& operator=(const DacChannelImpl&) = delete;

    void WriteValue(int value);
    int LastWrittenValue() const { return last_; }

private:
    int channel_;
    int pin_;
    std::string owner_;
    std::string powerDown_;
    int fd_ = -1;
    int last_ = 0;
};

}  // namespace GHIElectronics::Endpoint::Internal
