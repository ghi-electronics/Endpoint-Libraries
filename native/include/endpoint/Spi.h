// SPI for Endpoint, with the same API as .NET's System.Device.Spi:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Spi;
//
//   EPM815::Spi::Initialize(EPM815::Spi::Spi4);
//   SpiConnectionSettings settings(EPM815::Spi::Spi4);
//   settings.ClockFrequency = 1'000'000;
//   auto device = SpiDevice::Create(settings);
//   uint8_t received[4];
//   device.TransferFullDuplex(std::array<uint8_t, 4>{1, 2, 3, 4}, received);
//
// Chip select is not driven by the bus: use any GPIO pin.
#pragma once

#include <cstdint>
#include <span>

#include "Gpio.h"
#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Spi {

enum class SpiMode { Mode0 = 0, Mode1 = 1, Mode2 = 2, Mode3 = 3 };

enum class DataFlow { MsbFirst = 0, LsbFirst = 1 };

struct SpiConnectionSettings {
    explicit SpiConnectionSettings(int busId, int chipSelectLine = -1) : BusId(busId), ChipSelectLine(chipSelectLine) {}

    int BusId;
    int ChipSelectLine;
    SpiMode Mode = SpiMode::Mode0;
    int DataBitLength = 8;
    int ClockFrequency = 500'000;
    Spi::DataFlow DataFlow = Spi::DataFlow::MsbFirst;
    Gpio::PinValue ChipSelectLineActiveState = Gpio::PinValue::Low;
};

// A device on an SPI bus (call EPM815::Spi::Initialize for the bus first)
class SpiDevice {
public:
    static SpiDevice Create(const SpiConnectionSettings& settings) { return SpiDevice(settings); }

    explicit SpiDevice(const SpiConnectionSettings& settings) : settings_(settings) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_spidevice_create(settings.BusId, settings.ChipSelectLine, static_cast<int>(settings.Mode),
                                                      settings.DataBitLength, settings.ClockFrequency,
                                                      static_cast<int>(settings.DataFlow),
                                                      static_cast<int>(settings.ChipSelectLineActiveState), &handle));
        handle_ = Handle(handle);
    }

    const SpiConnectionSettings& ConnectionSettings() const { return settings_; }

    // Reads (sending zeros)
    void Read(std::span<uint8_t> buffer) {
        Core::detail::Check(endpoint_spidevice_transfer(h(), nullptr, buffer.data(), static_cast<int>(buffer.size())));
    }

    uint8_t ReadByte() {
        uint8_t value = 0;
        Read(std::span<uint8_t>(&value, 1));
        return value;
    }

    void Write(std::span<const uint8_t> buffer) {
        Core::detail::Check(endpoint_spidevice_transfer(h(), buffer.data(), nullptr, static_cast<int>(buffer.size())));
    }

    void WriteByte(uint8_t value) { Write(std::span<const uint8_t>(&value, 1)); }

    // Sends writeBuffer while reading into readBuffer (the same length)
    void TransferFullDuplex(std::span<const uint8_t> writeBuffer, std::span<uint8_t> readBuffer) {
        if (writeBuffer.size() != readBuffer.size()) {
            throw Core::ArgumentException("The write and read buffers must have the same length");
        }
        Core::detail::Check(
            endpoint_spidevice_transfer(h(), writeBuffer.data(), readBuffer.data(), static_cast<int>(readBuffer.size())));
    }

    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_spidevice_dispose>;

    void* h() const { return handle_.Get("SpiDevice"); }

    SpiConnectionSettings settings_;
    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Spi
