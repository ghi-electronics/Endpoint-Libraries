#include "Display.h"

#include <csignal>
#include <sys/stat.h>
#include <sys/wait.h>
#include <unistd.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <string>

#include <nlohmann/json.hpp>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Display {

namespace {

constexpr const char* kConfigPath = "/config/display.json";
constexpr const char* kFramebufferPath = "/dev/fb0";

void RunAndWait(const char* path, char* const argv[]) {
    pid_t child = fork();
    if (child == 0) {
        execv(path, argv);
        _exit(127);
    }
    if (child > 0) {
        while (waitpid(child, nullptr, 0) < 0 && errno == EINTR) {}
    }
}

nlohmann::json ReadConfig() {
    try {
        std::ifstream file(kConfigPath);
        if (!file) {
            return nlohmann::json::object();
        }
        nlohmann::json config;
        file >> config;
        return config;
    } catch (...) {
        return nlohmann::json::object();
    }
}

std::string JsonString(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_string()) ? it->get<std::string>() : std::string{};
}
int JsonInt(const nlohmann::json& j, const char* key, int fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_number_integer()) ? it->get<int>() : fallback;
}
nlohmann::json JsonObject(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_object()) ? *it : nlohmann::json::object();
}

Type ParseType(const std::string& s) {
    if (s == "parallel") return Type::Parallel;
    return Type::None;
}

// Extract the numeric part of a chip name like "gpiochip7" -> 7. Accepts
// either "gpiochip7" or just "7".
int ParseGpioChipNumber(const std::string& name) {
    if (name.empty()) return -1;
    const char* digits = name.c_str();
    while (*digits && (*digits < '0' || *digits > '9')) ++digits;
    if (!*digits) return -1;
    return std::atoi(digits);
}

bool FramebufferExists() {
    struct stat st{};
    return stat(kFramebufferPath, &st) == 0;
}

// Static ltdc_generic_setting values the GHI panel driver (see
// 0004-drm-panel-simple-add-ghi-ltdc-generic-panel.patch) is known to work
// with across every panel the Endpoint-samples ship for. We only vary clock,
// timings and the two polarity bits of bus_flags that the LTDC driver
// actually reads (see drivers/gpu/drm/stm/ltdc.c:600-603 in the Linux tree).
constexpr int kDefaultNumModes      = 1;
constexpr int kDefaultDpiWidth      = 0;   // kernel fills in 152 (mm)
constexpr int kDefaultDpiHeight     = 0;   // kernel fills in 91  (mm)
constexpr int kDefaultBusFormat     = 0;   // kernel fills in RGB888_1X24
constexpr int kDefaultConnectorType = 0;   // kernel fills in DRM_MODE_CONNECTOR_DPI
constexpr int kDefaultBpc           = 8;   // kernel's own default

// Bit values the STM32 LTDC driver reads from bus_flags (from
// include/drm/drm_connector.h in kernel 5.13):
//   bit 0 (DRM_BUS_FLAG_DE_LOW):               DE active-low
//   bit 3 (DRM_BUS_FLAG_PIXDATA_DRIVE_NEGEDGE): pixel clock inverted
// Every other bit is ignored by the LTDC driver on STM32MP1.
constexpr int kBusFlagDeLow                 = 1 << 0;  // = 1
constexpr int kBusFlagPixelClockNegEdge     = 1 << 3;  // = 8

void ApplyParallelLcd(const nlohmann::json& section) {
    auto parallel = JsonObject(section, "parallel");
    int width = JsonInt(parallel, "width", 0);
    int height = JsonInt(parallel, "height", 0);
    int pixelClockHz = JsonInt(parallel, "pixelClockHz", 0);
    int hfp = JsonInt(parallel, "hFrontPorch", 0);
    int hbp = JsonInt(parallel, "hBackPorch", 0);
    int hsync = JsonInt(parallel, "hSync", 0);
    int vfp = JsonInt(parallel, "vFrontPorch", 0);
    int vbp = JsonInt(parallel, "vBackPorch", 0);
    int vsync = JsonInt(parallel, "vSync", 0);
    // The STM32 LTDC driver only looks at two bits of bus_flags, so we build
    // the value from the two user-facing fields instead of letting the user
    // guess hex magic numbers.
    bool deActiveLow      = JsonInt(parallel, "deActiveLow", 0) != 0;
    bool pixelClockFalling = JsonInt(parallel, "pixelClockFalling", 0) != 0;
    int busFlags = (deActiveLow ? kBusFlagDeLow : 0)
                 | (pixelClockFalling ? kBusFlagPixelClockNegEdge : 0);

    if (width <= 0 || height <= 0 || pixelClockHz <= 0) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            "parallel width/height/pixelClockHz missing in /config/display.json");
    }

    // panel-simple's ltdc_generic_setting takes 16 ints (see
    // 0004-drm-panel-simple-add-ghi-ltdc-generic-panel.patch):
    //   0: clock (kHz)                 8: vtotal
    //   1: hdisplay                    9: num_modes
    //   2: hsync_start                 10: size.width (mm)
    //   3: hsync_end                   11: size.height (mm)
    //   4: htotal                      12: bus_flags
    //   5: vdisplay                    13: bus_format
    //   6: vsync_start                 14: connector_type
    //   7: vsync_end                   15: bpc
    int clockKHz = pixelClockHz / 1000;
    int hsync_start = width + hfp;
    int hsync_end = hsync_start + hsync;
    int htotal = hsync_end + hbp;
    int vsync_start = height + vfp;
    int vsync_end = vsync_start + vsync;
    int vtotal = vsync_end + vbp;

    char param[256];
    std::snprintf(param, sizeof param,
                  "ltdc_generic_setting=%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",
                  clockKHz, width, hsync_start, hsync_end, htotal,
                  height, vsync_start, vsync_end, vtotal,
                  kDefaultNumModes, kDefaultDpiWidth, kDefaultDpiHeight,
                  busFlags, kDefaultBusFormat,
                  kDefaultConnectorType, kDefaultBpc);

    // Load the STM32 LTDC (display controller) driver FIRST. It is a module
    // now (CONFIG_DRM_STM=m) to keep boot time off GPIO-only customers, so
    // panel-simple has nothing to bind to until this is in. Without this,
    // the LTDC DT node stays unbound forever and /dev/fb0 never appears.
    char* ltdcArgv[] = {const_cast<char*>("modprobe"),
                        const_cast<char*>("stm_drm"),
                        nullptr};
    RunAndWait("/sbin/modprobe", ltdcArgv);

    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("panel-simple"),
                    param,
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
    // Touch is the customer's responsibility (subclass of the Touch
    // abstract class, driving their chip through Core.I2c + Core.Gpio).
    // We ship no touch kernel driver.
}

