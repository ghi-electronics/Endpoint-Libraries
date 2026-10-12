#pragma once

#include <string>

namespace GHIElectronics::Endpoint::Internal {

// The EPM815's pin numbers, the same as the .NET library: port * 16 + line,
// ports A (0) to I (8), then Z (9). PC0 = 32, PZ7 = 151.
namespace Pins {

constexpr int PortZ = 9;
constexpr int Last = PortZ * 16 + 7;  // PZ7

bool IsValid(int pin);

// The pin number, or an ArgumentOutOfRange error for anything else
int Check(int pin);

// "PC0", or "#200" for a number that is no pin
std::string Name(int pin);

// The chip's port (A = 0 ... I = 8, or Stm32Mp1Pins::PortZ) and line
struct ChipPin {
    int port;
    int line;
};
ChipPin ToChip(int pin);

}  // namespace Pins

}  // namespace GHIElectronics::Endpoint::Internal
