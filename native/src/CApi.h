#pragma once

// The hidden C layer of libendpoint. The Python package (ctypes) and the C++
// classes in endpoint.h call it; customers never use it directly, so it is
// not documented for them. Every function returns 0, or the ErrorKind of the
// error; endpoint_last_error() then gives its message.

#include <exception>
#include <string>

#include "Errors.h"

#define ENDPOINT_API extern "C" __attribute__((visibility("default")))

namespace GHIElectronics::Endpoint::Internal {

void SetLastError(const std::string& message);

// Runs action and turns an error into its ErrorKind and message
template <typename Action>
int Call(Action action) noexcept {
    try {
        action();
        return 0;
    }
    catch (const EndpointError& e) {
        SetLastError(e.what());
        return static_cast<int>(e.Kind());
    }
    catch (const std::exception& e) {
        SetLastError(e.what());
        return static_cast<int>(ErrorKind::IO);
    }
    catch (...) {
        SetLastError("Unknown error");
        return static_cast<int>(ErrorKind::IO);
    }
}

}  // namespace GHIElectronics::Endpoint::Internal
