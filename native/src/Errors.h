#pragma once

#include <stdexcept>
#include <string>

namespace GHIElectronics::Endpoint::Internal {

// The kinds of error the libraries report, the same as the .NET exceptions,
// so Python and C++ can raise the matching exception with the same message
enum class ErrorKind : int {
    None = 0,
    Argument = 1,            // ArgumentException
    ArgumentOutOfRange = 2,  // ArgumentOutOfRangeException
    InvalidOperation = 3,    // InvalidOperationException
    IO = 4,                  // IOException
    NotSupported = 5,        // NotSupportedException
    ObjectDisposed = 6,      // ObjectDisposedException
    NotImplemented = 7,      // NotImplementedException
    Timeout = 8,             // TimeoutException
};

class EndpointError : public std::runtime_error {
public:
    EndpointError(ErrorKind kind, const std::string& message) : std::runtime_error(message), kind_(kind) {}

    ErrorKind Kind() const noexcept { return kind_; }

private:
    ErrorKind kind_;
};

[[noreturn]] void ThrowArgument(const std::string& message);
[[noreturn]] void ThrowArgumentOutOfRange(const std::string& message);
[[noreturn]] void ThrowInvalidOperation(const std::string& message);
[[noreturn]] void ThrowIO(const std::string& message);
[[noreturn]] void ThrowNotSupported(const std::string& message);
[[noreturn]] void ThrowNotImplemented();
[[noreturn]] void ThrowTimeout();  // "The operation has timed out." (as .NET)

// An IO error with the reason of the last failed system call:
// "what: No such device"
[[noreturn]] void ThrowSystemError(const std::string& what);

}  // namespace GHIElectronics::Endpoint::Internal
