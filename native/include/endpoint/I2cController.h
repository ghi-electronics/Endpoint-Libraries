// Chip-neutral entry point for I2C setup. Call once before using an I2C
// device: it brings up the bus and muxes its pins. Dispatches to the
// right per-chip helper based on the board detected at startup.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   I2cController::Initialize(EPN256::I2c::I2c5);
//   // ... open /dev/i2c-5 and talk to the slave ...
//   I2cController::UnInitialize(EPN256::I2c::I2c5);
//
// Change EPN256 to EPM815 or EPN128 to target a different board; nothing
// else has to change. Mirrors .NET's GHIElectronics.Endpoint.Core.I2cController.
#pragma once

#include "Core.h"
#include "EPN256.h"
#include "EPN128.h"

namespace GHIElectronics::Endpoint::Core {

struct I2cController {
    // Connects the bus's pins and sets its speed. Only the two pins of this
    // bus are claimed; every other bus's pins stay free.
    static void Initialize(int bus, int frequency = 400'000) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::I2c::Initialize(bus, frequency); break;
            case Chip::Epn256: EPN256::I2c::Initialize(bus, frequency); break;
            default:           EPM815::I2c::Initialize(bus, frequency); break;
        }
    }

    // Releases the bus's pins back to free GPIO.
    static void UnInitialize(int bus) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::I2c::UnInitialize(bus); break;
            case Chip::Epn256: EPN256::I2c::UnInitialize(bus); break;
            default:           EPM815::I2c::UnInitialize(bus); break;
        }
    }

    // Current bus speed in Hz.
    static int GetFrequency(int bus) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: return EPN128::I2c::GetFrequency(bus);
            case Chip::Epn256: return EPN256::I2c::GetFrequency(bus);
            default:           return EPM815::I2c::GetFrequency(bus);
        }
    }
};

}  // namespace GHIElectronics::Endpoint::Core
