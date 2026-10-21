#include "UI.h"

#include <atomic>
#include <chrono>
#include <thread>

#include "lvgl.h"
#include "src/drivers/display/fb/lv_linux_fbdev.h"

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::UI {

namespace {

std::atomic<bool> g_initialized{false};
std::atomic<bool> g_running{false};

const auto g_start = std::chrono::steady_clock::now();

uint32_t TickNowMs() {
    using namespace std::chrono;
    return (uint32_t)duration_cast<milliseconds>(steady_clock::now() - g_start).count();
}

}  // namespace

void Initialize() {
    if (g_initialized.exchange(true)) return;

    lv_init();
    lv_tick_set_cb(TickNowMs);

    lv_display_t* disp = lv_linux_fbdev_create();
    if (!disp) {
        g_initialized = false;
        throw EndpointError(ErrorKind::IO, "lv_linux_fbdev_create failed");
    }
    lv_linux_fbdev_set_file(disp, "/dev/fb0");
}

void Run() {
    if (!g_initialized.load()) {
        throw EndpointError(ErrorKind::InvalidOperation, "UI::Initialize() must be called before Run()");
    }
    g_running = true;
    while (g_running.load()) {
        uint32_t sleep_ms = lv_timer_handler();
        if (sleep_ms > 50) sleep_ms = 50;
        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_ms));
    }
}

void Stop() {
    g_running = false;
}

}  // namespace GHIElectronics::Endpoint::Internal::UI
