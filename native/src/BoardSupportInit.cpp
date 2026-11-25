// BoardSupport implementations for the Endpoint-board chips (EPM815 /
// EPDomino24, EPN256, EPN128) and a static initializer that picks the
// right one at libendpoint load based on ChipDetect::Current().
//
// On an unknown chip (Pi Zero 2 W or an unrecognized host), none of these
// register and BoardSupport::Current() stays as UnknownBoardSupport.
// Non-Endpoint boards (Pi via endpoint-drivers/cpp/RaspberryPi/Zero2W.h)
// run their own static initializer in user code to install their
// BoardSupport.

#include <cstddef>
#include <iterator>

#include "endpoint/BoardSupport.h"
#include "endpoint/Core.h"
#include "endpoint/EPM815.h"
#include "endpoint/EPN256.h"
#include "endpoint/EPN128.h"
#include "GpioChips.h"

namespace GHIElectronics::Endpoint::Core {

namespace {

// All Endpoint-board chips (STM32MP1 for EPM815 / EPDomino24, MA35D1 for
// EPN256, MA35D0 for EPN128) share the same GPIO layout convention: 16 lines
// per /dev/gpiochip, chips labelled gpioa / gpiob / ... in the kernel DTS.
// So the pin-to-(chip, line) math is identical across all three; factor it
// into a mixin that each board inherits.
struct Stm32StyleGpio : UnknownBoardSupport {
    int GpioChipId(int pin) override { return Internal::GpioChips::Find(pin / 16); }
    int GpioLineId(int pin) override { return pin % 16; }
};

// The analog-input tables. These live here rather than in the public per-chip
// headers because the mux value and the IIO device name are internal detail
// that only Analog.cpp consumes; the headers expose just the pin constants.
// Mirrors .NET's EPM815.Adc.Inputs / EPN256.Adc.Inputs / EPN128.Adc.Inputs.
template <std::size_t N>
const AnalogInput* FindIn(const AnalogInput (&table)[N], int pin) {
    for (const auto& i : table) {
        if (i.pin == pin) return &i;
    }
    return nullptr;
}

// STM32MP1 ADC1 and ADC2 are two sub-devices of one peripheral, so they get
// separate IIO names. `alternate` is -1 for every row: STM32 pins reach the
// ADC through the pad's analog mode, not through an alternate function.
// ANA0 / ANA1 are dedicated analog pads with no GPIO, hence hasOwnPin false.
constexpr const char* Epm815Adc1Iio = "48003000.adc:adc@0";
constexpr const char* Epm815Adc2Iio = "48003000.adc:adc@100";

const AnalogInput Epm815Inputs[] = {
    {EPM815::Adc::Pin::ANA0,  0, -1, false, "ANA0", Epm815Adc1Iio},
    {EPM815::Adc::Pin::ANA1,  1, -1, false, "ANA1", Epm815Adc1Iio},
    {EPM815::Adc::Pin::PF11,  2, -1, true,  "PF11", Epm815Adc1Iio},
    {EPM815::Adc::Pin::PA6,   3, -1, true,  "PA6",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PF12,  6, -1, true,  "PF12", Epm815Adc1Iio},
    {EPM815::Adc::Pin::PB0,   9, -1, true,  "PB0",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PC0,  10, -1, true,  "PC0",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PC3,  13, -1, true,  "PC3",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PA3,  15, -1, true,  "PA3",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PA0,  16, -1, true,  "PA0",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PA4,  18, -1, true,  "PA4",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PA5,  19, -1, true,  "PA5",  Epm815Adc1Iio},
    {EPM815::Adc::Pin::PF13,  2, -1, true,  "PF13", Epm815Adc2Iio},
    {EPM815::Adc::Pin::PF14,  6, -1, true,  "PF14", Epm815Adc2Iio},
};

// MA35 has no analog pad mode: an ADC input is selected by the pin's MFP like
// any other peripheral function. 0x8 is EADC0_CHn per the vendor kernel's
// include/dt-bindings/pinctrl/ma35d1-pinfunc.h.
constexpr const char* Epn256AdcIio = "ma35d1-eadc";

const AnalogInput Epn256Inputs[] = {
    {EPN256::Adc::Pin::PB0, 0, 0x8, true, "PB0", Epn256AdcIio},
    {EPN256::Adc::Pin::PB1, 1, 0x8, true, "PB1", Epn256AdcIio},
    {EPN256::Adc::Pin::PB2, 2, 0x8, true, "PB2", Epn256AdcIio},
    {EPN256::Adc::Pin::PB3, 3, 0x8, true, "PB3", Epn256AdcIio},
    {EPN256::Adc::Pin::PB4, 4, 0x8, true, "PB4", Epn256AdcIio},
    {EPN256::Adc::Pin::PB5, 5, 0x8, true, "PB5", Epn256AdcIio},
    {EPN256::Adc::Pin::PB6, 6, 0x8, true, "PB6", Epn256AdcIio},
    {EPN256::Adc::Pin::PB7, 7, 0x8, true, "PB7", Epn256AdcIio},
};

// MA35D0's ADC driver names its IIO device after the DT node.
constexpr const char* Epn128AdcIio = "40420000.adc";

const AnalogInput Epn128Inputs[] = {
    {EPN128::Adc::Pin::PB10, 2, 0x8, true, "PB10", Epn128AdcIio},
    {EPN128::Adc::Pin::PB11, 3, 0x8, true, "PB11", Epn128AdcIio},
};

struct EPM815BoardSupport : Stm32StyleGpio {
    const std::string& Name() const override {
        static const std::string n = "EPM815";
        return n;
    }