// libgpiod 1.x releases the line as soon as gpioset exits, which drops the
// backlight. We daemonise gpioset with `-m signal` so it holds the line until
// killed. The PID lives in /var/run so Disable (or the next Enable) can stop
// it cleanly without racing a new backlight-on request.
constexpr const char* kBacklightPidFile = "/var/run/endpoint-backlight.pid";

void StopHeldBacklight() {
    std::ifstream f(kBacklightPidFile);
    int pid = 0;
    f >> pid;
    if (pid > 0) {
        kill(pid, SIGTERM);
        for (int i = 0; i < 20 && waitpid(pid, nullptr, WNOHANG) == 0; ++i) {
            usleep(50 * 1000);
        }
    }
    std::remove(kBacklightPidFile);
}

void ApplyBacklight(const nlohmann::json& section, bool on) {
    auto bl = JsonObject(section, "backlight");
    std::string chip = JsonString(bl, "gpioChip");
    int chipNum = ParseGpioChipNumber(chip);
    int pin = JsonInt(bl, "gpioPin", -1);
    // A missing or incomplete backlight entry is NOT an error: the customer
    // may have an external backlight controller, or no backlight at all.
    if (chipNum < 0 || pin < 0) return;

    // "activeLow" string = ON when GPIO low. Default / "activeHigh" = ON high.
    bool activeLow = JsonString(bl, "activePolarity") == "activeLow";
    int onLevel = activeLow ? 0 : 1;
    int offLevel = activeLow ? 1 : 0;

    // Release any previously-held line so the new state wins.
    StopHeldBacklight();

    char chip_arg[32];
    std::snprintf(chip_arg, sizeof chip_arg, "gpiochip%d", chipNum);
    char line_arg[16];
    std::snprintf(line_arg, sizeof line_arg, "%d=%d", pin, on ? onLevel : offLevel);

    pid_t child = fork();
    if (child == 0) {
        // Detach from the parent so an exiting program does not take the
        // backlight down with it; -m signal makes gpioset hold the line
        // until it receives SIGTERM.
        setsid();
        execl("/usr/bin/gpioset", "gpioset",
              "-m", "signal", chip_arg, line_arg, nullptr);
        _exit(127);
    }
    if (child > 0) {
        std::ofstream pf(kBacklightPidFile);
        pf << child;
    }
}

void UnloadPanel() {
    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("-r"),
                    const_cast<char*>("panel-simple"),
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
}

}  // namespace

void Enable() {
    nlohmann::json config = ReadConfig();
    Type type = ParseType(JsonString(config, "type"));
    if (type == Type::None) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            "display type is 'none' in /config/display.json");
    }
    ApplyParallelLcd(config);
    ApplyBacklight(config, true);

    // modprobe + panel-simple probe is asynchronous: /dev/fb0 appears a
    // moment later. Block until it exists (up to 10 s) so the user does not
    // have to poll before their first Flush. Timeout -> throw so IsEnabled
    // reports the true state.
    for (int waited = 0; waited < 10000; waited += 50) {
        if (FramebufferExists()) return;
        usleep(50 * 1000);
    }
    throw EndpointError(ErrorKind::Timeout,
                        "panel driver loaded but /dev/fb0 did not appear within 10 s");
}

void Disable() {
    nlohmann::json config = ReadConfig();
    ApplyBacklight(config, false);
    UnloadPanel();
}

bool IsEnabled() {
    // The panel driver is "enabled" when the kernel has probed it and created
    // the framebuffer character device.
    return FramebufferExists();
}

}  // namespace GHIElectronics::Endpoint::Internal::Display
