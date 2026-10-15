// Touchscreens on Endpoint, built-in (configured in display.json) or USB
// (hot-plugged, discovered via UsbHostController). The same class covers both:
// one Touch is one /dev/input/eventN device. Multi-touch is first class;
// single-touch simply means the Points vector in each event has one entry.
//
//   using namespace GHIElectronics::Endpoint::Devices::UsbHost;
//
//   auto touch = Touch::GetDefault();   // built-in from display.json; nullopt if none
//   touch->OnEvent += [](Touch& sender, const TouchEventArgs& e) {
//       for (const auto& p : e.Points())
//           std::cout << p.Id << ": " << p.X << ',' << p.Y << '\n';
//   };
//
// For a USB touchscreen, construct from the UsbHostController event's DeviceName.

#pragma once

#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "Core.h"
#include "detail/CApi.h"
#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::UsbHost {

enum class TouchEventType : int {
    Pressed = 0,
    Released = 1,
    Moved = 2,
};

struct TouchPoint {
    int Id = -1;  // tracking ID, stable per finger across moves
    int X = 0;
    int Y = 0;
};

class TouchEventArgs {
public:
    TouchEventArgs(TouchEventType type, std::vector<TouchPoint> points)
        : type_(type), points_(std::move(points)) {}
    TouchEventType EventType() const { return type_; }
    const std::vector<TouchPoint>& Points() const { return points_; }
private:
    TouchEventType type_;
    std::vector<TouchPoint> points_;
};

class Touch {
public:
    // Explicit path (power user). Also used by callers who open a USB touchscreen
    // reported by UsbHostController (DeviceType::Touch) with its DeviceName.
    explicit Touch(const std::string& deviceName)
        : self_(std::make_unique<Self>()) {
        self_->owner = this;
        void* h = nullptr;
        Core::detail::Check(endpoint_touch_open(deviceName.c_str(), &Trampoline, self_.get(), &h));
        handle_ = Core::detail::Handle<endpoint_touch_close>(h);
        int connected = 0;
        Core::detail::Check(endpoint_touch_info(handle_.Get("Touch"), &vendorId_, &productId_,
                                                &connected, &width_, &height_));
    }

    // Built-in touch (configured in display.json). Returns nullopt if none wired up.
    static std::optional<Touch> GetDefault() {
        char buf[64] = {};
        Core::detail::Check(endpoint_touch_find_default(buf, sizeof buf));
        if (buf[0] == '\0') {
            return std::nullopt;
        }
        return Touch(std::string(buf));
    }

    int VendorId() const { return vendorId_; }
    int ProductId() const { return productId_; }
    int Width() const { return width_; }
    int Height() const { return height_; }

    // One event handler; callers chain their own dispatch if they need multiple.
    using Handler = std::function<void(Touch&, const TouchEventArgs&)>;
    void SetOnEvent(Handler handler) { self_->handler = std::move(handler); }

private:
    struct Self {
        Touch* owner = nullptr;
        Handler handler;
    };

    static void Trampoline(void* context, int eventType, int count,
                           const int* ids, const int* xs, const int* ys) {
        auto* self = static_cast<Self*>(context);
        if (!self->handler) {
            return;
        }
        std::vector<TouchPoint> points;
        points.reserve(count);
        for (int i = 0; i < count; ++i) {
            points.push_back({ids[i], xs[i], ys[i]});
        }
        self->handler(*self->owner, TouchEventArgs(static_cast<TouchEventType>(eventType),
                                                   std::move(points)));
    }

    std::unique_ptr<Self> self_;
    Core::detail::Handle<endpoint_touch_close> handle_;
    int vendorId_ = 0;
    int productId_ = 0;
    int width_ = 0;
    int height_ = 0;
};

}  // namespace GHIElectronics::Endpoint::Devices::UsbHost
