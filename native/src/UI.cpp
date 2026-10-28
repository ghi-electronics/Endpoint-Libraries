#include "UI.h"

#include <atomic>
#include <chrono>
#include <fcntl.h>
#include <thread>
#include <unistd.h>

#include "lvgl.h"
#include "src/drivers/display/fb/lv_linux_fbdev.h"

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::UI {

namespace {

std::atomic<bool> g_initialized{false};
std::atomic<bool> g_running{false};

// Touch state. The read_cb snapshots these on each LVGL tick; the touch
// chip's own event thread writes them via TouchFeed(). lv_indev_t is set
// up once by TouchAttach().
std::atomic<int32_t> g_touch_x{0};
std::atomic<int32_t> g_touch_y{0};
std::atomic<bool>    g_touch_pressed{false};
lv_indev_t*          g_touch_indev = nullptr;

const auto g_start = std::chrono::steady_clock::now();

uint32_t TickNowMs() {
    using namespace std::chrono;
    return (uint32_t)duration_cast<milliseconds>(steady_clock::now() - g_start).count();
}

// Hide the Linux framebuffer console cursor (the small black block in the
// top-left corner from the kernel's text terminal that also lives on
// /dev/fb0). Silent on error — the sysfs node is only present when fbcon is
// enabled in the kernel; absence just means no cursor to hide.
void HideFbconCursor() {
    int fd = open("/sys/class/graphics/fbcon/cursor_blink", O_WRONLY);
    if (fd >= 0) {
        (void)!write(fd, "0", 1);
        close(fd);
    }
}

}  // namespace

void Initialize() {
    if (g_initialized.exchange(true)) return;

    HideFbconCursor();
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

namespace {
void TouchReadCb(lv_indev_t*, lv_indev_data_t* data) {
    data->point.x = g_touch_x.load();
    data->point.y = g_touch_y.load();
    data->state   = g_touch_pressed.load() ? LV_INDEV_STATE_PRESSED : LV_INDEV_STATE_RELEASED;
}
}

void TouchAttach() {
    if (g_touch_indev) return;
    g_touch_indev = lv_indev_create();
    lv_indev_set_type(g_touch_indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(g_touch_indev, TouchReadCb);
}

void TouchFeed(int x, int y, bool pressed) {
    g_touch_x = x;
    g_touch_y = y;
    g_touch_pressed = pressed;
}

namespace {
// The trampoline the LVGL timer calls. We stash the user's callback and
// context in a struct allocated at TimerCreate time, hand lv_timer that
// struct as the user data, and free the struct in TimerDelete so stale
// callbacks never fire after cancellation.
struct TimerSlot {
    void (*callback)(void*);
    void* context;
};

void TimerTrampoline(lv_timer_t* timer) {
    auto* slot = static_cast<TimerSlot*>(lv_timer_get_user_data(timer));
    if (slot && slot->callback) slot->callback(slot->context);
}
}

void* TimerCreate(uint32_t period_ms, void (*cb)(void*), void* context) {
    if (!g_initialized.load()) {
        throw EndpointError(ErrorKind::InvalidOperation, "UI::Initialize() must be called before TimerCreate()");
    }
    if (!cb) return nullptr;
    auto* slot = new TimerSlot{cb, context};
    lv_timer_t* timer = lv_timer_create(TimerTrampoline, period_ms, slot);
    if (!timer) {
        delete slot;
        throw EndpointError(ErrorKind::IO, "lv_timer_create failed");
    }
    return timer;
}

void TimerSetPeriod(void* timer, uint32_t period_ms) {
    if (!timer) return;
    lv_timer_set_period(static_cast<lv_timer_t*>(timer), period_ms);
}

void TimerDelete(void* timer) {
    if (!timer) return;
    auto* t = static_cast<lv_timer_t*>(timer);
    auto* slot = static_cast<TimerSlot*>(lv_timer_get_user_data(t));
    lv_timer_delete(t);
    delete slot;
}

}  // namespace GHIElectronics::Endpoint::Internal::UI
