// Nuvoton MA35D1 / MA35D0 (aarch64) display driver glue.
//
// The panel timings, bus format, and DE / pixel-clock polarities are described
// in the device tree (ma35d1-som-256m.dts + Endpoint tweaks) and read by
// ma35-drm at probe. The /config/display.json "parallel" section's width /
// height / timing fields are IGNORED on this chip - the schema is cross-chip
// for forward compatibility but there is nothing to pass through as module
// params. Load the Nuvoton DCUltra driver and the panel comes up.
//
// ma35-drm is a module (CONFIG_DRM_MA35=m) so a customer with no display
// pays zero boot time; this call is the moment the ~200 ms probe runs. Touch
// stays the customer's job (subclass of the Touch abstract class, driving
// their chip through Core.I2c + Core.Gpio) - we ship no touch kernel driver.

#include "DisplayChip.h"

#include <sys/wait.h>
#include <unistd.h>

#include <cerrno>
#include <cstddef>

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

}  // namespace

void ApplyPanel(const nlohmann::json& /*config*/) {
    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("ma35-drm"),
                    nullptr};
    RunAndWait("/sbin/modprobe", argv);
}

void UnloadPanel() {
    // modprobe -r ma35-drm. Reclaims the 8 MiB display_buf CMA region and
    // ends the DCUltra pixel-clock, so Disable() is "pay-for-use off" and
    // not just the backlight.
    //
    // After the wrappers close /dev/fb0 the kernel's DRM teardown takes a
    // few ms to drop its last internal reference, so an immediate
    // `modprobe -r` fails with EBUSY. Retry a handful of times with a
    // short sleep so the common close-then-disable path finishes cleanly.
    // Give up quietly after ~500 ms; the next Enable() will modprobe again
    // and the stale instance will unload when its refcount drops.
    char* argv[] = {const_cast<char*>("modprobe"),
                    const_cast<char*>("-r"),
                    const_cast<char*>("ma35-drm"),
                    nullptr};
    for (int i = 0; i < 10; ++i) {
        if (RunAndWait("/sbin/modprobe", argv) == 0) return;
        usleep(50 * 1000);
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::Display::Chip
