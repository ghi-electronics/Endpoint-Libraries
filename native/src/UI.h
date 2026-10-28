// Internal UI subsystem lifecycle (LVGL init + Linux framebuffer binding +
// event-loop pump). Hidden from customers; endpoint/UI.h and the C API layer
// (UICApi.cpp) are the only callers.

#pragma once

#include <cstdint>

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

// Registers an LVGL pointer input device backed by TouchFeed(). Idempotent.
void TouchAttach();

// Pushes the latest touch point into the atomic state the pointer indev
// reads. Safe from any thread (touch chip drivers run on their own thread).
void TouchFeed(int x, int y, bool pressed);

// Thread-safe way to run a callback on the UI thread. The callback fires
// every `period_ms` milliseconds from inside lv_timer_handler, so widget
// calls inside it are safe even though LVGL has LV_USE_OS=LV_OS_NONE.
// Must be called AFTER Initialize(). Returns an opaque handle; pass it to
// TimerDelete to cancel. TimerCreate / TimerDelete / TimerSetPeriod are
// safe from any thread.
void* TimerCreate(uint32_t period_ms, void (*cb)(void* context), void* context);
void TimerSetPeriod(void* timer, uint32_t period_ms);
void TimerDelete(void* timer);

}  // namespace GHIElectronics::Endpoint::Internal::UI
