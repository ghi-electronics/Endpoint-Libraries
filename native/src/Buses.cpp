#include "Buses.h"

#include <fcntl.h>
#include <linux/i2c-dev.h>
#include <linux/i2c.h>
#include <linux/spi/spidev.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <cerrno>
#include <cmath>
#include <cstdio>
#include <filesystem>

#include "Errors.h"
#include "KernelModules.h"

namespace GHIElectronics::Endpoint::Internal {

namespace {

std::string Hex2(int value) {
    char text[8];
    std::snprintf(text, sizeof text, "0x%02X", value);
    return text;
}

}  // namespace

// ---------------------------------------------------------------- I2C

I2cDeviceImpl::I2cDeviceImpl(int busId, int deviceAddress) : bus_(busId), address_(deviceAddress) {
    if (deviceAddress < 0 || deviceAddress > 0x7F) {
        ThrowArgumentOutOfRange("I2C addresses are 0x00 to 0x7F: " + std::to_string(deviceAddress));
    }
    auto path = "/dev/i2c-" + std::to_string(busId);
    fd_ = open(path.c_str(), O_RDWR | O_CLOEXEC);
    if (fd_ < 0) {
        ThrowSystemError("Cannot open " + path);
    }
}

I2cDeviceImpl::~I2cDeviceImpl() {
    if (fd_ >= 0) {
        close(fd_);
    }
}

void I2cDeviceImpl::Transfer(const uint8_t* write, size_t writeLength, uint8_t* read, size_t readLength) {
    i2c_msg messages[2];
    int count = 0;
    if (writeLength > 0) {
        messages[count++] = i2c_msg{static_cast<__u16>(address_), 0, static_cast<__u16>(writeLength), const_cast<uint8_t*>(write)};
    }
    if (readLength > 0) {
        messages[count++] = i2c_msg{static_cast<__u16>(address_), I2C_M_RD, static_cast<__u16>(readLength), read};
    }
    if (count == 0) {
        return;
    }
    i2c_rdwr_ioctl_data data{messages, static_cast<__u32>(count)};
    if (ioctl(fd_, I2C_RDWR, &data) < 0) {
        ThrowSystemError("I2C transfer with device " + Hex2(address_) + " on bus " + std::to_string(bus_) + " failed");
    }
}

void I2cDeviceImpl::Read(uint8_t* buffer, size_t length) { Transfer(nullptr, 0, buffer, length); }

void I2cDeviceImpl::Write(const uint8_t* buffer, size_t length) { Transfer(buffer, length, nullptr, 0); }

void I2cDeviceImpl::WriteRead(const uint8_t* write, size_t writeLength, uint8_t* read, size_t readLength) {
    Transfer(write, writeLength, read, readLength);
}

// ---------------------------------------------------------------- SPI

SpiDeviceImpl::SpiDeviceImpl(const SpiSettings& settings) : settings_(settings) {
    if (settings.mode < 0 || settings.mode > 3) {
        ThrowArgumentOutOfRange("SPI modes are Mode0 to Mode3");
    }
    if (settings.dataBitLength < 1 || settings.dataBitLength > 32) {
        ThrowArgumentOutOfRange("The data bit length must be 1 to 32");
    }
    if (settings.clockFrequency <= 0) {
        ThrowArgumentOutOfRange("The clock frequency must be more than 0");
    }

    // The bus drives no chip select (programs use any GPIO pin): -1, .NET's
    // "no chip select", is the bus's only device file, spidevN.0
    auto chipSelect = settings.chipSelectLine < 0 ? 0 : settings.chipSelectLine;
    auto path = "/dev/spidev" + std::to_string(settings.busId) + "." + std::to_string(chipSelect);
    fd_ = open(path.c_str(), O_RDWR | O_CLOEXEC);
    if (fd_ < 0) {
        if (errno == ENOENT) {
            ThrowInvalidOperation(path + " does not exist: call EPM815.Spi.Initialize for the bus first");
        }
        ThrowSystemError("Cannot open " + path);
    }

    uint32_t mode = static_cast<uint32_t>(settings.mode) | (settings.dataFlow == 1 ? SPI_LSB_FIRST : 0) |
                    (settings.chipSelectActiveHigh ? SPI_CS_HIGH : 0);
    uint8_t bits = static_cast<uint8_t>(settings.dataBitLength);
    uint32_t speed = static_cast<uint32_t>(settings.clockFrequency);
    if (ioctl(fd_, SPI_IOC_WR_MODE32, &mode) < 0 || ioctl(fd_, SPI_IOC_WR_BITS_PER_WORD, &bits) < 0 ||
        ioctl(fd_, SPI_IOC_WR_MAX_SPEED_HZ, &speed) < 0) {
        auto error = errno;
        close(fd_);
        fd_ = -1;
        errno = error;
        ThrowSystemError("Cannot set up " + path);
    }
}

SpiDeviceImpl::~SpiDeviceImpl() {
    if (fd_ >= 0) {
        close(fd_);
    }
}

void SpiDeviceImpl::Transfer(const uint8_t* write, uint8_t* read, size_t length) {
    if (length == 0) {
        return;
    }
    spi_ioc_transfer transfer{};
    transfer.tx_buf = reinterpret_cast<uintptr_t>(write);
    transfer.rx_buf = reinterpret_cast<uintptr_t>(read);
    transfer.len = static_cast<__u32>(length);
    transfer.speed_hz = static_cast<__u32>(settings_.clockFrequency);
    transfer.bits_per_word = static_cast<__u8>(settings_.dataBitLength);
    if (ioctl(fd_, SPI_IOC_MESSAGE(1), &transfer) < 0) {
        if (errno == EMSGSIZE) {
            ThrowArgument("The SPI transfer of " + std::to_string(length) +
                          " bytes is longer than the driver's buffer: give a larger size to EPM815.Spi.Initialize");
        }
        ThrowSystemError("SPI transfer on bus " + std::to_string(settings_.busId) + " failed");
    }
}

// ---------------------------------------------------------------- PWM

PwmChannelImpl::PwmChannelImpl(int chip, int channel, int frequency, double dutyCycle) : channel_(channel) {
    if (frequency <= 0) {
        ThrowArgumentOutOfRange("The frequency must be more than 0");
    }
    if (dutyCycle < 0 || dutyCycle > 1) {
        ThrowArgumentOutOfRange("The duty cycle must be 0.0 to 1.0");
    }
    chipFolder_ = "/sys/class/pwm/pwmchip" + std::to_string(chip);
    folder_ = chipFolder_ + "/pwm" + std::to_string(channel);
    if (!std::filesystem::exists(chipFolder_)) {
        ThrowArgument("There is no PWM chip " + std::to_string(chip));
    }
    if (!std::filesystem::exists(folder_)) {
        WriteFile(chipFolder_ + "/export", std::to_string(channel));
        KernelModules::WaitFor(folder_ + "/enable");
    }
    // A channel left exported by an earlier program keeps its period
    if (auto file = std::fopen((folder_ + "/period").c_str(), "r")) {
        long long period = 0;
        if (std::fscanf(file, "%lld", &period) == 1) {
            period_ = period;
        }
        std::fclose(file);
    }
    Apply(frequency, dutyCycle);
}

PwmChannelImpl::~PwmChannelImpl() {
    try {
        Stop();
        WriteFile(chipFolder_ + "/unexport", std::to_string(channel_));
    }
    catch (...) {
    }
}

void PwmChannelImpl::WriteFile(const std::string& path, const std::string& value) {
    auto fd = open(path.c_str(), O_WRONLY | O_CLOEXEC);
    if (fd < 0) {
        ThrowSystemError("Cannot open " + path);
    }
    auto written = write(fd, value.data(), value.size());
    auto error = errno;
    close(fd);
    if (written != static_cast<ssize_t>(value.size())) {
        errno = error;
        ThrowSystemError("Cannot write " + value + " to " + path);
    }
}

void PwmChannelImpl::Apply(int frequency, double dutyCycle) {
    auto period = std::llround(1e9 / frequency);
    auto duty = std::llround(period * dutyCycle);
    // The duty cycle may never be longer than the period, also between the two writes
    if (period < period_) {
        WriteFile(folder_ + "/duty_cycle", std::to_string(duty));
        WriteFile(folder_ + "/period", std::to_string(period));
    }
    else {
        WriteFile(folder_ + "/period", std::to_string(period));
        WriteFile(folder_ + "/duty_cycle", std::to_string(duty));
    }
    period_ = period;
    frequency_ = frequency;
    dutyCycle_ = dutyCycle;
}

void PwmChannelImpl::Frequency(int frequency) {
    if (frequency <= 0) {
        ThrowArgumentOutOfRange("The frequency must be more than 0");
    }
    Apply(frequency, dutyCycle_);
}

void PwmChannelImpl::DutyCycle(double dutyCycle) {
    if (dutyCycle < 0 || dutyCycle > 1) {
        ThrowArgumentOutOfRange("The duty cycle must be 0.0 to 1.0");
    }
    Apply(frequency_, dutyCycle);
}

void PwmChannelImpl::Start() { WriteFile(folder_ + "/enable", "1"); }

void PwmChannelImpl::Stop() { WriteFile(folder_ + "/enable", "0"); }

}  // namespace GHIElectronics::Endpoint::Internal
