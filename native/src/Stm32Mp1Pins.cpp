#include "Stm32Mp1Pins.h"

#include "Errors.h"
#include "Pinmux.h"

namespace GHIElectronics::Endpoint::Internal::Stm32Mp1Pins {

namespace {

// Function packing. Keep in sync with Endpoint-Firmware/pinmux/src/stm32mp1.c:
//   [Mode:2][Type:1][Speed:2][Pull:2][AF:4]
constexpr int AfShift    = 0,  AfMask    = 0xF;
constexpr int PullShift  = 4,  PullMask  = 0x3;
constexpr int SpeedShift = 6,  SpeedMask = 0x3;
constexpr int TypeShift  = 8,  TypeMask  = 0x1;
constexpr int ModeShift  = 9,  ModeMask  = 0x3;

// Re-encode (port, line) back to the chip-neutral pin number the C
// binary expects. Z=25 -> 9 so the uint16 pin arg stays small.
int ToPin(int port, int line) {
    if (line < 0 || line > 15) {
        ThrowArgumentOutOfRange("Pins are 0 to 15 in a port");
    }
    if (port != PortZ && (port < 0 || port > 10)) {
        ThrowArgumentOutOfRange("Ports are A to K and Z");
    }
    return (port == PortZ ? 9 : port) * 16 + line;
}

uint32_t Pack(Mode mode, OutputType type, Speed speed, Pull pull, int af) {
    return ((static_cast<uint32_t>(mode)  & ModeMask)  << ModeShift)  |
           ((static_cast<uint32_t>(type)  & TypeMask)  << TypeShift)  |
           ((static_cast<uint32_t>(speed) & SpeedMask) << SpeedShift) |
           ((static_cast<uint32_t>(pull)  & PullMask)  << PullShift)  |
           ((static_cast<uint32_t>(af)    & AfMask)    << AfShift);
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
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), SpeedShift, SpeedMask, static_cast<uint32_t>(speed)));
}

void SetOutputType(int port, int line, OutputType type) {
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), TypeShift, TypeMask, static_cast<uint32_t>(type)));
}

void SetAlternate(int port, int line, int function) {
    if (function < 0 || function > 15) {
        ThrowArgumentOutOfRange("Alternate functions are 0 to 15");
    }
    auto pin = ToPin(port, line);
    Pinmux::Set(pin, Replace(Pinmux::Get(pin), AfShift, AfMask, static_cast<uint32_t>(function)));
}

State Save(int port, int line) {
    return State{Pinmux::Get(ToPin(port, line))};
}

void Restore(int port, int line, const State& state) {
    Pinmux::Set(ToPin(port, line), state.packed);
}

void SetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed) {
    if (function < 0 || function > 15) {
        ThrowArgumentOutOfRange("Alternate functions are 0 to 15");
    }
    Pinmux::Set(ToPin(port, line), Pack(Mode::Alternate, type, speed, pull, function));
}

PortLine FromPin(int pin) {
    auto port = pin / 16;
    return PortLine{port == 9 ? PortZ : port, pin % 16};
}

}  // namespace GHIElectronics::Endpoint::Internal::Stm32Mp1Pins
