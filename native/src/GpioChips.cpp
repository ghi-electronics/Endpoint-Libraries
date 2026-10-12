#include "GpioChips.h"

#include <fcntl.h>
#include <linux/gpio.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <filesystem>
#include <map>
#include <mutex>
#include <optional>
#include <string>

#include "Errors.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal::GpioChips {

namespace {

std::mutex gate;
std::optional<std::map<std::string, int>> chips;

std::map<std::string, int> Scan() {
    std::map<std::string, int> found;
    std::error_code error;
    for (const auto& entry : std::filesystem::directory_iterator("/dev", error)) {
        auto name = entry.path().filename().string();
        if (name.rfind("gpiochip", 0) != 0 || name.size() == 8 ||
            name.find_first_not_of("0123456789", 8) != std::string::npos) {
            continue;
        }
        auto fd = open(entry.path().c_str(), O_RDONLY | O_CLOEXEC);
        if (fd < 0) {
            ThrowSystemError("Cannot open " + entry.path().string());
        }
        gpiochip_info info{};
        auto result = ioctl(fd, GPIO_GET_CHIPINFO_IOCTL, &info);
        close(fd);
        if (result < 0) {
            ThrowSystemError("Cannot read the label of " + entry.path().string());
        }
        found[info.label] = std::stoi(name.substr(8));
    }
    return found;
}

}  // namespace

int Find(int port) {
    auto label = std::string("GPIO") + (port == Pins::PortZ ? 'Z' : static_cast<char>('A' + port));
    std::lock_guard lock(gate);
    if (!chips) {
        chips = Scan();
    }
    auto found = chips->find(label);
    if (found == chips->end()) {
        ThrowInvalidOperation("The kernel has no " + label + " (no /dev/gpiochip with that label)");
    }
    return found->second;
}

}  // namespace GHIElectronics::Endpoint::Internal::GpioChips
