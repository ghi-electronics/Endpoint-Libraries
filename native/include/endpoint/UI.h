// GHIElectronics::Endpoint::UI — widgets on top of the embedded LVGL in
// libendpoint. API mirrors the .NET and Python packages.
//
// Lifecycle:
//   DisplayController::GetDefault()->Enable();   // bring up framebuffer
//   UI::Initialize();                            // bind LVGL to /dev/fb0
//   Screen screen;
//   Button button(screen);
//   button.Text("Click me");
//   button.Clicked([]{ std::puts("clicked"); });
//   screen.Load();
//   UI::Run();                                   // blocks until Stop()
#pragma once

#include <cstdint>
#include <functional>
#include <string>

#include "Core.h"                 // detail::Check
#include "detail/CApi.h"          // endpoint_ui_* C API

namespace GHIElectronics {
namespace Endpoint {
namespace UI {

/// Opaque handle to a native widget (internally an lv_obj_t*).
using NativeHandle = void*;

namespace detail {
    // Event trampoline: the C API passes back context as void*. We allocate a
    // std::function on the heap at subscribe time and reconstruct it here.
    // Lives until process exit (unsubscribe isn't exposed yet).
    inline void EventTrampoline(void* context) {
        auto* fn = static_cast<std::function<void()>*>(context);
        if (fn) (*fn)();
    }
}

// UI subsystem lifecycle. Call Initialize() once after the display is
// enabled; call Run() to drive the event loop; Stop() to exit. Free
// functions (not a class) so that `UI::Initialize()` looks like
// namespace-qualified call rather than `UI::UI::Initialize()`.
inline void Initialize() { Core::detail::Check(endpoint_ui_initialize()); }
inline void Run()        { Core::detail::Check(endpoint_ui_run()); }
inline void Stop()       { Core::detail::Check(endpoint_ui_stop()); }

/// Touch input bridge. Call TouchAttach() once after Initialize(), then
/// call TouchFeed(x, y, pressed) from your touch chip's event handler.
namespace Touch {
    inline void Attach() { Core::detail::Check(endpoint_ui_touch_attach()); }
    inline void Feed(int x, int y, bool pressed) {
        Core::detail::Check(endpoint_ui_touch_feed(x, y, pressed ? 1 : 0));
    }
}

/// Base class for every widget. Holds the native handle; destructor releases
/// the underlying LVGL object. Not copyable (widgets own their handle).
class Widget {
public:
    virtual ~Widget() {
        if (this->_native) endpoint_ui_widget_destroy(this->_native);
    }

    int X() const       { int v = 0; Core::detail::Check(endpoint_ui_widget_get_pos(this->_native, &v, nullptr)); return v; }
    int Y() const       { int v = 0; Core::detail::Check(endpoint_ui_widget_get_pos(this->_native, nullptr, &v)); return v; }
    int Width() const   { int v = 0; Core::detail::Check(endpoint_ui_widget_get_size(this->_native, &v, nullptr)); return v; }
    int Height() const  { int v = 0; Core::detail::Check(endpoint_ui_widget_get_size(this->_native, nullptr, &v)); return v; }
    bool Visible() const { int v = 0; Core::detail::Check(endpoint_ui_widget_get_visible(this->_native, &v)); return v != 0; }

    // Single-axis set: LVGL's getters return 0 until the first layout pass,
    // so a read-then-write-both would zero out the untouched axis. These
    // forward to lv_obj_set_x/y/width/height which only touch one axis.
    void X(int value)       { Core::detail::Check(endpoint_ui_widget_set_x(this->_native, value)); }
    void Y(int value)       { Core::detail::Check(endpoint_ui_widget_set_y(this->_native, value)); }
    void Width(int value)   { Core::detail::Check(endpoint_ui_widget_set_width(this->_native, value)); }
    void Height(int value)  { Core::detail::Check(endpoint_ui_widget_set_height(this->_native, value)); }
    void Visible(bool value) { Core::detail::Check(endpoint_ui_widget_set_visible(this->_native, value ? 1 : 0)); }

    NativeHandle Native() const { return this->_native; }

protected:
    explicit Widget(NativeHandle native) : _native(native) {}
    NativeHandle _native;

