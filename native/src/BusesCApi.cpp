// The hidden C layer (see CApi.h) for the EPM815 board setup and the I2C,
// SPI and PWM devices. Devices are handles; buffers are pointer and length.

#include <cstdint>

#include "Buses.h"
#include "CApi.h"
#include "Epm815.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {
template <typename T>
T* Handle(void* handle, const char* name) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, std::string("The ") + name + " is disposed");
    }
    return static_cast<T*>(handle);
}
}  // namespace

// ---------------------------------------------------------------- EPM815 setup

ENDPOINT_API int endpoint_i2c_initialize(int bus, int frequency) {
    return Call([&] { Epm815::I2c::Initialize(bus, frequency); });
}

ENDPOINT_API int endpoint_i2c_uninitialize(int bus) { return Call([&] { Epm815::I2c::UnInitialize(bus); }); }

ENDPOINT_API int endpoint_i2c_get_frequency(int bus, int* frequency) {
    return Call([&] { *frequency = Epm815::I2c::GetFrequency(bus); });
}

ENDPOINT_API int endpoint_spi_initialize(int bus, int bufferSize) {
    return Call([&] { Epm815::Spi::Initialize(bus, bufferSize); });
}

ENDPOINT_API int endpoint_spi_uninitialize(int bus) { return Call([&] { Epm815::Spi::UnInitialize(bus); }); }

ENDPOINT_API int endpoint_pwm_initialize(int pin) { return Call([&] { Epm815::Pwm::Initialize(pin); }); }

ENDPOINT_API int endpoint_pwm_uninitialize(int pin) { return Call([&] { Epm815::Pwm::UnInitialize(pin); }); }

ENDPOINT_API int endpoint_pwm_get_chip_id(int pin, int* chip) {
    return Call([&] { *chip = Epm815::Pwm::GetChipId(pin); });
}

ENDPOINT_API int endpoint_pwm_get_channel_id(int pin, int* channel) {
    return Call([&] { *channel = Epm815::Pwm::GetChannelId(pin); });
}

ENDPOINT_API int endpoint_serialport_initialize(const char* portName, int enableHardwareFlowControl) {
    return Call([&] { Epm815::SerialPort::Initialize(portName, enableHardwareFlowControl != 0); });
}

ENDPOINT_API int endpoint_serialport_uninitialize(const char* portName) {
    return Call([&] { Epm815::SerialPort::UnInitialize(portName); });
}

ENDPOINT_API int endpoint_can_initialize(const char* port, int bitrate) {
    return Call([&] { Epm815::Can::Initialize(port, bitrate); });
}

ENDPOINT_API int endpoint_can_uninitialize(const char* port) { return Call([&] { Epm815::Can::UnInitialize(port); }); }

// ---------------------------------------------------------------- I2cDevice

ENDPOINT_API int endpoint_i2cdevice_create(int busId, int deviceAddress, void** device) {
    return Call([&] { *device = new I2cDeviceImpl(busId, deviceAddress); });
}

ENDPOINT_API int endpoint_i2cdevice_dispose(void* device) {
    return Call([&] { delete static_cast<I2cDeviceImpl*>(device); });
}

ENDPOINT_API int endpoint_i2cdevice_read(void* device, uint8_t* buffer, int length) {
    return Call([&] { Handle<I2cDeviceImpl>(device, "I2cDevice")->Read(buffer, length); });
}

ENDPOINT_API int endpoint_i2cdevice_write(void* device, const uint8_t* buffer, int length) {
    return Call([&] { Handle<I2cDeviceImpl>(device, "I2cDevice")->Write(buffer, length); });
}

ENDPOINT_API int endpoint_i2cdevice_write_read(void* device, const uint8_t* write, int writeLength, uint8_t* read,
                                               int readLength) {
    return Call([&] { Handle<I2cDeviceImpl>(device, "I2cDevice")->WriteRead(write, writeLength, read, readLength); });
}

// ---------------------------------------------------------------- SpiDevice

ENDPOINT_API int endpoint_spidevice_create(int busId, int chipSelectLine, int mode, int dataBitLength, int clockFrequency,
                                           int dataFlow, int chipSelectActiveHigh, void** device) {
    return Call([&] {
        *device = new SpiDeviceImpl(
            SpiSettings{busId, chipSelectLine, mode, dataBitLength, clockFrequency, dataFlow, chipSelectActiveHigh});
    });
}

ENDPOINT_API int endpoint_spidevice_dispose(void* device) {
    return Call([&] { delete static_cast<SpiDeviceImpl*>(device); });
}

// write or read may be null
ENDPOINT_API int endpoint_spidevice_transfer(void* device, const uint8_t* write, uint8_t* read, int length) {
    return Call([&] { Handle<SpiDeviceImpl>(device, "SpiDevice")->Transfer(write, read, length); });
}

// ---------------------------------------------------------------- PwmChannel

ENDPOINT_API int endpoint_pwmchannel_create(int chip, int channel, int frequency, double dutyCycle, void** pwm) {
    return Call([&] { *pwm = new PwmChannelImpl(chip, channel, frequency, dutyCycle); });
}

ENDPOINT_API int endpoint_pwmchannel_dispose(void* pwm) {
    return Call([&] { delete static_cast<PwmChannelImpl*>(pwm); });
}

ENDPOINT_API int endpoint_pwmchannel_get_frequency(void* pwm, int* frequency) {
    return Call([&] { *frequency = Handle<PwmChannelImpl>(pwm, "PwmChannel")->Frequency(); });
}

ENDPOINT_API int endpoint_pwmchannel_set_frequency(void* pwm, int frequency) {
    return Call([&] { Handle<PwmChannelImpl>(pwm, "PwmChannel")->Frequency(frequency); });
}

ENDPOINT_API int endpoint_pwmchannel_get_duty_cycle(void* pwm, double* dutyCycle) {
    return Call([&] { *dutyCycle = Handle<PwmChannelImpl>(pwm, "PwmChannel")->DutyCycle(); });
}

ENDPOINT_API int endpoint_pwmchannel_set_duty_cycle(void* pwm, double dutyCycle) {
    return Call([&] { Handle<PwmChannelImpl>(pwm, "PwmChannel")->DutyCycle(dutyCycle); });
}

ENDPOINT_API int endpoint_pwmchannel_start(void* pwm) {
    return Call([&] { Handle<PwmChannelImpl>(pwm, "PwmChannel")->Start(); });
}

ENDPOINT_API int endpoint_pwmchannel_stop(void* pwm) {
    return Call([&] { Handle<PwmChannelImpl>(pwm, "PwmChannel")->Stop(); });
}
