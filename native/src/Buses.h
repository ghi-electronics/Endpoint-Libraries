#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

namespace GHIElectronics::Endpoint::Internal {

// As .NET's System.Device.I2c.I2cDevice on /dev/i2c-<bus>
class I2cDeviceImpl {
public:
    I2cDeviceImpl(int busId, int deviceAddress);
    ~I2cDeviceImpl();

    I2cDeviceImpl(const I2cDeviceImpl&) = delete;
    I2cDeviceImpl& operator=(const I2cDeviceImpl&) = delete;

    void Read(uint8_t* buffer, size_t length);
    void Write(const uint8_t* buffer, size_t length);
    // Write, then read with a repeated start (no stop in between)
    void WriteRead(const uint8_t* write, size_t writeLength, uint8_t* read, size_t readLength);

private:
    void Transfer(const uint8_t* write, size_t writeLength, uint8_t* read, size_t readLength);

    int fd_ = -1;
    int bus_;
    int address_;
};

// As .NET's System.Device.Spi.SpiDevice on /dev/spidev<bus>.<chipSelectLine>.
// The values are those of .NET's SpiConnectionSettings.
struct SpiSettings {
    int busId;
    int chipSelectLine;
    int mode = 0;               // SpiMode.Mode0 ... Mode3
    int dataBitLength = 8;
    int clockFrequency = 500'000;
    int dataFlow = 0;           // DataFlow.MsbFirst = 0, LsbFirst = 1
    int chipSelectActiveHigh = 0;  // ChipSelectLineActiveState High
};

class SpiDeviceImpl {
public:
    explicit SpiDeviceImpl(const SpiSettings& settings);
    ~SpiDeviceImpl();

    SpiDeviceImpl(const SpiDeviceImpl&) = delete;
    SpiDeviceImpl& operator=(const SpiDeviceImpl&) = delete;

    // write or read may be null: Read sends zeros, Write ignores what comes back
    void Transfer(const uint8_t* write, uint8_t* read, size_t length);

private:
    int fd_ = -1;
    SpiSettings settings_;
};

// As .NET's System.Device.Pwm.PwmChannel on /sys/class/pwm/pwmchip<chip>
class PwmChannelImpl {
public:
    PwmChannelImpl(int chip, int channel, int frequency, double dutyCycle);
    ~PwmChannelImpl();

    PwmChannelImpl(const PwmChannelImpl&) = delete;
    PwmChannelImpl& operator=(const PwmChannelImpl&) = delete;

    int Frequency() const { return frequency_; }
    void Frequency(int frequency);
    double DutyCycle() const { return dutyCycle_; }
    void DutyCycle(double dutyCycle);
    void Start();
    void Stop();

private:
    void Apply(int frequency, double dutyCycle);
    void WriteFile(const std::string& name, const std::string& value);

    std::string chipFolder_;
    std::string folder_;
    int channel_;
    int frequency_ = 0;
    double dutyCycle_ = 0;
    long long period_ = 0;  // ns
};

}  // namespace GHIElectronics::Endpoint::Internal
