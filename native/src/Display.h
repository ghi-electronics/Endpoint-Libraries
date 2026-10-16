#pragma once

// DisplayController in libendpoint. The C layer drives one concrete transport
// (the SoC's parallel RGB panel via /dev/fb0). Enable() reads
// /config/display.json fresh, loads the panel driver (panel-simple with the
// ltdc_generic_setting module parameter) and raises the backlight GPIO.
// Disable() lowers backlight and unloads. Runs synchronously in the caller's
// own program.
//
// Custom SPI/I2C panels are the customer's own code: they subclass the
// DisplayController abstract class in each language and drive the hardware
// through the Core Spi/I2c/Gpio APIs. The library intentionally knows nothing
// about chip-specific init sequences.

namespace GHIElectronics::Endpoint::Internal::Display {

enum class Type : int {
    None = 0,
    Parallel = 1,
};

// Reads /config/display.json and applies it. Throws EndpointError if type is
// None, if the driver fails to load, or if a backlight is configured but the
// GPIO cannot be driven.
void Enable();

// Lowers backlight and unloads the panel driver. Safe to call when nothing
// is enabled (no-op).
void Disable();

// True if a framebuffer currently exists for a loaded panel driver.
bool IsEnabled();

}  // namespace GHIElectronics::Endpoint::Internal::Display
