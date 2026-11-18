// Chip-neutral entry point for SPI setup. Call once before using an SPI
// device: it brings up the bus and muxes its SCK/MISO/MOSI pins.
// Dispatches via BoardSupport::Current(). Chip select is not driven by
// the bus: use any GPIO pin.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   SpiController::Initialize(EPN256::Spi::Spi0);
//   // ... open /dev/spidev0.0 and transfer ...
//   SpiController::UnInitialize(EPN256::Spi::Spi0);
//
// EPN128 (MA35D0 Chili Pro) does not bond out SPI pins; Initialize throws
// NotSupportedException there and UnInitialize is a silent no-op. Mirrors
// .NET's GHIElectronics.Endpoint.Core.SpiController.
#pragma once

#include "BoardSupport.h"

namespace GHIElectronics::Endpoint::Core {

struct SpiController {
    static void Initialize(int bus, int bufferSize = 2048) {
        BoardSupport::Current().SpiInitialize(bus, bufferSize);
    }

    static void UnInitialize(int bus) {
        BoardSupport::Current().SpiUnInitialize(bus);
    }
};

}  // namespace GHIElectronics::Endpoint::Core
