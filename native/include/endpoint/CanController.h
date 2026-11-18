// Chip-neutral entry point for CAN-bus setup. Call once before using the
// CAN device API: it muxes the TX/RX pins and brings the canN network
// interface up at the chosen bitrate. Dispatches via BoardSupport::Current().
//
//   using namespace GHIElectronics::Endpoint::Core;
//   CanController::Initialize(EPN256::Can::Can0, 500000);
//   // ... send / receive frames via SocketCAN ...
//   CanController::UnInitialize(EPN256::Can::Can0);
//
// Mirrors .NET's GHIElectronics.Endpoint.Core.CanController.
#pragma once

#include <string>

#include "BoardSupport.h"

namespace GHIElectronics::Endpoint::Core {

struct CanController {
    static void Initialize(const std::string& port, int bitrate = 500'000) {
        BoardSupport::Current().CanInitialize(port, bitrate);
    }

    static void UnInitialize(const std::string& port) {
        BoardSupport::Current().CanUnInitialize(port);
    }
};

}  // namespace GHIElectronics::Endpoint::Core
