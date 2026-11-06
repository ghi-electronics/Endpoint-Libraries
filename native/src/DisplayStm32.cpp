// STM32MP15x (armv7) display driver glue.
//
// Unlike Nuvoton, the STM32 LTDC + panel-simple pair gets its panel timings
// as a module parameter. We build the ltdc_generic_setting= string from
// /config/display.json, modprobe stm_drm first (panel-simple does not pull
// it in as a dependency), then modprobe panel-simple with the param. The
// panel probes synchronously and /dev/fb0 appears.
//
// Mirrors the init sequence the firmware's endpoint-agent displayboot.cpp
// runs at boot when enableAtBoot=true, so Enable() behaves the same whether
// the boot-time applier or the user's program brings the panel up.

#include "DisplayChip.h"

#include <sys/wait.h>
#include <unistd.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <string>

namespace GHIElectronics::Endpoint::Internal::Display::Chip {

namespace {

int RunAndWait(const char* path, char* const argv[]) {
    pid_t child = fork();
    if (child == 0) {
        execv(path, argv);
        _exit(127);
    }
    if (child <= 0) return -1;
    int status = 0;
    while (waitpid(child, &status, 0) < 0 && errno == EINTR) {}
    if (WIFEXITED(status)) return WEXITSTATUS(status);
    return -1;
}

int JsonInt(const nlohmann::json& j, const char* key, int fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_number_integer()) ? it->get<int>() : fallback;
}
bool JsonBool(const nlohmann::json& j, const char* key, bool fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_boolean()) ? it->get<bool>() : fallback;
}
nlohmann::json JsonObject(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_object()) ? *it : nlohmann::json::object();
}

// Fixed ltdc_generic_setting values the GHI panel-simple patch reads.
constexpr int kDefaultNumModes      = 1;
constexpr int kDefaultDpiWidth      = 0;
constexpr int kDefaultDpiHeight     = 0;
constexpr int kDefaultBusFormat     = 0;
constexpr int kDefaultConnectorType = 0;
constexpr int kDefaultBpc           = 8;

// DRM bus_flags bit layout (include/drm/drm_connector.h). Always emit a bit
// for every axis — never leave a bit (or the whole field) at 0, because the
// GHI panel-simple patch treats bus_flags==0 as "unset, use defaults" and
// forces DE_HIGH | PIXDATA_NEGEDGE | SYNC_NEGEDGE regardless of what the
// user configured. That silently inverts ClockEdge.Rising to NEGEDGE and
// hides pixels on any panel that actually samples on the rising edge.
constexpr int kBusFlagDeLow                = 1 << 0;  // DE active-low
constexpr int kBusFlagDeHigh               = 1 << 1;  // DE active-high
constexpr int kBusFlagPixelDataPosEdge     = 1 << 2;  // sample pixel data on rising edge
constexpr int kBusFlagPixelDataNegEdge     = 1 << 3;  // sample pixel data on falling edge
constexpr int kBusFlagSyncSamplePosEdge    = 1 << 4;  // sample sync on rising edge
constexpr int kBusFlagSyncSampleNegEdge    = 1 << 5;  // sample sync on falling edge

}  // namespace

void ApplyPanel(const nlohmann::json& config) {
    auto parallel = JsonObject(config, "parallel");
    int width = JsonInt(parallel, "width", 0);
    int height = JsonInt(parallel, "height", 0);
    int pixelClockHz = JsonInt(parallel, "pixelClockHz", 0);
    if (width <= 0 || height <= 0 || pixelClockHz <= 0) return;

    int hfp   = JsonInt(parallel, "hFrontPorch", 0);
    int hbp   = JsonInt(parallel, "hBackPorch", 0);
    int hsync = JsonInt(parallel, "hSync", 0);
    int vfp   = JsonInt(parallel, "vFrontPorch", 0);
    int vbp   = JsonInt(parallel, "vBackPorch", 0);
    int vsync = JsonInt(parallel, "vSync", 0);
    bool deActiveLow       = JsonBool(parallel, "deActiveLow", false);
    bool pixelClockFalling = JsonBool(parallel, "pixelClockFalling", false);
    // Build bus_flags with explicit bits on every axis so the kernel never
    // sees 0 and falls back to its baked-in defaults. Sync-edge follows
    // pixelclock-edge: every panel we ship with has them paired.
    int busFlags = (deActiveLow       ? kBusFlagDeLow                : kBusFlagDeHigh)
                 | (pixelClockFalling ? kBusFlagPixelDataNegEdge     : kBusFlagPixelDataPosEdge)
                 | (pixelClockFalling ? kBusFlagSyncSampleNegEdge    : kBusFlagSyncSamplePosEdge);

    int clockKHz    = pixelClockHz / 1000;
    int hsync_start = width + hfp;
    int hsync_end   = hsync_start + hsync;
    int htotal      = hsync_end + hbp;
    int vsync_start = height + vfp;
    int vsync_end   = vsync_start + vsync;
    int vtotal      = vsync_end + vbp;

    char param[256];
    std::snprintf(param, sizeof param,
                  "ltdc_generic_setting=%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",
                  clockKHz, width, hsync_start, hsync_end, htotal,
                  height, vsync_start, vsync_end, vtotal,
                  kDefaultNumModes, kDefaultDpiWidth, kDefaultDpiHeight,
                  busFlags, kDefaultBusFormat,
                  kDefaultConnectorType, kDefaultBpc);

    // stm_drm first: panel-simple does not declare it as a dependency, so
    // without this /dev/fb0 never appears.
    char* ltdcArgv[] = {const_cast<char*>("modprobe"),
                        const_cast<char*>("stm_drm"),
                        nullptr};
    RunAndWait("/sbin/modprobe", ltdcArgv);

    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("panel-simple"),
                    param,
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
}

void UnloadPanel() {
    // panel-simple first (depends on stm_drm's crtc), then stm_drm. Retry on
    // EBUSY: closing /dev/fb0 in user code releases the last ref a few ms
    // later. Give up quietly after ~500 ms per module; the next Enable() will
    // reload and the stale instances will unload when their refcount drops.
    const char* mods[] = {"panel-simple", "stm_drm"};
    for (const char* m : mods) {
        char* argv[] = {const_cast<char*>("modprobe"),
                        const_cast<char*>("-r"),
                        const_cast<char*>(m),
                        nullptr};
        for (int i = 0; i < 10; ++i) {
            if (RunAndWait("/sbin/modprobe", argv) == 0) break;
            usleep(50 * 1000);
        }
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::Display::Chip
