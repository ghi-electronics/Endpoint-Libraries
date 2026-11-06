#pragma once

#include <nlohmann/json.hpp>

// Chip-specific display init, picked at build time by CMake:
//   endpoint-arm   preset -> src/DisplayStm32.cpp
//   endpoint-arm64 preset -> src/DisplayNuvoton.cpp
// Exactly one of the two is compiled per libendpoint.so, so neither chip's
// code can ever end up running on the other board.

namespace GHIElectronics::Endpoint::Internal::Display::Chip {

// Loads the kernel driver(s) for the panel described in /config/display.json.
// Called from Enable() after it has decided the display is "Parallel".
void ApplyPanel(const nlohmann::json& config);

// Reverses ApplyPanel. Called from Disable() after the backlight is off.
void UnloadPanel();

}  // namespace GHIElectronics::Endpoint::Internal::Display::Chip
