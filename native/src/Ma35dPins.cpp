#include "Ma35dPins.h"

#include "Errors.h"
#include "Pinmux.h"

namespace GHIElectronics::Endpoint::Internal::Ma35dPins {

namespace {

// Function packing. Keep in sync with Endpoint-Firmware/pinmux/src/ma35d.c:
//   [Slew:2][Pull:2][Mode:2][MFP:4]
constexpr int MfpShift  = 0, MfpMask  = 0xF;
constexpr int ModeShift = 4, ModeMask = 0x3;
constexpr int PullShift = 6, PullMask = 0x3;
constexpr int SlewShift = 8, SlewMask = 0x3;

int ToPin(int port, int line) {
    if (line < 0 || line > 15) {
        ThrowArgumentOutOfRange("Pins are 0 to 15 in a port");
    }
    if (port < 0 || port > MaxPort) {
        ThrowArgumentOutOfRange("Ports are A to N");
    }
    return port * 16 + line;
}

// Chip-neutral Speed (4 values) to MA35 SLEWCTL (3 values). Lossy: Medium and
// High both collapse to "high". Same table as Ma35dPins.SpeedToSlew in .NET.
uint32_t SpeedToSlew(Speed speed) {
    switch (speed) {
        case Speed::Low:      return 0;
        case Speed::VeryHigh: return 2;
        default:              return 1;
    }
}

uint32_t Pack(Mode mode, Pull pull, Speed speed, int mfp) {
    return (SpeedToSlew(speed) << SlewShift) |
           ((static_cast<uint32_t>(pull) & PullMask) << PullShift) |
           ((static_cast<uint32_t>(mode) & ModeMask) << ModeShift) |
           ((static_cast<uint32_t>(mfp)  & MfpMask)  << MfpShift);
}

uint32_t Replace(uint32_t v, int shift, uint32_t mask, uint32_t field) {
    return (v & ~(mask << shift)) | ((field & mask) << shift);
}

}  // namespace

void SetMode(int port, int line, Mode mode) {
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), ModeShift, ModeMask, static_cast<uint32_t>(mode)));
}

void SetPull(int port, int line, Pull pull) {
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), PullShift, PullMask, static_cast<uint32_t>(pull)));
}

void SetSpeed(int port, int line, Speed speed) {
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), SlewShift, SlewMask, SpeedToSlew(speed)));
}

void SetOutputType(int port, int line, OutputType type) {
    SetMode(port, line, type == OutputType::OpenDrain ? Mode::OpenDrain : Mode::Output);
}

void SetAlternate(int port, int line, int function) {
    if (function < 0 || function > 15) {
        ThrowArgumentOutOfRange("MFP functions are 0 to 15");
    }
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), MfpShift, MfpMask, static_cast<uint32_t>(function)));
}

State Save(int port, int line) {
    return State{Pinmux::Get(ToPin(port, line))};
}

void Restore(int port, int line, const State& state) {
    Pinmux::Set(ToPin(port, line), state.packed);
}

void SetFunction(int port, int line, int function, Pull pull, OutputType, Speed speed) {
    if (function < 0 || function > 15) {
        ThrowArgumentOutOfRange("MFP functions are 0 to 15");
    }
    Pinmux::Set(ToPin(port, line), Pack(Mode::Input, pull, speed, function));
}

PortLine FromPin(int pin) {
    return PortLine{pin / 16, pin % 16};
}

}  // namespace GHIElectronics::Endpoint::Internal::Ma35dPins