    void I2cInitialize(int bus, int frequency) override { EPM815::I2c::Initialize(bus, frequency); }
    void I2cUnInitialize(int bus) override              { EPM815::I2c::UnInitialize(bus); }
    int  I2cGetFrequency(int bus) override              { return EPM815::I2c::GetFrequency(bus); }

    void SpiInitialize(int bus, int bufferSize) override { EPM815::Spi::Initialize(bus, bufferSize); }
    void SpiUnInitialize(int bus) override               { EPM815::Spi::UnInitialize(bus); }

    void PwmInitialize(int pin) override    { EPM815::Pwm::Initialize(pin); }
    void PwmUnInitialize(int pin) override  { EPM815::Pwm::UnInitialize(pin); }
    int  PwmGetChipId(int pin) override     { return EPM815::Pwm::GetChipId(pin); }
    int  PwmGetChannelId(int pin) override  { return EPM815::Pwm::GetChannelId(pin); }

    void CanInitialize(const std::string& port, int bitrate) override { EPM815::Can::Initialize(port, bitrate); }
    void CanUnInitialize(const std::string& port) override            { EPM815::Can::UnInitialize(port); }

    void SerialPortInitialize(const std::string& portName, bool enableHardwareFlowControl) override {
        EPM815::SerialPort::Initialize(portName, enableHardwareFlowControl);
    }
    void SerialPortUnInitialize(const std::string& portName) override {
        EPM815::SerialPort::UnInitialize(portName);
    }

    const AnalogInput* AdcInputs(int* count) override {
        *count = static_cast<int>(std::size(Epm815Inputs));
        return Epm815Inputs;
    }
    const AnalogInput* AdcFind(int pin) override { return FindIn(Epm815Inputs, pin); }
};

struct EPN256BoardSupport : Stm32StyleGpio {
    const std::string& Name() const override {
        static const std::string n = "EPN256";
        return n;
    }

    void I2cInitialize(int bus, int frequency) override { EPN256::I2c::Initialize(bus, frequency); }
    void I2cUnInitialize(int bus) override              { EPN256::I2c::UnInitialize(bus); }
    int  I2cGetFrequency(int bus) override              { return EPN256::I2c::GetFrequency(bus); }

    void SpiInitialize(int bus, int /*bufferSize*/) override { EPN256::Spi::Initialize(bus); }
    void SpiUnInitialize(int bus) override                   { EPN256::Spi::UnInitialize(bus); }

