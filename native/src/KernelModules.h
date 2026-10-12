#pragma once

#include <string>

namespace GHIElectronics::Endpoint::Internal {

// Loads drivers that the firmware builds as modules: they cost nothing at
// boot and load the first time a program needs them
namespace KernelModules {

// Loads module (nothing happens if it is loaded already)
void Load(const std::string& module, const std::string& parameter = {});

// Waits until a driver that just loaded has made path (a file or folder)
void WaitFor(const std::string& path);

}  // namespace KernelModules

}  // namespace GHIElectronics::Endpoint::Internal
