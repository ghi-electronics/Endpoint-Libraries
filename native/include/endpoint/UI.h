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
#include <vector>

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

    // Solid background color (0x00RRGGBB).
    void SetBackgroundColor(uint32_t rgb) {
        Core::detail::Check(endpoint_ui_widget_set_bg_color(this->_native, rgb));
    }
    // Linear gradient from start to end. direction: 0=vertical (top->bottom), 1=horizontal (left->right).
    void SetBackgroundGradient(uint32_t rgb_start, uint32_t rgb_end, int direction = 0) {
        Core::detail::Check(endpoint_ui_widget_set_bg_gradient(this->_native, rgb_start, rgb_end, direction));
    }
    // Tiled background from raw RGB565 pixels (little-endian). The byte buffer
    // must outlive the widget.
    void SetBackgroundImage(const uint8_t* data, int data_len, int width, int height) {
        Core::detail::Check(endpoint_ui_widget_set_bg_image(this->_native, data, data_len, width, height));
    }

    // WPF-style per-widget Margin.
    void SetMargin(int left, int top, int right, int bottom) {
        Core::detail::Check(endpoint_ui_widget_set_margin(this->_native, left, top, right, bottom));
    }

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

/// Container with WPF-style Border styling: thickness, brush color,
/// corner radius, padding. Background is set via the inherited
/// SetBackgroundColor on Widget.
class Border : public Widget {
public:
    explicit Border(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_border_create(parent.Native(), &this->_native));
    }

    void Thickness(int px)       { Core::detail::Check(endpoint_ui_border_set_thickness(this->_native, px)); }
    void BorderBrush(uint32_t c) { Core::detail::Check(endpoint_ui_border_set_brush(this->_native, c)); }
    void CornerRadius(int px)    { Core::detail::Check(endpoint_ui_border_set_corner_radius(this->_native, px)); }
    void Padding(int px)         { Core::detail::Check(endpoint_ui_border_set_padding(this->_native, px)); }
};

/// WPF-style StackPanel: stacks children vertically or horizontally via
/// LVGL flex layout. Spacing is the gap between children; children's
/// individual Margin stacks on top of that.
enum class Orientation { Vertical = 0, Horizontal = 1 };

class StackPanel : public Widget {
public:
    explicit StackPanel(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_stackpanel_create(parent.Native(), &this->_native));
    }

    void SetOrientation(Orientation o) {
        Core::detail::Check(endpoint_ui_stackpanel_set_orientation(this->_native, (int)o));
    }
    void Spacing(int px) {
        Core::detail::Check(endpoint_ui_stackpanel_set_spacing(this->_native, px));
    }
};

/// WPF-style Grid: children live in named rows/columns. Call
/// SetDefinitions once, then for each child call SetCell on it to place
/// it. Sizes: positive = fixed px, 0 or negative = star (-1 = "*", -2 =
/// "2*", ...).
class Grid : public Widget {
public:
    explicit Grid(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_grid_create(parent.Native(), &this->_native));
    }

    void SetDefinitions(const std::vector<int>& columns, const std::vector<int>& rows) {
        Core::detail::Check(endpoint_ui_grid_set_definitions(
            this->_native,
            columns.empty() ? nullptr : columns.data(), (int)columns.size(),
            rows.empty() ? nullptr : rows.data(), (int)rows.size()));
    }

    static void SetCell(const Widget& child, int column, int row,
                        int columnSpan = 1, int rowSpan = 1) {
        Core::detail::Check(endpoint_ui_grid_set_cell(child.Native(), column, columnSpan, row, rowSpan));
    }
};

/// WPF-style DockPanel. LVGL has no dock layout; the owner (or codegen)
/// positions children with absolute coords after construction. LastChildFill
/// is a hint for user code / codegen.
class DockPanel : public Widget {
public:
    explicit DockPanel(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_dockpanel_create(parent.Native(), &this->_native));
    }
};

