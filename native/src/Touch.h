#pragma once

#include <cstdint>
#include <string>

// Touchscreen input. Opens a /dev/input/eventN device (built-in or USB
// touchscreen), reads the Linux multi-touch protocol (type B: slot-based),
// and dispatches point events on a per-device reader thread. Multi-touch
// is first class; single-touch simply means the Points array has one entry.

namespace GHIElectronics::Endpoint::Internal::Touch {

constexpr int MaxPoints = 10;

enum class EventType : int {
    Pressed = 0,   // first finger down (points went from 0 to >=1)
    Released = 1,  // last finger up   (points went from >=1 to 0)
    Moved = 2,     // any point moved, or a point was added/removed with >=1 remaining
};

struct Point {
    int id = -1;   // tracking ID (persistent per finger), -1 = unused slot
    int x = 0;
    int y = 0;
};

using Callback = void (*)(void* context, EventType event, int pointCount, const Point* points);

class TouchImpl {
public:
    TouchImpl(const std::string& deviceName, Callback callback, void* context);
    ~TouchImpl();
    TouchImpl(const TouchImpl&) = delete;
    TouchImpl& operator=(const TouchImpl&) = delete;

    int VendorId() const { return vendorId_; }
    int ProductId() const { return productId_; }
    bool Connected() const { return connected_; }
    int Width() const { return width_; }
    int Height() const { return height_; }

    // Fills out (ids, xs, ys) with up to max active points; returns the count.
    int CurrentPoints(int* ids, int* xs, int* ys, int max) const;

private:
    void Run();

    int fd_ = -1;
    int wakeFd_ = -1;
    std::string deviceName_;
    Callback callback_;
    void* context_;
    int vendorId_ = 0;
    int productId_ = 0;
    int width_ = 0;
    int height_ = 0;
    volatile bool connected_ = true;
    volatile bool stop_ = false;
    void* threadHandle_ = nullptr;
    mutable void* stateMutex_;  // actual type hidden to avoid pulling <mutex> into header
};

// Returns the /dev/input/eventN path of the first built-in (non-USB)
// touchscreen found, or an empty string if none is present.
std::string FindDefaultDevice();

}  // namespace GHIElectronics::Endpoint::Internal::Touch
