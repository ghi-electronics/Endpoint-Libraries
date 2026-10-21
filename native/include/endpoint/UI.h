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
//
// This header declares the public surface; M4 wires it to the libendpoint
// C API (lv_* internals stay private). Implementation lands in M5.
#pragma once

#include <cstdint>
#include <functional>
#include <string>

namespace GHIElectronics {
namespace Endpoint {
namespace UI {

/// Opaque handle to a native widget (internally an lv_obj_t*).
using NativeHandle = void*;

/// UI subsystem lifecycle. Call Initialize() once after the display is
/// enabled; call Run() to drive the event loop; Stop() to exit.
class UI {
public:
    static void Initialize();
    static void Run();
    static void Stop();
};

/// Base class for every widget. Not instantiable directly; use Screen,
/// Button, Label, etc. Holds the native handle; destructor releases the
/// underlying LVGL object.
class Widget {
public:
    virtual ~Widget();

    int X() const;        void X(int value);
    int Y() const;        void Y(int value);
    int Width() const;    void Width(int value);
    int Height() const;   void Height(int value);
    bool Visible() const; void Visible(bool value);

    NativeHandle Native() const { return this->_native; }

protected:
    explicit Widget(NativeHandle native);
    NativeHandle _native;

    // Not copyable (widgets own their native handle).
    Widget(const Widget&) = delete;
    Widget& operator=(const Widget&) = delete;
};

/// A top-level container with no parent. Multiple screens can exist at once;
/// only one is active. Call Load() to make this the displayed screen.
class Screen : public Widget {
public:
    Screen();
    void Load();
    static Screen& Active();
};

class Container : public Widget {
public:
    explicit Container(const Widget& parent);
};

class Button : public Widget {
public:
    explicit Button(const Widget& parent);

    std::string Text() const;     void Text(const std::string& value);
    uint32_t BackgroundColor() const; void BackgroundColor(uint32_t rgb);
    uint32_t TextColor() const;   void TextColor(uint32_t rgb);
    int FontSize() const;         void FontSize(int value);

    using ClickedHandler = std::function<void()>;
    void Clicked(ClickedHandler handler);
};

class Label : public Widget {
public:
    explicit Label(const Widget& parent);

    std::string Text() const; void Text(const std::string& value);
    uint32_t Color() const;   void Color(uint32_t rgb);
    int FontSize() const;     void FontSize(int value);
};

class Image : public Widget {
public:
    explicit Image(const Widget& parent);

    /// Set the image from RGB565 pixel bytes (little-endian, matches
    /// Display::PixelFormat::Format16bppRgb565). Width/Height describe the
    /// source bitmap; use the inherited Width/Height setters for display
    /// size (LVGL scales).
    void SetSource(const uint8_t* rgb565Pixels, size_t byteCount, int width, int height);
};

class TextBox : public Widget {
public:
    explicit TextBox(const Widget& parent);

    std::string Text() const;        void Text(const std::string& value);
    std::string Placeholder() const; void Placeholder(const std::string& value);

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler);
};

class Slider : public Widget {
public:
    explicit Slider(const Widget& parent);

    int Minimum() const; void Minimum(int value);
    int Maximum() const; void Maximum(int value);
    int Value() const;   void Value(int value);

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler);
};

class CheckBox : public Widget {
public:
    explicit CheckBox(const Widget& parent);

    std::string Text() const; void Text(const std::string& value);
    bool IsChecked() const;   void IsChecked(bool value);

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler);
};

}  // namespace UI
}  // namespace Endpoint
}  // namespace GHIElectronics