/// Menu bar — a horizontal row of MenuItem widgets. MenuItem itself is a
/// handle wrapper (not a Widget subclass) returned by AddItem / AddDropdown.
class MenuItemHandle {
public:
    using ClickedHandler = std::function<void()>;
    void Clicked(ClickedHandler handler) {
        auto* fn = new ClickedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_clicked(this->_native, &detail::EventTrampoline, fn));
    }
    NativeHandle Native() const { return this->_native; }
private:
    friend class Menu;
    explicit MenuItemHandle(NativeHandle h) : _native(h) {}
    NativeHandle _native;
};

class Menu : public Widget {
public:
    explicit Menu(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_menu_create(parent.Native(), &this->_native));
    }
    void Background(uint32_t rgb) {
        Core::detail::Check(endpoint_ui_menu_set_background(this->_native, rgb));
    }
    /// Add a leaf item.
    MenuItemHandle AddItem(const std::string& header, int itemWidth = 0) {
        NativeHandle h = nullptr;
        Core::detail::Check(endpoint_ui_menuitem_create(this->_native, header.c_str(), itemWidth, &h));
        return MenuItemHandle(h);
    }
    /// Add a non-leaf item with a dropdown of sub-item strings.
    MenuItemHandle AddDropdown(const std::string& header,
                               const std::vector<std::string>& subItems,
                               int itemWidth = 0) {
        std::string joined;
        for (size_t i = 0; i < subItems.size(); ++i) {
            if (i) joined += '\n';
            joined += subItems[i];
        }
        NativeHandle h = nullptr;
        Core::detail::Check(endpoint_ui_menuitem_create_dropdown(this->_native, header.c_str(),
                                                                 joined.c_str(), itemWidth, &h));
        return MenuItemHandle(h);
    }
};

/// Month Calendar.
class Calendar : public Widget {
public:
    explicit Calendar(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_calendar_create(parent.Native(), &this->_native));
    }
    void SetShowedDate(int year, int month) {
        Core::detail::Check(endpoint_ui_calendar_set_showed_date(this->_native, year, month));
    }
    void SetToday(int year, int month, int day) {
        Core::detail::Check(endpoint_ui_calendar_set_today(this->_native, year, month, day));
    }
};

/// DataGrid — a simple rows x columns table. Call SetSize, then SetCell on
/// each cell. (TinyCLR-style simple API; the widget keeps the standard name.)
class DataGrid : public Widget {
public:
    explicit DataGrid(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_datagrid_create(parent.Native(), &this->_native));
    }
    void SetSize(int columns, int rows) {
        Core::detail::Check(endpoint_ui_datagrid_set_size(this->_native, columns, rows));
    }
    void SetCell(int row, int column, const std::string& text) {
        Core::detail::Check(endpoint_ui_datagrid_set_cell(this->_native, row, column, text.c_str()));
    }
    void SetColumnWidth(int column, int px) {
        Core::detail::Check(endpoint_ui_datagrid_set_column_width(this->_native, column, px));
    }
};

/// TreeView — a vertical list of indented nodes.
class TreeView : public Widget {
public:
    explicit TreeView(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_treeview_create(parent.Native(), &this->_native));
    }
    void AddNode(int depth, bool isExpanded, bool hasChildren, const std::string& header) {
        Core::detail::Check(endpoint_ui_treeview_add_node(this->_native, depth,
            isExpanded ? 1 : 0, hasChildren ? 1 : 0, header.c_str(), nullptr));
    }
    void Clear() { Core::detail::Check(endpoint_ui_treeview_clear(this->_native)); }
};

/// Chart with a single series. Modes: Line (default), Bar, Area.
enum class ChartMode { Line = 0, Rectangle = 1, Area = 2 };

class Chart : public Widget {
public:
    explicit Chart(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_chart_create(parent.Native(), &this->_native));
    }
    void Mode(ChartMode m) {
        Core::detail::Check(endpoint_ui_chart_set_mode(this->_native, (int)m));
    }
    void SetColors(uint32_t pointColor, uint32_t areaColor, uint32_t labelColor) {
        Core::detail::Check(endpoint_ui_chart_set_colors(this->_native, pointColor, areaColor, labelColor));
    }
    void DivisionAxisY(int divisions) {
        Core::detail::Check(endpoint_ui_chart_set_division(this->_native, divisions));
    }
    void SetPoints(const std::vector<int>& values) {
        Core::detail::Check(endpoint_ui_chart_set_points(this->_native,
            values.empty() ? nullptr : values.data(), (int)values.size()));
    }
};

