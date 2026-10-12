#include "KernelModules.h"

#include <chrono>
#include <filesystem>
#include <thread>

#include "Errors.h"
#include "SystemCommands.h"

namespace GHIElectronics::Endpoint::Internal::KernelModules {

void Load(const std::string& module, const std::string& parameter) {
    auto what = "Cannot load the " + module + " driver";
    if (parameter.empty()) {
        SystemCommands::Run("modprobe", what, {module});
    }
    else {
        SystemCommands::Run("modprobe", what, {module, parameter});
    }
}

void WaitFor(const std::string& path) {
    auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1);
    std::error_code error;
    while (!std::filesystem::exists(path, error)) {
        if (std::chrono::steady_clock::now() > deadline) {
            ThrowIO(path + " did not appear");
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::KernelModules
