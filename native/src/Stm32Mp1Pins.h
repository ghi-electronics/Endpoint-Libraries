#pragma once

#include <cstdint>

namespace GHIElectronics::Endpoint::Internal {

// Pin setup of the STM32MP1 (mode, alternate function, pull, speed, output
// type). All reads and writes go through /usr/sbin/endpoint-pinmux, the C
// helper that writes SoC pinmux registers via /dev/mem. No register map
// lives here. See Endpoint-Firmware/pinmux/src/stm32mp1.c for the
// authoritative implementation. Mirrors .NET's Pins.Stm32Mp1Pins.
//
// A pin is a port (0 = A ... 10 = K, or PortZ) and a line (0 to 15). The
// chip-neutral pin number is port * 16 + line, with Z folded onto 9
// (so uint16 stays small on the wire).
namespace Stm32Mp1Pins {

enum class Mode : int { Input = 0, Output = 1, Alternate = 2, Analog = 3 };
enum class Pull : int { None = 0, Up = 1, Down = 2 };
enum class Speed : int { Low = 0, Medium = 1, High = 2, VeryHigh = 3 };
enum class OutputType : int { PushPull = 0, OpenDrain = 1 };

// Opaque snapshot of a pin's full register state; pass back to Restore.
// The chip-specific uint32 packing matches
// Endpoint-Firmware/pinmux/src/stm32mp1.c: [Mode:2][Type:1][Speed:2][Pull:2][AF:4].
struct State {
    uint32_t packed;

    bool operator==(const State&) const = default;

    Mode       GetMode()       const { return static_cast<Mode>((packed >> 9) & 0x3); }
    OutputType GetOutputType() const { return static_cast<OutputType>((packed >> 8) & 0x1); }
    Speed      GetSpeed()      const { return static_cast<Speed>((packed >> 6) & 0x3); }
    Pull       GetPull()       const { return static_cast<Pull>((packed >> 4) & 0x3); }
    int        GetAlternate()  const { return static_cast<int>(packed & 0xF); }
};

// Port Z: separate bus in silicon, identified as port 25.
constexpr int PortZ = 25;

// Decodes a packed pin number (port * 16 + line) into the (port, line)
// pair this helper expects. The library's Pins:: namespace numbers Z as 9
// (packed right after I = 8); Stm32Mp1Pins identifies Z with PortZ (= 25)
// because its clock and MMIO live on a different bus.
struct PortLine { int port; int line; };
PortLine FromPin(int pin);

void SetMode(int port, int line, Mode mode);
void SetPull(int port, int line, Pull pull);
void SetSpeed(int port, int line, Speed speed);
void SetOutputType(int port, int line, OutputType type);
void SetAlternate(int port, int line, int function);

// Snapshot the pin's full setup so Restore can put it back.
State Save(int port, int line);

// Restore the pin to the state captured by Save.
void Restore(int port, int line, const State& state);

// Set the pin as a peripheral's pin: alternate function, pull, speed,
// type, mode all at once (one write).
void SetFunction(int port, int line, int function, Pull pull = Pull::None,
                 OutputType type = OutputType::PushPull, Speed speed = Speed::Low);

}  // namespace Stm32Mp1Pins

}  // namespace GHIElectronics::Endpoint::Internal
