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

// Runs <path> with <argv> and returns the child's exit status (-1 on fork or
// wait failure). Caller interprets non-zero to decide retry policy.
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

void ApplyParallelLcd(const nlohmann::json& /*section*/) {
    // Nuvoton MA35D1 DCUltra. The panel timings, bus format, and DE / pixel-
    // clock polarities are described in the device tree (ma35d1-som-256m.dts +
    // our Endpoint DTS tweaks) and read by ma35-drm at probe. The /config/
    // display.json "parallel" section's width / height / timing fields are
    // IGNORED on MA35D1 — we keep the schema cross-chip for forward
    // compatibility but there is nothing to pass through as module params on
    // this chip. STM32MP1 used to need panel-simple + ltdc_generic_setting
    // here; that path is gone with the 0.4.x switch to Nuvoton-only.
    //
    // Load the Nuvoton DCUltra driver. It is a module (CONFIG_DRM_MA35=m) so
    // a customer with no display pays zero boot time; this call is the
    // moment the ~200 ms probe runs. Touch stays the customer's job
    // (subclass of the Touch abstract class, driving their chip through
    // Core.I2c + Core.Gpio) — we ship no touch kernel driver.
    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("ma35-drm"),
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
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
    // modprobe -r ma35-drm. Reclaims the 8 MiB display_buf CMA region and
    // ends the DCUltra pixel-clock, so a Disable() call really is "pay-for-
    // use off" and not just the backlight.
    //
    // After the C# / Python / C++ wrappers close their /dev/fb0 FileStream the
    // kernel's DRM teardown takes a few ms to drop its last internal reference,
    // so an immediate `modprobe -r` fails with EBUSY ("Resource temporarily
    // unavailable"). Retry a handful of times with a short sleep so the common
    // close-then-disable path finishes cleanly. Give up quietly after ~500 ms;
    // the next Enable() will modprobe again and the stale instance will unload
    // when its refcount drops.
    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("-r"),
                    const_cast<char*>("ma35-drm"),
                    nullptr};
    for (int i = 0; i < 10; ++i) {
        if (RunAndWait("/sbin/modprobe", argv) == 0) return;
        usleep(50 * 1000);
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
