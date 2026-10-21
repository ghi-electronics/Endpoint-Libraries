// Internal UI subsystem lifecycle (LVGL init + Linux framebuffer binding +
// event-loop pump). Hidden from customers; endpoint/UI.h and the C API layer
// (UICApi.cpp) are the only callers.

#pragma once

namespace GHIElectronics::Endpoint::Internal::UI {

// Boots LVGL and binds it to /dev/fb0. Safe to call multiple times
// (second and later calls are no-ops). Throws EndpointError on failure.
void Initialize();

// Pumps lv_timer_handler in a loop until Stop() is called. Blocks the
// calling thread. Sleeps between frames based on lv_timer_handler's hint,
// capped at 50 ms.
void Run();

// Flips the Run() loop's exit flag. Safe to call from signal handlers.
void Stop();

}  // namespace GHIElectronics::Endpoint::Internal::UI
