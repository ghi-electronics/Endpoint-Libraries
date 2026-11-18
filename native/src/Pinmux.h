#pragma once

#include <cstdint>

namespace GHIElectronics::Endpoint::Internal {

// Fork+exec wrapper around /usr/sbin/endpoint-pinmux, the C helper that
// writes SoC pinmux registers via /dev/mem. One binary per chip ships in
// the Buildroot image; the chip-specific Pins helpers (Stm32Mp1Pins,
// Ma35dPins, ...) call these from their Save / Restore / SetFunction.
// Keeps chip-specific register knowledge in one place (the C binary) so
// .NET, Python, and C/C++ don't each re-implement it. Mirrors .NET's
// GHIElectronics.Endpoint.Core.Internal.Pinmux and Python's
// GHIElectronics.Endpoint.Core._board_support.Pinmux.
//
// `function` is a chip-specific packed uint32 - see
// Endpoint-Firmware/pinmux/src/pinmux.h for each chip's layout.
namespace Pinmux {

// Set the pin's function. Throws if the binary exits non-zero.
void Set(int pin, uint32_t function);

// Read the pin's current function (packed uint32, chip-specific layout).
uint32_t Get(int pin);

}  // namespace Pinmux

}  // namespace GHIElectronics::Endpoint::Internal
