// Chip-neutral entry point for PWM setup. Call once before using a PWM
// channel: it brings up the PWM channel and muxes its pin. Dispatches to
// the right per-chip helper based on the board detected at startup.
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

#include "Core.h"
#include "EPN256.h"
#include "EPN128.h"

namespace GHIElectronics::Endpoint::Core {

struct PwmController {
    // Brings up a PWM channel. The argument is a pin constant from the
    // chip's namespace (for example EPM815::Pwm::Pin::PA8 or
    // EPN256::Pwm::Pin::PK5); only that pin is claimed.
    static void Initialize(int pin) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128:
                throw NotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
            case Chip::Epn256: EPN256::Pwm::Initialize(pin); break;
            default:           EPM815::Pwm::Initialize(pin); break;
        }
    }

    // Releases the PWM pin back to free GPIO. Silent no-op on EPN128.
    static void UnInitialize(int pin) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: return;
            case Chip::Epn256: EPN256::Pwm::UnInitialize(pin); break;
            default:           EPM815::Pwm::UnInitialize(pin); break;
        }
    }

    // Kernel pwmchipN index to pass to the PWM channel API.
    static int GetChipId(int pin) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128:
                throw NotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
            case Chip::Epn256: return EPN256::Pwm::GetChipId(pin);
            default:           return EPM815::Pwm::GetChipId(pin);
        }
    }

    // Channel index within the PWM chip to pass to the PWM channel API.
    static int GetChannelId(int pin) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128:
                throw NotSupportedException("EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.");
            case Chip::Epn256: return EPN256::Pwm::GetChannelId(pin);
            default:           return EPM815::Pwm::GetChannelId(pin);
        }
    }
};

}  // namespace GHIElectronics::Endpoint::Core
