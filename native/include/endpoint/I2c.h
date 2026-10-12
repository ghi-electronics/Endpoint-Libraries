// I2C for Endpoint, with the same API as .NET's System.Device.I2c:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::I2c;
//
//   EPM815::I2c::Initialize(EPM815::I2c::I2c1);
//   auto device = I2cDevice::Create(I2cConnectionSettings(EPM815::I2c::I2c1, 0x50));
//   uint8_t data[2];
//   device.WriteRead(std::array<uint8_t, 1>{0x00}, data);
#pragma once

#include <cstdint>
#include <span>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::I2c {

struct I2cConnectionSettings {
    I2cConnectionSettings(int busId, int deviceAddress) : BusId(busId), DeviceAddress(deviceAddress) {}

    int BusId;
    int DeviceAddress;
};

// A device on an I2C bus (call EPM815::I2c::Initialize for the bus first)
class I2cDevice {
public:
    static I2cDevice Create(const I2cConnectionSettings& settings) { return I2cDevice(settings); }

    explicit I2cDevice(const I2cConnectionSettings& settings) : settings_(settings) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_i2cdevice_create(settings.BusId, settings.DeviceAddress, &handle));
        handle_ = Handle(handle);
    }

    const I2cConnectionSettings& ConnectionSettings() const { return settings_; }

    void Read(std::span<uint8_t> buffer) {
        Core::detail::Check(endpoint_i2cdevice_read(h(), buffer.data(), static_cast<int>(buffer.size())));
    }

    uint8_t ReadByte() {
        uint8_t value = 0;
        Read(std::span<uint8_t>(&value, 1));
        return value;
    }

    void Write(std::span<const uint8_t> buffer) {
        Core::detail::Check(endpoint_i2cdevice_write(h(), buffer.data(), static_cast<int>(buffer.size())));
    }

    void WriteByte(uint8_t value) { Write(std::span<const uint8_t>(&value, 1)); }

    // Writes, then reads with a repeated start (no stop in between)
    void WriteRead(std::span<const uint8_t> writeBuffer, std::span<uint8_t> readBuffer) {
        Core::detail::Check(endpoint_i2cdevice_write_read(h(), writeBuffer.data(), static_cast<int>(writeBuffer.size()),
                                                          readBuffer.data(), static_cast<int>(readBuffer.size())));
    }

    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_i2cdevice_dispose>;

    void* h() const { return handle_.Get("I2cDevice"); }

    I2cConnectionSettings settings_;
    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::I2c
