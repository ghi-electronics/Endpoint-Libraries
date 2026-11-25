#include "EndpointDevices.h"

#include <cstdio>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <system_error>

namespace GHIElectronics::Endpoint::Internal::EndpointDevices {

namespace fs = std::filesystem;

namespace {

std::string Trim(std::string text) {
    auto end = text.find_last_not_of(" \t\r\n");
    return end == std::string::npos ? std::string() : text.substr(0, end + 1);
}

}  // namespace

bool IsAt(const std::string& cls, const std::string& name, uint64_t address) {
    std::error_code error;
    auto target = fs::canonical("/sys/class/" + cls + "/" + name, error);
    if (error) {
        return false;
    }
    // Most subsystems keep the platform device in the path, as in
    // /sys/devices/platform/40630000.spi/spi_master/spi3/spi3.0/spidev/spidev3.0
    char part[24];
    std::snprintf(part, sizeof part, "/%llx.", static_cast<unsigned long long>(address));
    if (target.string().find(part) != std::string::npos) {
        return true;
    }
    // The i2c core does not: an adapter lands at /sys/devices/i2c-1, whose
    // name carries the bus number instead of the address. The device tree
    // node is then the only authority, and it hangs off a parent folder.
    char suffix[24];
    std::snprintf(suffix, sizeof suffix, "@%llx", static_cast<unsigned long long>(address));
    for (auto folder = target; !folder.empty() && folder != folder.root_path();
         folder = folder.parent_path()) {
        auto node = fs::read_symlink(folder / "of_node", error);
        if (!error) {
            auto text = node.string();
            auto length = std::strlen(suffix);
            return text.size() >= length &&
                   text.compare(text.size() - length, length, suffix) == 0;
        }
    }
    return false;
}

int Find(const std::string& cls, const std::string& prefix, uint64_t address) {
    std::error_code error;
    for (const auto& entry : fs::directory_iterator("/sys/class/" + cls, error)) {
        auto name = entry.path().filename().string();
        if (name.rfind(prefix, 0) != 0 || name.size() == prefix.size()) {
            continue;
        }
        auto digits = name.substr(prefix.size());
        if (digits.find_first_not_of("0123456789") != std::string::npos) {
            continue;
        }
        if (IsAt(cls, name, address)) {
            return std::stoi(digits);
        }
    }
    return -1;
}

std::optional<std::string> FindIio(const std::string& name) {
    std::error_code error;
    for (const auto& entry : fs::directory_iterator("/sys/bus/iio/devices", error)) {
        if (entry.path().filename().string().rfind("iio:device", 0) != 0) {
            continue;
        }
        std::ifstream file(entry.path() / "name");
        std::string text;
        if (file && std::getline(file, text) && Trim(text) == name) {
            return entry.path().string();
        }
    }
    return std::nullopt;
}

}  // namespace GHIElectronics::Endpoint::Internal::EndpointDevices
