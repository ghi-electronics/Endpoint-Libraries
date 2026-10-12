#pragma once

#include <string>
#include <vector>

namespace GHIElectronics::Endpoint::Internal {

// Runs the system's own tools (modprobe, ip...) where they are the standard
// way to do something: the arguments go straight to the tool, with no shell,
// and its error message becomes the error's message. The same as the .NET
// library's SystemCommands.
namespace SystemCommands {

// Runs program; an IO error "what: reason" if it fails or takes longer than
// timeoutMs
void Run(const std::string& program, const std::string& what, const std::vector<std::string>& arguments,
         int timeoutMs = 5000);

}  // namespace SystemCommands

}  // namespace GHIElectronics::Endpoint::Internal
