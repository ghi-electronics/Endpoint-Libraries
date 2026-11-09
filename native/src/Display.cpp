#include "Display.h"

#include <csignal>
#include <sys/stat.h>
#include <sys/wait.h>
#include <unistd.h>

#include <fcntl.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <string>

#include <nlohmann/json.hpp>

#include "DisplayChip.h"
#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Display {

namespace {

constexpr const char* kConfigPath = "/config/display.json";
constexpr const char* kFramebufferPath = "/dev/fb0";

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

// Hide the Linux framebuffer-console text cursor (the small block in the
// top-left of fb0 that the kernel's text terminal draws there).
//   1. Write "0" to /sys/class/graphics/fbcon/cursor_blink so the cursor
//      stops blinking.
//   2. Send the ANSI "hide cursor" escape ("\e[?25l") to /dev/tty0 so fbcon
//      removes the cursor glyph entirely on the next refresh.
// We cannot use KDSETMODE(KD_GRAPHICS) on this family: the DRM panel driver
// treats it as "stop driving the panel" and blanks scanout, so direct writes
// to /dev/fb0 go to a dark screen. Silent on error: kernels without fbcon
// or /dev/tty0 need no action.
void HideFbconCursor() {
    int cb = open("/sys/class/graphics/fbcon/cursor_blink", O_WRONLY);
    if (cb >= 0) {
        (void)!write(cb, "0", 1);
        close(cb);
    }
    int tty = open("/dev/tty0", O_WRONLY | O_NOCTTY);
    if (tty >= 0) {
        static const char kHideCursor[] = "\x1b[?25l";
        (void)!write(tty, kHideCursor, sizeof(kHideCursor) - 1);
        close(tty);
    }
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
    // New schema: "chip" + "line" are the ints the .NET WriteBacklight
    // produces from the packed BacklightSettings.Pin. Legacy schema (0.3.x
    // and earlier): "gpioChip" (string "gpiochipN") + "gpioPin" (line) -
    // read those as fallback so an existing /config/display.json keeps
    // working until the user's next SetSettings call rewrites it.
    int chipNum = JsonInt(bl, "chip", -1);
    int pin = JsonInt(bl, "line", -1);
    if (chipNum < 0 || pin < 0) {
        std::string legacyChip = JsonString(bl, "gpioChip");
        chipNum = ParseGpioChipNumber(legacyChip);
        pin = JsonInt(bl, "gpioPin", -1);
    }
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

}  // namespace

void Enable() {
    nlohmann::json config = ReadConfig();
    Type type = ParseType(JsonString(config, "type"));
    if (type == Type::None) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            "display type is 'none' in /config/display.json");
    }
    Chip::ApplyPanel(config);
    ApplyBacklight(config, true);

    // modprobe + panel probe is asynchronous: /dev/fb0 appears a moment
    // later. Block until it exists (up to 10 s) so the user does not have
    // to poll before their first Flush. Timeout -> throw so IsEnabled
    // reports the true state.
    for (int waited = 0; waited < 10000; waited += 50) {
        if (FramebufferExists()) {
            HideFbconCursor();
            return;
        }
        usleep(50 * 1000);
    }
    throw EndpointError(ErrorKind::Timeout,
                        "panel driver loaded but /dev/fb0 did not appear within 10 s");
}

void Disable() {
    nlohmann::json config = ReadConfig();
    ApplyBacklight(config, false);
    Chip::UnloadPanel();
}

bool IsEnabled() {
    // The panel driver is "enabled" when the kernel has probed it and created
    // the framebuffer character device.
    return FramebufferExists();
}

}  // namespace GHIElectronics::Endpoint::Internal::Display