/// Radial Gauge. The width/height passed to the constructor is fixed — the
/// scale and needle are sized at creation time.
class Gauge : public Widget {
public:
    Gauge(const Widget& parent, int width, int height) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_gauge_create(parent.Native(), width, height, &this->_native));
    }
    void SetRange(int min, int max) {
        Core::detail::Check(endpoint_ui_gauge_set_range(this->_native, min, max));
    }
    void Value(int value) {
        Core::detail::Check(endpoint_ui_gauge_set_value(this->_native, value));
    }
    void SweepAngle(int degrees) {
        Core::detail::Check(endpoint_ui_gauge_set_sweep(this->_native, degrees));
    }
    /// Needle center-Y (0..1 fraction of height) and length (0..1 of half-min-dim).
    void SetNeedleShape(float centerY, float length) {
        Core::detail::Check(endpoint_ui_gauge_set_needle_shape(this->_native,
            (int)(centerY * 1000.0f), (int)(length * 1000.0f)));
    }
    void EnableScale(bool visible) {
        Core::detail::Check(endpoint_ui_gauge_show_scale(this->_native, visible ? 1 : 0));
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

/// Modal dialog with title, body text, and footer buttons. AddButton
/// returns a Button-like handle (wrapped Widget); subscribe its Clicked
/// event and call Hide() to dismiss.
class MessageBox;

/// Handle to a button inside a MessageBox. Does NOT own the native widget
/// (the MessageBox does), so copies are safe and the button survives after
/// AddButton() returns.
class MessageBoxButton {
public:
    using ClickedHandler = std::function<void()>;
    void Clicked(ClickedHandler handler) {
        auto* fn = new ClickedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_clicked(this->_native, &detail::EventTrampoline, fn));
    }
    NativeHandle Native() const { return this->_native; }
private:
    friend class MessageBox;
    explicit MessageBoxButton(NativeHandle h) : _native(h) {}
    NativeHandle _native;
};

class MessageBox : public Widget {
public:
    explicit MessageBox(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_messagebox_create(parent.Native(), &this->_native));
    }

    void SetTitle(const std::string& t) { Core::detail::Check(endpoint_ui_messagebox_set_title(this->_native, t.c_str())); }
    void SetBody(const std::string& t)  { Core::detail::Check(endpoint_ui_messagebox_set_body(this->_native, t.c_str())); }

    MessageBoxButton AddButton(const std::string& text) {
        NativeHandle btn = nullptr;
        Core::detail::Check(endpoint_ui_messagebox_add_button(this->_native, text.c_str(), &btn));
        return MessageBoxButton(btn);
    }

    void Hide() { Core::detail::Check(endpoint_ui_messagebox_hide(this->_native)); }
};

/// Vertical scrollable list. AddItem returns a Button-like handle whose
/// Clicked event you subscribe to.
class ListBox;

/// Handle to an item inside a ListBox. Does NOT own the native widget
/// (the ListBox does), so AddItem() can be a fire-and-forget call for
/// static lists, and copies are safe.
class ListBoxItem {
public:
    using ClickedHandler = std::function<void()>;
    void Clicked(ClickedHandler handler) {
        auto* fn = new ClickedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_clicked(this->_native, &detail::EventTrampoline, fn));
    }
    NativeHandle Native() const { return this->_native; }
private:
    friend class ListBox;
    explicit ListBoxItem(NativeHandle h) : _native(h) {}
    NativeHandle _native;
};

