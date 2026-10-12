#include "CApi.h"
#include "endpoint/Version.h"

namespace GHIElectronics::Endpoint::Internal {

namespace {
thread_local std::string lastError;
}

void SetLastError(const std::string& message) { lastError = message; }

}  // namespace GHIElectronics::Endpoint::Internal

using namespace GHIElectronics::Endpoint::Internal;

// The message of the last error on this thread
ENDPOINT_API const char* endpoint_last_error() { return lastError.c_str(); }

// The library's version, for example "0.2.0" (endpoint/Version.h)
ENDPOINT_API const char* endpoint_version() { return ENDPOINT_VERSION; }
