// Chip-neutral entry point for CAN-bus setup. Call once before using the
// CAN device API: it muxes the TX/RX pins and brings the canN network
// interface up at the chosen bitrate. Dispatches to the right per-chip
// helper based on the board detected at startup.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   CanController::Initialize(EPN256::Can::Can0, 500000);
//   // ... send / receive frames via SocketCAN ...
//   CanController::UnInitialize(EPN256::Can::Can0);
//
// Mirrors .NET's GHIElectronics.Endpoint.Core.CanController.
#pragma once

#include <string>

#include "Core.h"
#include "EPN256.h"
#include "EPN128.h"

namespace GHIElectronics::Endpoint::Core {

struct CanController {
    // Muxes the CAN pins and configures the bitrate.
    static void Initialize(const std::string& port, int bitrate = 500'000) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::Can::Initialize(port, bitrate); break;
            case Chip::Epn256: EPN256::Can::Initialize(port, bitrate); break;
            default:           EPM815::Can::Initialize(port, bitrate); break;
        }
    }

    // Takes the interface down and releases its pins.
    static void UnInitialize(const std::string& port) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::Can::UnInitialize(port); break;
            case Chip::Epn256: EPN256::Can::UnInitialize(port); break;
            default:           EPM815::Can::UnInitialize(port); break;
        }
    }
};

}  // namespace GHIElectronics::Endpoint::Core
