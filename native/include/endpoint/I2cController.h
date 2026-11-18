// Chip-neutral entry point for I2C setup. Call once before using an I2C
// device: it brings up the bus and muxes its pins. Dispatches via
// BoardSupport::Current() so the same code works on every board.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   I2cController::Initialize(EPN256::I2c::I2c5);
//   // ... open /dev/i2c-5 and talk to the slave ...
//   I2cController::UnInitialize(EPN256::I2c::I2c5);
//
// On a Pi Zero 2 W (after including the RaspberryPi/Zero2W.h header):
//   I2cController::Initialize(Zero2W::I2c::I2c1);
//
// Mirrors .NET's GHIElectronics.Endpoint.Core.I2cController.
#pragma once

#include "BoardSupport.h"

namespace GHIElectronics::Endpoint::Core {

struct I2cController {
    // Connects the bus's pins (open drain, internal pull-ups on) and sets its speed.
    static void Initialize(int bus, int frequency = 400'000) {
        BoardSupport::Current().I2cInitialize(bus, frequency);
    }

    // Releases the bus's pins back to free GPIO.
    static void UnInitialize(int bus) {
        BoardSupport::Current().I2cUnInitialize(bus);
    }

    // Current bus speed in Hz.
    static int GetFrequency(int bus) {
        return BoardSupport::Current().I2cGetFrequency(bus);
    }
};

}  // namespace GHIElectronics::Endpoint::Core
