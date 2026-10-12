#pragma once

#include <cstdint>

namespace GHIElectronics::Endpoint::Internal {

// Pin setup of the STM32MP1 (mode, alternate function, pull, speed, output
// type), written to the GPIO registers. Products built on this chip (EPM815)
// use it; another chip gets its own. A pin is a port (0 = A ... 10 = K, or
// PortZ) and a line (0 to 15). The firmware keeps the port clocks on (kernel
// patch "pinctrl: stm32: keep the GPIO bank clocks on"): without it, reads
// return 0 and writes are lost. The same as the .NET library's Stm32Mp1Pins.
namespace Stm32Mp1Pins {

enum class Mode : int { Input = 0, Output = 1, Alternate = 2, Analog = 3 };
enum class Pull : int { None = 0, Up = 1, Down = 2 };
enum class Speed : int { Low = 0, Medium = 1, High = 2, VeryHigh = 3 };
enum class OutputType : int { PushPull = 0, OpenDrain = 1 };

// A pin's whole setup, to put it back later
struct State {
    Mode mode;
    int alternate;
    Pull pull;
    OutputType type;
    Speed speed;

    bool operator==(const State&) const = default;
};

// Port Z: its registers and clock are separate from ports A to K
constexpr int PortZ = 25;

void SetMode(int port, int line, Mode mode);
void SetPull(int port, int line, Pull pull);
void SetSpeed(int port, int line, Speed speed);
void SetOutputType(int port, int line, OutputType type);
void SetAlternate(int port, int line, int function);

State Save(int port, int line);

// Puts the setup back, the mode last (no passing through another mode)
void Restore(int port, int line, const State& state);

// The pin as a peripheral's pin: alternate function first, then the mode
void SetFunction(int port, int line, int function, Pull pull = Pull::None,
                 OutputType type = OutputType::PushPull, Speed speed = Speed::Low);

}  // namespace Stm32Mp1Pins

}  // namespace GHIElectronics::Endpoint::Internal
