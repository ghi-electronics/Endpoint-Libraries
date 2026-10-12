#include "CApi.h"

namespace GHIElectronics::Endpoint::Internal {

namespace {
thread_local std::string lastError;
}

void SetLastError(const std::string& message) { lastError = message; }

}  // namespace GHIElectronics::Endpoint::Internal

using namespace GHIElectronics::Endpoint::Internal;

// The message of the last error on this thread
ENDPOINT_API const char* endpoint_last_error() { return lastError.c_str(); }

// The library's version, for example "0.1.0"
ENDPOINT_API const char* endpoint_version() { return "0.1.0"; }
