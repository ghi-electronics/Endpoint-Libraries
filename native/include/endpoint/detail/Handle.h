// An owned libendpoint handle: moved, never copied, closed once
#pragma once

#include <utility>

#include "../Core.h"

namespace GHIElectronics::Endpoint::Core::detail {

template <int (*Close)(void*)>
class Handle {
public:
    Handle() = default;
    explicit Handle(void* handle) : handle_(handle) {}
    ~Handle() { Reset(); }

    Handle(Handle&& other) noexcept : handle_(std::exchange(other.handle_, nullptr)) {}
    Handle& operator=(Handle&& other) noexcept {
        if (this != &other) {
            Reset();
            handle_ = std::exchange(other.handle_, nullptr);
        }
        return *this;
    }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;

    // The handle, or an ObjectDisposedException("The <name> is disposed")
    void* Get(const char* name) const {
        if (handle_ == nullptr) {
            throw ObjectDisposedException(std::string("The ") + name + " is disposed");
        }
        return handle_;
    }

    void Reset() {
        if (handle_ != nullptr) {
            Close(handle_);
            handle_ = nullptr;
        }
    }

private:
    void* handle_ = nullptr;
};

}  // namespace GHIElectronics::Endpoint::Core::detail