class ListBox : public Widget {
public:
    explicit ListBox(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_listbox_create(parent.Native(), &this->_native));
    }

    ListBoxItem AddItem(const std::string& text) {
        NativeHandle item = nullptr;
        Core::detail::Check(endpoint_ui_listbox_add_item(this->_native, text.c_str(), &item));
        return ListBoxItem(item);
    }

    void Clear() { Core::detail::Check(endpoint_ui_listbox_clear(this->_native)); }
};

/// Circular-indicator CheckBox. Grouping (mutual exclusion) is up to the
/// caller: register ValueChanged on each, uncheck the others when one is
/// selected.
class RadioButton : public Widget {
public:
    explicit RadioButton(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_radiobutton_create(parent.Native(), &this->_native));
    }

    void Text(const std::string& value) { Core::detail::Check(endpoint_ui_radiobutton_set_text(this->_native, value.c_str())); }
    void IsChecked(bool value)          { Core::detail::Check(endpoint_ui_radiobutton_set_checked(this->_native, value ? 1 : 0)); }
    bool IsChecked() const              { int v = 0; Core::detail::Check(endpoint_ui_radiobutton_get_checked(this->_native, &v)); return v != 0; }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

/// Select one item from a list. ValueChanged fires when the selection changes.
class Dropdown : public Widget {
public:
    explicit Dropdown(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_dropdown_create(parent.Native(), &this->_native));
    }

    /// Options passed as a vector of strings; internally joined with '\n'.
    void SetOptions(const std::vector<std::string>& options) {
        std::string joined;
        for (size_t i = 0; i < options.size(); ++i) {
            if (i) joined += '\n';
            joined += options[i];
        }
        Core::detail::Check(endpoint_ui_dropdown_set_options(this->_native, joined.c_str()));
    }

    void Selected(int index)  { Core::detail::Check(endpoint_ui_dropdown_set_selected(this->_native, index)); }
    int Selected() const      { int v = 0; Core::detail::Check(endpoint_ui_dropdown_get_selected(this->_native, &v)); return v; }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

/// Horizontal progress bar. No events; the owner drives it via SetValue().
class ProgressBar : public Widget {
public:
    explicit ProgressBar(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_progressbar_create(parent.Native(), &this->_native));
    }

    void SetRange(int min, int max) { Core::detail::Check(endpoint_ui_progressbar_set_range(this->_native, min, max)); }
    void Value(int value)           { Core::detail::Check(endpoint_ui_progressbar_set_value(this->_native, value)); }
    int Value() const               { int v = 0; Core::detail::Check(endpoint_ui_progressbar_get_value(this->_native, &v)); return v; }
};

/// Binary on/off toggle. ValueChanged fires on each flip.
class Switch : public Widget {
public:
    explicit Switch(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_switch_create(parent.Native(), &this->_native));
    }

    void IsChecked(bool value) { Core::detail::Check(endpoint_ui_switch_set_checked(this->_native, value ? 1 : 0)); }
    bool IsChecked() const     { int v = 0; Core::detail::Check(endpoint_ui_switch_get_checked(this->_native, &v)); return v != 0; }

    using ValueChangedHandler = std::function<void()>;
    void ValueChanged(ValueChangedHandler handler) {
        auto* fn = new ValueChangedHandler(std::move(handler));
        Core::detail::Check(endpoint_ui_subscribe_value_changed(this->_native, &detail::EventTrampoline, fn));
    }
};

/// Virtual on-screen keyboard. Attach to a TextBox so taps on keys type
/// into it. Positions itself at the bottom half of the display by default;
/// use the inherited geometry setters to override.
class Keyboard : public Widget {
public:
    explicit Keyboard(const Widget& parent) : Widget(nullptr) {
        Core::detail::Check(endpoint_ui_keyboard_create(parent.Native(), &this->_native));
    }

    void SetTarget(const TextBox& textbox) {
        Core::detail::Check(endpoint_ui_keyboard_set_textbox(this->_native, textbox.Native()));
    }
    void ClearTarget() {
        Core::detail::Check(endpoint_ui_keyboard_set_textbox(this->_native, nullptr));
    }
};

}  // namespace UI
}  // namespace Endpoint
}  // namespace GHIElectronics
