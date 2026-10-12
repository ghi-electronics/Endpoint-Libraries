#include "Errors.h"

#include <cerrno>
#include <cstring>

namespace GHIElectronics::Endpoint::Internal {

void ThrowArgument(const std::string& message) { throw EndpointError(ErrorKind::Argument, message); }

void ThrowArgumentOutOfRange(const std::string& message) { throw EndpointError(ErrorKind::ArgumentOutOfRange, message); }

void ThrowInvalidOperation(const std::string& message) { throw EndpointError(ErrorKind::InvalidOperation, message); }

void ThrowIO(const std::string& message) { throw EndpointError(ErrorKind::IO, message); }

void ThrowNotSupported(const std::string& message) { throw EndpointError(ErrorKind::NotSupported, message); }

void ThrowNotImplemented() { throw EndpointError(ErrorKind::NotImplemented, "Not implemented"); }

void ThrowTimeout() { throw EndpointError(ErrorKind::Timeout, "The operation has timed out."); }

void ThrowSystemError(const std::string& what) {
    auto error = errno;
    throw EndpointError(ErrorKind::IO, what + ": " + std::strerror(error));
}

}  // namespace GHIElectronics::Endpoint::Internal