    void PwmInitialize(int pin) override    { EPN256::Pwm::Initialize(pin); }
    void PwmUnInitialize(int pin) override  { EPN256::Pwm::UnInitialize(pin); }
    int  PwmGetChipId(int pin) override     { return EPN256::Pwm::GetChipId(pin); }
    int  PwmGetChannelId(int pin) override  { return EPN256::Pwm::GetChannelId(pin); }

    void CanInitialize(const std::string& port, int bitrate) override { EPN256::Can::Initialize(port, bitrate); }
    void CanUnInitialize(const std::string& port) override            { EPN256::Can::UnInitialize(port); }

    void SerialPortInitialize(const std::string& portName, bool enableHardwareFlowControl) override {
        EPN256::SerialPort::Initialize(portName, enableHardwareFlowControl);
    }
    void SerialPortUnInitialize(const std::string& portName) override {
        EPN256::SerialPort::UnInitialize(portName);
    }

    const AnalogInput* AdcInputs(int* count) override {
        *count = static_cast<int>(std::size(Epn256Inputs));
        return Epn256Inputs;
    }
    const AnalogInput* AdcFind(int pin) override { return FindIn(Epn256Inputs, pin); }
};

// EPN128 (Chili Pro): no SPI / PWM bonded out — inherit UnknownBoardSupport's
// throwing defaults for those. I2C / CAN / SerialPort are the same pay-for-use
// modprobe shape as EPN256.
struct EPN128BoardSupport : Stm32StyleGpio {
    const std::string& Name() const override {
        static const std::string n = "EPN128";
        return n;
    }

    void I2cInitialize(int bus, int frequency) override { EPN128::I2c::Initialize(bus, frequency); }
    void I2cUnInitialize(int bus) override              { EPN128::I2c::UnInitialize(bus); }
    int  I2cGetFrequency(int bus) override              { return EPN128::I2c::GetFrequency(bus); }

    void CanInitialize(const std::string& port, int bitrate) override { EPN128::Can::Initialize(port, bitrate); }
    void CanUnInitialize(const std::string& port) override            { EPN128::Can::UnInitialize(port); }

    void SerialPortInitialize(const std::string& portName, bool enableHardwareFlowControl) override {
        EPN128::SerialPort::Initialize(portName, enableHardwareFlowControl);
    }
    void SerialPortUnInitialize(const std::string& portName) override {
        EPN128::SerialPort::UnInitialize(portName);
    }

    const AnalogInput* AdcInputs(int* count) override {
        *count = static_cast<int>(std::size(Epn128Inputs));
        return Epn128Inputs;
    }
    const AnalogInput* AdcFind(int pin) override { return FindIn(Epn128Inputs, pin); }
};

// Pick + register the matching BoardSupport at libendpoint load. Runs once
// per process. On Chip::Unknown, nothing registers (the non-Endpoint board
// driver — e.g. endpoint-drivers/cpp/RaspberryPi/Zero2W.h — runs its own
// static initializer to install its implementation).
const auto _register_chip = []() {
    switch (ChipDetect::Current()) {
        case Chip::Epm815: BoardSupport::Register(new EPM815BoardSupport()); break;
        case Chip::Epn256: BoardSupport::Register(new EPN256BoardSupport()); break;
        case Chip::Epn128: BoardSupport::Register(new EPN128BoardSupport()); break;
        case Chip::Unknown: default: break;
    }
    return 0;
}();

}  // namespace

}  // namespace GHIElectronics::Endpoint::Core

// Force-link marker referenced from BoardSupport.h. See the comment above
// its forward declaration in that header for why this exists. The value is
// irrelevant; only the symbol presence matters.
extern "C" int endpoint_internal_board_support_init_token = 0;

// See the comment above the declaration in endpoint/BoardSupport.h.
extern "C" __attribute__((visibility("default")))
void endpoint_register_board_support(void* board_support_impl) {
    using namespace GHIElectronics::Endpoint::Core;
    BoardSupport::Register(static_cast<IBoardSupport*>(board_support_impl));
}
