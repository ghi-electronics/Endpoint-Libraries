// CAN for Endpoint in C++, with the same API as .NET IoT's
// Iot.Device.SocketCan (Python programs use python-can):
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Can;
//
//   EPM815::Can::Initialize(EPM815::Can::Can1, 500'000);
//   CanRaw can(EPM815::Can::Can1);
//   can.WriteFrame(std::array<uint8_t, 3>{1, 2, 3}, CanId{.Standard = 0x123});
//
// Classic CAN: up to 8 data bytes. CAN FD comes later.
#pragma once

#include <cstdint>
#include <cstring>
#include <span>
#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Can {

// A CAN identifier, as .NET IoT's CanId: set Standard (11 bits), or
// Extended (29 bits) with ExtendedFrameFormat
struct CanId {
    uint32_t Standard = 0;
    uint32_t Extended = 0;
    bool ExtendedFrameFormat = false;
    bool RemoteTransmissionRequest = false;
    bool Error = false;

    static constexpr uint32_t ExtendedFlag = 0x80000000, RemoteFlag = 0x40000000, ErrorFlag = 0x20000000;

    // The raw identifier with the flags
    uint32_t Value() const {
        return (ExtendedFrameFormat ? (Extended & 0x1FFFFFFF) | ExtendedFlag : (Standard & 0x7FF)) |
               (RemoteTransmissionRequest ? RemoteFlag : 0) | (Error ? ErrorFlag : 0);
    }

    bool IsValid() const { return ExtendedFrameFormat ? Extended <= 0x1FFFFFFF : Standard <= 0x7FF; }

    static CanId FromValue(uint32_t value) {
        CanId id;
        id.ExtendedFrameFormat = (value & ExtendedFlag) != 0;
        id.RemoteTransmissionRequest = (value & RemoteFlag) != 0;
        id.Error = (value & ErrorFlag) != 0;
        if (id.ExtendedFrameFormat) {
            id.Extended = value & 0x1FFFFFFF;
        }
        else {
            id.Standard = value & 0x7FF;
        }
        return id;
    }
};

// Reads and writes frames on a CAN port (call EPM815::Can::Initialize first)
class CanRaw {
public:
    explicit CanRaw(const std::string& networkInterface) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_canraw_open(networkInterface.c_str(), &handle));
        handle_ = Handle(handle);
    }

    void WriteFrame(std::span<const uint8_t> data, CanId id) {
        if (!id.IsValid()) {
            throw Core::ArgumentException("The CAN id is not valid");
        }
        Core::detail::Check(endpoint_canraw_write_frame(h(), id.Value(), data.data(), static_cast<int>(data.size())));
    }

    // Waits for a frame; false if it cannot be read
    bool TryReadFrame(std::span<uint8_t> data, int& frameLength, CanId& id) {
        uint32_t raw = 0;
        int length = 0, ok = 0;
        Core::detail::Check(endpoint_canraw_read_frame(h(), &raw, data.data(), static_cast<int>(data.size()), &length, &ok));
        if (!ok) {
            return false;
        }
        frameLength = length;
        id = CanId::FromValue(raw);
        return true;
    }

    // Receives only frames with this id
    void Filter(CanId id) { Core::detail::Check(endpoint_canraw_filter(h(), id.Value())); }

    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_canraw_close>;

    void* h() const { return handle_.Get("CanRaw"); }

    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Can
