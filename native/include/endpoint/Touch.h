// Touchscreens on Endpoint. Touch is an abstract base: subclass it for your
// panel's chip (I2C, SPI, custom) and emit events via RaiseChanged() from
// your reader loop or interrupt handler. The library ships one concrete
// subclass, InputTouch, that wraps a kernel /dev/input/eventN - used for
// USB HID touchscreens (hot-plugged, discovered via UsbHostController).
//
//   using namespace GHIElectronics::Endpoint::Devices::Touch;
//
//   auto t = Touch::GetDefault();   // kernel /dev/input/event0 if any, else nullptr
//   if (t) {
//       t->SetOnEvent([](Touch& sender, const TouchEventArgs& e) {
//           for (const auto& p : e.Points())
//               std::cout << p.Id << ": " << p.X << ',' << p.Y << '\n';
//       });
//   }

#pragma once

#include <functional>
#include <memory>
#include <string>
#include <utility>
#include <vector>

#include "Core.h"
#include "detail/CApi.h"
#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Touch {

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
    TouchEventType EventType() const { return this->type_; }
    const std::vector<TouchPoint>& Points() const { return this->points_; }
private:
    TouchEventType type_;
    std::vector<TouchPoint> points_;
};

// Abstract touchscreen. Subclass for your chip.
class Touch {
public:
    virtual ~Touch() = default;

    virtual int Width() const = 0;
    virtual int Height() const = 0;
    virtual bool Connected() const = 0;

    using Handler = std::function<void(Touch&, const TouchEventArgs&)>;
    void SetOnEvent(Handler handler) { this->handler_ = std::move(handler); }

    // The first kernel-exposed touchscreen the library can find, or nullptr.
    static std::unique_ptr<Touch> GetDefault();

protected:
    // Subclasses call this to emit a touch event.
    void RaiseChanged(const TouchEventArgs& e) {
        if (this->handler_) this->handler_(*this, e);
    }

private:
    Handler handler_;
};

// Touchscreen backed by a kernel input device at /dev/input/eventN. Used for
// USB HID touchscreens; the on-board panel's custom chip is the customer's
// own subclass of Touch.
class InputTouch : public Touch {
public:
    explicit InputTouch(const std::string& deviceName)
        : self_(std::make_unique<Self>()) {
        this->self_->owner = this;
        void* h = nullptr;
        Core::detail::Check(endpoint_touch_open(deviceName.c_str(), &Trampoline,
                                                this->self_.get(), &h));
        this->handle_ = Core::detail::Handle<endpoint_touch_close>(h);
        int connected = 0;
        Core::detail::Check(endpoint_touch_info(this->handle_.Get("InputTouch"),
                                                &this->vendorId_, &this->productId_,
                                                &connected, &this->width_, &this->height_));
        this->connected_ = connected != 0;
        this->deviceName_ = deviceName;
    }

    int Width() const override { return this->width_; }
    int Height() const override { return this->height_; }
    bool Connected() const override { return this->connected_; }

    const std::string& DeviceName() const { return this->deviceName_; }
    int VendorId() const { return this->vendorId_; }
    int ProductId() const { return this->productId_; }

private:
    struct Self {
        InputTouch* owner = nullptr;
    };

    static void Trampoline(void* context, int eventType, int count,
                           const int* ids, const int* xs, const int* ys) {
        auto* self = static_cast<Self*>(context);
        std::vector<TouchPoint> points;
        points.reserve(count);
        for (int i = 0; i < count; ++i) {
            points.push_back({ids[i], xs[i], ys[i]});
        }
        self->owner->RaiseChanged(TouchEventArgs(static_cast<TouchEventType>(eventType),
                                                 std::move(points)));
    }

    std::unique_ptr<Self> self_;
    Core::detail::Handle<endpoint_touch_close> handle_;
    std::string deviceName_;
    int vendorId_ = 0;
    int productId_ = 0;
    int width_ = 0;
    int height_ = 0;
    bool connected_ = false;
};

inline std::unique_ptr<Touch> Touch::GetDefault() {
    char buf[64] = {};
    Core::detail::Check(endpoint_touch_find_default(buf, sizeof buf));
    if (buf[0] == '\0') return nullptr;
    return std::unique_ptr<Touch>(new InputTouch(std::string(buf)));
}

}  // namespace GHIElectronics::Endpoint::Devices::Touch
