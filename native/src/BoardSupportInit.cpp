// BoardSupport implementations for the Endpoint-board chips (EPM815 /
// EPDomino24, EPN256, EPN128) and a static initializer that picks the
// right one at libendpoint load based on ChipDetect::Current().
//
// On an unknown chip (Pi Zero 2 W or an unrecognized host), none of these
// register and BoardSupport::Current() stays as UnknownBoardSupport.
// Non-Endpoint boards (Pi via endpoint-drivers/cpp/RaspberryPi/Zero2W.h)
// run their own static initializer in user code to install their
// BoardSupport.

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
