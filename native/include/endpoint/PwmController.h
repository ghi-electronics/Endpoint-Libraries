// Chip-neutral entry point for PWM setup. Call once before using a PWM
// channel: it brings up the PWM channel and muxes its pin. Dispatches via
// BoardSupport::Current().
//
//   using namespace GHIElectronics::Endpoint::Core;
//   PwmController::Initialize(EPN256::Pwm::Pin::PK5);
//   int chip    = PwmController::GetChipId(EPN256::Pwm::Pin::PK5);
//   int channel = PwmController::GetChannelId(EPN256::Pwm::Pin::PK5);
//   // ... open /sys/class/pwm/pwmchip<chip>/pwm<channel> ...
//
// EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins; Initialize /
// GetChipId / GetChannelId throw NotSupportedException there and
// UnInitialize is a silent no-op. Mirrors .NET's
// GHIElectronics.Endpoint.Core.PwmController.
#pragma once

#include "BoardSupport.h"

namespace GHIElectronics::Endpoint::Core {

struct PwmController {
    static void Initialize(int pin) {
        BoardSupport::Current().PwmInitialize(pin);
    }

    static void UnInitialize(int pin) {
        BoardSupport::Current().PwmUnInitialize(pin);
    }

    static int GetChipId(int pin) {
        return BoardSupport::Current().PwmGetChipId(pin);
    }

    static int GetChannelId(int pin) {
        return BoardSupport::Current().PwmGetChannelId(pin);
    }
};

}  // namespace GHIElectronics::Endpoint::Core
