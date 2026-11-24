// Nuvoton MA35D1 / MA35D0 (aarch64) display driver glue.
//
// The DCUltra controller takes its mode from whatever panel the device tree
// names, so panel timings reach the kernel the same way they do on STM32: as
// panel-simple module parameters. The Endpoint device tree points the panel
// node at "ghi,endpoint-parallel", a writable panel_desc whose mode is filled
// in from the endpoint_panel_setting array at insertion time. That is what
// lets a customer run their own screen size and timings from
// /config/display.json or from DisplayController.SetSettings.
//
// panel-simple goes in first so the DCUltra bridge resolves on its first
// probe instead of deferring, then ma35-drm. Both are modules
// (CONFIG_DRM_PANEL_SIMPLE=m, CONFIG_DRM_MA35=m) so a customer with no
// display pays zero boot time; this call is the moment the ~200 ms probe
// runs. Touch stays the customer's job (subclass of the Touch abstract class,
// driving their chip through Core.I2c + Core.Gpio): we ship no touch kernel
// driver.
//
// Mirrors the init sequence the firmware's endpoint-agent displayboot.cpp
// runs at boot when enableAtBoot=true, so Enable() behaves the same whether
// the boot-time applier or the user's program brings the panel up.

#include "DisplayChip.h"

#include <sys/wait.h>
#include <unistd.h>

#include <cerrno>
#include <cstddef>
#include <cstdio>

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

// Fixed endpoint_panel_setting values. Zero means "keep the driver's built-in
// default for that field", which is what we want for everything except the
// timings and the bus polarities.
constexpr int kDefaultNumModes      = 1;
constexpr int kDefaultDpiWidth      = 0;
constexpr int kDefaultDpiHeight     = 0;
constexpr int kDefaultBusFormat     = 0;
constexpr int kDefaultConnectorType = 0;
constexpr int kDefaultBpc           = 8;

// DRM bus_flags bit layout (include/drm/drm_connector.h). Always emit a bit
// for every axis, never leave a bit (or the whole field) at 0, because the
// generic panel treats bus_flags==0 as "unset, use defaults" and forces
// DE_HIGH | PIXDATA_POSEDGE regardless of what the user configured. That
// would silently invert ClockEdge and hide pixels on any panel that samples
// on the other edge.
constexpr int kBusFlagDeLow             = 1 << 0;  // DE active-low
constexpr int kBusFlagDeHigh            = 1 << 1;  // DE active-high
constexpr int kBusFlagPixelDataPosEdge  = 1 << 2;  // sample pixel data rising
constexpr int kBusFlagPixelDataNegEdge  = 1 << 3;  // sample pixel data falling
constexpr int kBusFlagSyncSamplePosEdge = 1 << 4;  // sample sync rising
constexpr int kBusFlagSyncSampleNegEdge = 1 << 5;  // sample sync falling

}  // namespace

void ApplyPanel(const nlohmann::json& config) {
    auto parallel = JsonObject(config, "parallel");
    int width = JsonInt(parallel, "width", 0);
    int height = JsonInt(parallel, "height", 0);
    int pixelClockHz = JsonInt(parallel, "pixelClockHz", 0);

    char param[256];
    bool haveTimings = width > 0 && height > 0 && pixelClockHz > 0;

    if (haveTimings) {
        int hfp   = JsonInt(parallel, "hFrontPorch", 0);
        int hbp   = JsonInt(parallel, "hBackPorch", 0);
        int hsync = JsonInt(parallel, "hSync", 0);
        int vfp   = JsonInt(parallel, "vFrontPorch", 0);
        int vbp   = JsonInt(parallel, "vBackPorch", 0);
        int vsync = JsonInt(parallel, "vSync", 0);
        bool deActiveLow       = JsonBool(parallel, "deActiveLow", false);
        bool pixelClockFalling = JsonBool(parallel, "pixelClockFalling", false);
        // Sync edge follows pixel clock edge: every panel we ship pairs them.
        int busFlags = (deActiveLow       ? kBusFlagDeLow            : kBusFlagDeHigh)
                     | (pixelClockFalling ? kBusFlagPixelDataNegEdge : kBusFlagPixelDataPosEdge)
                     | (pixelClockFalling ? kBusFlagSyncSampleNegEdge : kBusFlagSyncSamplePosEdge);

        int clockKHz    = pixelClockHz / 1000;
        int hsync_start = width + hfp;
        int hsync_end   = hsync_start + hsync;
        int htotal      = hsync_end + hbp;
        int vsync_start = height + vfp;
        int vsync_end   = vsync_start + vsync;
        int vtotal      = vsync_end + vbp;

        std::snprintf(param, sizeof param,
                      "endpoint_panel_setting=%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",
                      clockKHz, width, hsync_start, hsync_end, htotal,
                      height, vsync_start, vsync_end, vtotal,
                      kDefaultNumModes, kDefaultDpiWidth, kDefaultDpiHeight,
                      busFlags, kDefaultBusFormat,
                      kDefaultConnectorType, kDefaultBpc);
    }

    char* panelArgv[] = {const_cast<char*>("modprobe"),
                         const_cast<char*>("panel-simple"),
                         haveTimings ? param : nullptr,
                         nullptr};
    RunAndWait("/sbin/modprobe", panelArgv);

    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("ma35-drm"),
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
}

void UnloadPanel() {
    // ma35-drm first (it holds the bridge reference), then panel-simple.
    // Reclaims the 8 MiB display_buf CMA region and ends the DCUltra
    // pixel-clock, so Disable() is "pay-for-use off" and not just the
    // backlight.
    //
    // After the wrappers close /dev/fb0 the kernel's DRM teardown takes a
    // few ms to drop its last internal reference, so an immediate
    // `modprobe -r` fails with EBUSY. Retry a handful of times with a
    // short sleep so the common close-then-disable path finishes cleanly.
    // Give up quietly after ~500 ms per module; the next Enable() will
    // modprobe again and the stale instance will unload when its refcount
    // drops.
    //
    // panel-simple must come out too: its panel_desc keeps the timings from
    // the last insertion, so leaving it loaded would make a later
    // SetSettings + Enable() silently reuse the old mode.
    const char* mods[] = {"ma35-drm", "panel-simple"};
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
