#include "UI.h"

#include <atomic>
#include <chrono>
#include <cstdio>
#include <fcntl.h>
#include <thread>
#include <unistd.h>
#include <vector>

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

// LV_USE_LOG = 1 in lv_conf.h; LVGL calls this for every warning or error
// (level filter is LV_LOG_LEVEL_WARN). Prefix so it is obvious where the
// line came from when it shows up alongside the customer's own output.
void LvglLogToStderr(lv_log_level_t level, const char* msg) {
    const char* tag = level >= LV_LOG_LEVEL_ERROR ? "error" : "warn";
    std::fprintf(stderr, "libendpoint-ui: LVGL %s: %s", tag, msg ? msg : "");
    std::fflush(stderr);
}

}  // namespace

void Initialize() {
    if (g_initialized.exchange(true)) return;

    HideFbconCursor();
    lv_log_register_print_cb(LvglLogToStderr);
    lv_init();
    lv_tick_set_cb(TickNowMs);

    lv_display_t* disp = lv_linux_fbdev_create();
    if (!disp) {
        g_initialized = false;
        throw EndpointError(ErrorKind::IO, "lv_linux_fbdev_create failed");
    }
    lv_linux_fbdev_set_file(disp, "/dev/fb0");
}

namespace {

// Slot held for the lifetime of the custom display. LVGL holds a stable
// pointer to the lv_display_t's user_data, which points here, so the C-side
// flush trampoline can reach the user's managed callback + context + the
// pre-allocated draw buffer. A single display per process matches the
// existing fbdev path, so we keep this as a singleton.
struct CustomDisplaySlot {
    void (*user_flush)(void* context, const uint8_t* pixels, int byte_count);
    void* user_context;
    std::vector<uint8_t> draw_buffer;
    int byte_count;
};

CustomDisplaySlot* g_custom_slot = nullptr;

void CustomFlushTrampoline(lv_display_t* disp, const lv_area_t* /*area*/, uint8_t* px_map) {
    auto* slot = static_cast<CustomDisplaySlot*>(lv_display_get_user_data(disp));
    if (slot && slot->user_flush) {
        slot->user_flush(slot->user_context, px_map, slot->byte_count);
    }
    lv_display_flush_ready(disp);
}

// Map the public PixelFormat enum (matches endpoint::PixelFormat in C#) to
// LVGL's color format + bits-per-pixel. Keep in sync with the C# enum values.
struct FormatInfo {
    lv_color_format_t color_format;
    int bits_per_pixel;
};
FormatInfo ResolveFormat(int format) {
    switch (format) {
        case 1: return {LV_COLOR_FORMAT_I1,       1};   // Monochrome (1 bpp, MSB-first)
        case 2: return {LV_COLOR_FORMAT_RGB565,  16};   // Rgb565
        case 3: return {LV_COLOR_FORMAT_RGB888,  24};   // Rgb888
        case 4: return {LV_COLOR_FORMAT_ARGB8888,32};   // Argb8888
        default:
            throw EndpointError(ErrorKind::Argument,
                "unsupported pixel format: use Monochrome, Rgb565, Rgb888 or Argb8888");
    }
}

}  // namespace

void InitializeCustom(int width, int height, int pixel_format,
                      void (*flush_cb)(void* context, const uint8_t* pixels, int byte_count),
                      void* context) {
    if (g_initialized.exchange(true)) return;
    if (width <= 0 || height <= 0) {
        g_initialized = false;
        throw EndpointError(ErrorKind::Argument, "width and height must be positive");
    }
    if (!flush_cb) {
        g_initialized = false;
        throw EndpointError(ErrorKind::Argument, "flush callback must not be null");
    }
    FormatInfo fmt = ResolveFormat(pixel_format);

    lv_log_register_print_cb(LvglLogToStderr);
    lv_init();
    lv_tick_set_cb(TickNowMs);

    lv_display_t* disp = lv_display_create(width, height);
    if (!disp) {
        g_initialized = false;
        throw EndpointError(ErrorKind::IO, "lv_display_create failed");
    }
    lv_display_set_color_format(disp, fmt.color_format);

    // Draw buffer sized to the whole frame; LV_DISPLAY_RENDER_MODE_DIRECT
    // means LVGL renders straight into it and calls flush once per frame
    // with the full buffer — simplest possible transport abstraction.
    size_t buf_bytes = (size_t)(width * height * fmt.bits_per_pixel + 7) / 8;
    g_custom_slot = new CustomDisplaySlot{};
    g_custom_slot->user_flush = flush_cb;
    g_custom_slot->user_context = context;
    g_custom_slot->draw_buffer.resize(buf_bytes);
    g_custom_slot->byte_count = (int)buf_bytes;

    lv_display_set_buffers(disp, g_custom_slot->draw_buffer.data(), nullptr,
                           (uint32_t)buf_bytes, LV_DISPLAY_RENDER_MODE_DIRECT);
    lv_display_set_user_data(disp, g_custom_slot);
    lv_display_set_flush_cb(disp, CustomFlushTrampoline);
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
