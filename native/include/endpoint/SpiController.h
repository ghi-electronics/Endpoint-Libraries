// Chip-neutral entry point for SPI setup. Call once before using an SPI
// device: it brings up the bus and muxes its SCK/MISO/MOSI pins.
// Dispatches to the right per-chip helper based on the board detected at
// startup. Chip select is not driven by the bus: use any GPIO pin.
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

#include "Core.h"
#include "EPN256.h"
#include "EPN128.h"

namespace GHIElectronics::Endpoint::Core {

struct SpiController {
    // Connects the bus's SCK/MISO/MOSI pins. On EPM815, bufferSize also
    // sizes the kernel transfer buffer; on EPN256 the argument is accepted
    // for signature parity and otherwise ignored (that chip uses a fixed
    // DMA buffer). On EPN128 this throws because the chip does not bond
    // out SPI pins.
    static void Initialize(int bus, int bufferSize = 2048) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128:
                throw NotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out SPI pins.");
            case Chip::Epn256: EPN256::Spi::Initialize(bus); break;
            default:           EPM815::Spi::Initialize(bus, bufferSize); break;
        }
    }

    // Releases the bus's pins back to free GPIO. Silent no-op on EPN128.
    static void UnInitialize(int bus) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: return;
            case Chip::Epn256: EPN256::Spi::UnInitialize(bus); break;
            default:           EPM815::Spi::UnInitialize(bus); break;
        }
    }
};

}  // namespace GHIElectronics::Endpoint::Core
