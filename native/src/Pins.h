#pragma once

#include <string>

namespace GHIElectronics::Endpoint::Internal {

// The EPN256's pin numbers, the same as the .NET library: port * 16 + line,
// ports A (0) to N (13). PC0 = 32, PJ14 = 158, PN15 = 223.
namespace Pins {

constexpr int PortCount = 14;             // GPIOA ... GPION
constexpr int Last = PortCount * 16 - 1;  // PN15 = 223

// Kept as a compile-time constant for the STM32-era peripheral code
// (I2C/SPI/UART/CAN/ADC/DAC) that references it. GPIO on MA35D1 does not
// use PortZ: port 9 is just GPIOJ.
constexpr int PortZ = 9;

bool IsValid(int pin);

// The pin number, or an ArgumentOutOfRange error for anything else
int Check(int pin);

// "PC0", or "#200" for a number that is no pin
std::string Name(int pin);

// The chip's port (A = 0 ... N = 13) and line
struct ChipPin {
    int port;
    int line;
};
ChipPin ToChip(int pin);

}  // namespace Pins

}  // namespace GHIElectronics::Endpoint::Internal
