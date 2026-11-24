#pragma once

#include <cstdint>

#include "Stm32Mp1Pins.h"

namespace GHIElectronics::Endpoint::Internal {

// Pin setup of the Nuvoton MA35D1 / MA35D0 (multi-function pin selection,
// mode, pull, slew). All reads and writes go through /usr/sbin/endpoint-pinmux,
// the C helper that writes SoC pinmux registers via /dev/mem. No register map
// lives here. See Endpoint-Firmware/pinmux/src/ma35d.c for the authoritative
// implementation. Mirrors .NET's Pins.Ma35dPins.
//
// A pin is a port (0 = A ... 13 = N) and a line (0 to 15); the chip-neutral
// pin number is port * 16 + line.
//
// Semantic differences from STM32, kept behind the same shape as
// Stm32Mp1Pins so PeripheralPins can treat both the same way:
// - MFP (0..15) is the alternate function; 0 = plain GPIO.
// - Mode has four values: Input / Output / OpenDrain / Quasi. Open drain is a
//   Mode value (MA35 has no separate output-type register).
// - Slew has three steps; the chip-neutral Speed maps Low->0, Medium/High->1,
//   VeryHigh->2 (lossy).
// - No analog mode: ADC pins select via MFP like any peripheral function.
namespace Ma35dPins {

// The chip-neutral Pull / OutputType / Speed enums are shared with
// Stm32Mp1Pins so PinFunction stays one type across chips.
using Stm32Mp1Pins::OutputType;
using Stm32Mp1Pins::Pull;
using Stm32Mp1Pins::Speed;

enum class Mode : int { Input = 0, Output = 1, OpenDrain = 2, Quasi = 3 };

// Opaque snapshot of a pin's full register state; pass back to Restore.
// The packing matches ma35d.c: [Slew:2][Pull:2][Mode:2][MFP:4].
struct State {
    uint32_t packed;

    bool operator==(const State&) const = default;

    Speed GetSlew() const { return static_cast<Speed>((packed >> 8) & 0x3); }
    Pull  GetPull() const { return static_cast<Pull>((packed >> 6) & 0x3); }
    Mode  GetMode() const { return static_cast<Mode>((packed >> 4) & 0x3); }
    int   GetAlternate() const { return static_cast<int>(packed & 0xF); }
};

// Last port bonded out on the chip (A = 0 ... N = 13).
constexpr int MaxPort = 13;

struct PortLine { int port; int line; };
PortLine FromPin(int pin);

void SetMode(int port, int line, Mode mode);
void SetPull(int port, int line, Pull pull);
void SetSpeed(int port, int line, Speed speed);

// On MA35 output type folds into Mode: OpenDrain -> Mode::OpenDrain,
// PushPull -> Mode::Output.
void SetOutputType(int port, int line, OutputType type);

void SetAlternate(int port, int line, int function);

// Snapshot the pin's full setup so Restore can put it back.
State Save(int port, int line);

void Restore(int port, int line, const State& state);

// Set the pin as a peripheral's pin: MFP + pull + slew + mode in one write.
// `type` is accepted for signature parity with Stm32Mp1Pins::SetFunction; the
// peripheral owns the pin once MFP is set, so it does not apply here.
void SetFunction(int port, int line, int function, Pull pull = Pull::None,
                 OutputType type = OutputType::PushPull, Speed speed = Speed::Low);

}  // namespace Ma35dPins

}  // namespace GHIElectronics::Endpoint::Internal