    Widget(const Widget&) = delete;
    Widget& operator=(const Widget&) = delete;
};

/// A top-level container with no parent. Multiple screens can exist at once;
/// only one is active. Call Load() to make this the displayed screen.
class Screen : public Widget {
public:
    Screen() : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_screen_create(&this->_native));
    }

    void Load() { Core::detail::Check(endpoint_ui_screen_load(this->_native)); }

    /// The screen currently displayed on the panel. Never destroyed by this
    /// wrapper (owned by LVGL). The returned reference is static storage.
    static Screen& Active() {
        static Screen instance{SentinelActive{}};
        Core::detail::Check(endpoint_ui_screen_active(&instance._native));
        return instance;
    }

private:
    struct SentinelActive {};
    explicit Screen(SentinelActive) : Widget(nullptr) {}
};

class Container : public Widget {
public:
    explicit Container(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_container_create(parent.Native(), &this->_native));
    }
};

class Button : public Widget {
public:
    explicit Button(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_button_create(parent.Native(), &this->_native));
    }

    void Text(const std::string& value)        { Core::detail::Check(endpoint_ui_button_set_text(this->_native, value.c_str())); }
    void BackgroundColor(uint32_t rgb)         { Core::detail::Check(endpoint_ui_button_set_bg_color(this->_native, rgb)); }
    void TextColor(uint32_t rgb)               { Core::detail::Check(endpoint_ui_button_set_text_color(this->_native, rgb)); }
    void FontSize(int size)                    { Core::detail::Check(endpoint_ui_button_set_font_size(this->_native, size)); }

    using ClickedHandler = std::function<void()>;
    void Clicked(ClickedHandler handler) {
        auto* fn = new ClickedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_clicked(this->_native, &detail::EventTrampoline, fn));
    }
};

class Label : public Widget {
public:
    explicit Label(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_label_create(parent.Native(), &this->_native));
    }

    void Text(const std::string& value) { Core::detail::Check(endpoint_ui_label_set_text(this->_native, value.c_str())); }
    void Color(uint32_t rgb)            { Core::detail::Check(endpoint_ui_label_set_color(this->_native, rgb)); }
    void FontSize(int size)             { Core::detail::Check(endpoint_ui_label_set_font_size(this->_native, size)); }
};

class Image : public Widget {
public:
    explicit Image(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_image_create(parent.Native(), &this->_native));
    }

    /// Caller keeps `pixels` alive for the lifetime of the widget (not copied).
    void SetSource(const uint8_t* pixels, size_t byteCount, int width, int height) {
        Core::detail::Check(endpoint_ui_image_set_source(this->_native, pixels, (int)byteCount, width, height));
    }
};

class TextBox : public Widget {
public:
    explicit TextBox(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_textbox_create(parent.Native(), &this->_native));
    }

    void Text(const std::string& value)        { Core::detail::Check(endpoint_ui_textbox_set_text(this->_native, value.c_str())); }
    void Placeholder(const std::string& value) { Core::detail::Check(endpoint_ui_textbox_set_placeholder(this->_native, value.c_str())); }

    std::string Text() const {
        char buf[256];
        Core::detail::Check(endpoint_ui_textbox_get_text(this->_native, buf, sizeof(buf)));
        return std::string(buf);
    }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

class Slider : public Widget {
public:
    explicit Slider(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_slider_create(parent.Native(), &this->_native));
    }

    // Minimum and Maximum are set together via SetRange (LVGL stores them as a pair).
    void SetRange(int min, int max) { Core::detail::Check(endpoint_ui_slider_set_range(this->_native, min, max)); }
    void Value(int value)           { Core::detail::Check(endpoint_ui_slider_set_value(this->_native, value)); }
    int Value() const               { int v = 0; Core::detail::Check(endpoint_ui_slider_get_value(this->_native, &v)); return v; }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

class CheckBox : public Widget {
public:
    explicit CheckBox(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_checkbox_create(parent.Native(), &this->_native));
    }

    void Text(const std::string& value) { Core::detail::Check(endpoint_ui_checkbox_set_text(this->_native, value.c_str())); }
    void IsChecked(bool value)          { Core::detail::Check(endpoint_ui_checkbox_set_checked(this->_native, value ? 1 : 0)); }
    bool IsChecked() const              { int v = 0; Core::detail::Check(endpoint_ui_checkbox_get_checked(this->_native, &v)); return v != 0; }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

}  // namespace UI
}  // namespace Endpoint
}  // namespace GHIElectronics
