"""GHIElectronics.Endpoint.UI — widgets on top of the embedded LVGL in
libendpoint. API mirrors the .NET package character-for-character
(PascalCase properties and methods).

Usage::

    from GHIElectronics.Endpoint.Devices.Display import DisplayController
    from GHIElectronics.Endpoint.UI import UI, Screen, Button

    display = DisplayController.GetDefault()
    display.Enable()
    UI.Initialize()

    screen = Screen()
    button = Button(screen)
    button.Text = "Click me"
    button.Clicked += lambda: print("clicked")
    screen.Load()
    UI.Run()
"""

import ctypes

from GHIElectronics.Endpoint.Core._native import native, UiEventCallback


# ------------------------------------------------------------------
# Event helper
# ------------------------------------------------------------------

class _Event:
    """Mimics the .NET += / -= event idiom. Internal; use via Widget events."""

    def __init__(self):
        self._handlers = []

    def __iadd__(self, handler):
        self._handlers.append(handler)
        return self

    def __isub__(self, handler):
        if handler in self._handlers:
            self._handlers.remove(handler)
        return self

    def _fire(self):
        for h in self._handlers:
            try:
                h()
            except Exception:
                # A C callback must not propagate Python exceptions back into
                # native code (would unwind through LVGL). Swallow.
                pass


# ------------------------------------------------------------------
# UI subsystem lifecycle
# ------------------------------------------------------------------

class UI:
    """UI subsystem lifecycle. Call Initialize() once after the display is
    enabled; call Run() to drive the event loop; Stop() to exit."""

    @staticmethod
    def Initialize():
        native.ui_initialize()

    @staticmethod
    def Run():
        native.ui_run()

    @staticmethod
    def Stop():
        native.ui_stop()


class TouchBridge:
    """Touch input bridge. Call Attach() once after UI.Initialize(), then
    call Feed(x, y, pressed) from your touch chip's event handler. LVGL will
    dispatch Clicked / ValueChanged accordingly on its event loop."""

    @staticmethod
    def Attach():
        native.ui_touch_attach()

    @staticmethod
    def Feed(x, y, pressed):
        native.ui_touch_feed(x, y, 1 if pressed else 0)


class UiTimer:
    """Thread-safe periodic callback that runs on the UI thread. The callable
    fires inside UI.Run()'s LVGL pump every period_ms milliseconds, so widget
    updates in it are safe (LVGL itself is single-threaded). Dispose() /
    __del__ cancels the timer. Create / set_period / Dispose are safe from any
    thread; the body runs on the UI thread.

    Example::

        def tick():
            gauge.Value = int(1650 + 1450 * math.sin(phase))
        timer = UiTimer(100, tick)
    """

    def __init__(self, period_ms, callback):
        if callback is None:
            raise ValueError("callback is required")
        self._callback = callback
        # Keep a reference to the trampoline so ctypes doesn't collect it while
        # the native timer still holds a pointer to it.
        self._trampoline = UiEventCallback(lambda _ctx: self._fire())
        handle = native.out_handle()
        native.ui_timer_create(int(period_ms), self._trampoline, None, native.ref(handle))
        self._handle = handle.value

    def _fire(self):
        try:
            self._callback()
        except Exception:
            # A C callback must not propagate Python exceptions back into
            # native code (would unwind through LVGL). Swallow.
            pass

    def SetPeriod(self, period_ms):
        if self._handle:
            native.ui_timer_set_period(self._handle, int(period_ms))

    def Dispose(self):
        if self._handle:
            native.ui_timer_delete(self._handle)
            self._handle = None

    def __del__(self):
        try: self.Dispose()
        except Exception: pass


# ------------------------------------------------------------------
# Base + widgets
# ------------------------------------------------------------------

class Widget:
    """Base class for every widget. Holds the native handle and shared
    geometry/visibility."""

    def __init__(self, native_handle):
        self._native = native_handle
        # ctypes keeps weak references to CFUNCTYPE instances by default; we
        # pin event trampolines here so GC doesn't collect them mid-use.
        self._event_trampolines = []

    @property
    def X(self):
        x, y = native.out_int(), native.out_int()
        native.ui_widget_get_pos(self._native, native.ref(x), native.ref(y))
        return x.value

    @X.setter
    def X(self, value):
        # Single-axis set: LVGL's getters return 0 until first layout pass,
        # so read-then-write-both would zero out the untouched axis.
        native.ui_widget_set_x(self._native, value)

    @property
    def Y(self):
        x, y = native.out_int(), native.out_int()
        native.ui_widget_get_pos(self._native, native.ref(x), native.ref(y))
        return y.value

    @Y.setter
    def Y(self, value):
        native.ui_widget_set_y(self._native, value)

    @property
    def Width(self):
        w, h = native.out_int(), native.out_int()
        native.ui_widget_get_size(self._native, native.ref(w), native.ref(h))
        return w.value

    @Width.setter
    def Width(self, value):
        native.ui_widget_set_width(self._native, value)

    @property
    def Height(self):
        w, h = native.out_int(), native.out_int()
        native.ui_widget_get_size(self._native, native.ref(w), native.ref(h))
        return h.value

    @Height.setter
    def Height(self, value):
        native.ui_widget_set_height(self._native, value)

    @property
    def Visible(self):
        v = native.out_int()
        native.ui_widget_get_visible(self._native, native.ref(v))
        return v.value != 0

    @Visible.setter
    def Visible(self, value):
        native.ui_widget_set_visible(self._native, 1 if value else 0)

    @property
    def BackgroundColor(self):
        raise AttributeError("BackgroundColor is write-only")

    @BackgroundColor.setter
    def BackgroundColor(self, rgb):
        """Solid background color (0x00RRGGBB)."""
        native.ui_widget_set_bg_color(self._native, int(rgb))

    def SetBackgroundGradient(self, rgb_start, rgb_end, direction=0):
        """Linear gradient background. direction: 0 = vertical (top->bottom), 1 = horizontal (left->right)."""
        native.ui_widget_set_bg_gradient(self._native, int(rgb_start), int(rgb_end), int(direction))

    def SetMargin(self, left, top, right, bottom):
        """WPF-style per-widget Margin. Works in any parent."""
        native.ui_widget_set_margin(self._native, int(left), int(top), int(right), int(bottom))

    def SetBackgroundImage(self, rgb565_bytes, width, height):
        """Tiled background from raw RGB565 pixel bytes (little-endian).
        The bytes buffer is pinned on the widget so LVGL can hold a pointer
        into it."""
        if not hasattr(self, "_bg_pixels"):
            self._bg_pixels = None
        self._bg_pixels = bytes(rgb565_bytes)  # keep alive for the widget's life
        buf = (ctypes.c_ubyte * len(self._bg_pixels)).from_buffer_copy(self._bg_pixels)
        self._bg_pixels_buf = buf  # also keep the ctypes buffer alive
        native.ui_widget_set_bg_image(self._native, buf, len(self._bg_pixels), width, height)

    def Dispose(self):
        if self._native:
            native.ui_widget_destroy(self._native)
            self._native = None
        self._event_trampolines.clear()


class Screen(Widget):
    """A top-level container with no parent. Call Load() to make it active."""

    def __init__(self):
        h = native.out_handle()
        native.ui_screen_create(native.ref(h))
        super().__init__(h.value)

    def Load(self):
        native.ui_screen_load(self._native)

    @staticmethod
    def Active():
        h = native.out_handle()
        native.ui_screen_active(native.ref(h))
        s = Screen.__new__(Screen)
        Widget.__init__(s, h.value)
        return s


class Container(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_container_create(parent._native, native.ref(h))
        super().__init__(h.value)


class Orientation:
    """StackPanel orientation (mirrors the .NET enum of the same name)."""
    Vertical = 0
    Horizontal = 1


class StackPanel(Widget):
    """WPF-style StackPanel: stacks children vertically (default) or
    horizontally via LVGL flex. Spacing is the inter-child gap; each
    child's SetMargin stacks on top of that."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_stackpanel_create(parent._native, native.ref(h))
        super().__init__(h.value)

    @property
    def Orientation(self): raise AttributeError("Orientation is write-only")
    @Orientation.setter
    def Orientation(self, v): native.ui_stackpanel_set_orientation(self._native, int(v))

    @property
    def Spacing(self): raise AttributeError("Spacing is write-only")
    @Spacing.setter
    def Spacing(self, px): native.ui_stackpanel_set_spacing(self._native, int(px))


class Grid(Widget):
    """WPF-style Grid with ColumnDefinitions + RowDefinitions. Sizes:
    positive = fixed px, 0 or negative = star (-1 = '*', -2 = '2*', ...)."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_grid_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._cols_buf = None  # keep arrays alive while LVGL points into them
        self._rows_buf = None

    def SetDefinitions(self, columns, rows):
        c_arr = (ctypes.c_int * len(columns))(*[int(v) for v in columns])
        r_arr = (ctypes.c_int * len(rows))(*[int(v) for v in rows])
        self._cols_buf = c_arr; self._rows_buf = r_arr
        native.ui_grid_set_definitions(self._native, c_arr, len(columns), r_arr, len(rows))

    @staticmethod
    def SetCell(child, column, row, column_span=1, row_span=1):
        native.ui_grid_set_cell(child._native, int(column), int(column_span), int(row), int(row_span))


class DockPanel(Widget):
    """WPF-style DockPanel. LVGL has no dock layout; the caller positions
    children using their x/y/width/height after reading their `dock` side."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_dockpanel_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.LastChildFill = True


class Menu(Widget):
    """Horizontal menu bar."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_menu_create(parent._native, native.ref(h))
        super().__init__(h.value)

    @property
    def Background(self): raise AttributeError("Background is write-only")
    @Background.setter
    def Background(self, rgb): native.ui_menu_set_background(self._native, int(rgb))

    def AddItem(self, header, item_width=0):
        out = native.out_handle()
        native.ui_menuitem_create(self._native, header.encode("utf-8"), int(item_width), native.ref(out))
        return MenuItem(out.value)

    def AddDropdown(self, header, sub_items, item_width=0):
        joined = "\n".join(sub_items or [])
        out = native.out_handle()
        native.ui_menuitem_create_dropdown(
            self._native, header.encode("utf-8"), joined.encode("utf-8"), int(item_width), native.ref(out))
        return MenuItem(out.value)


class MenuItem:
    """Handle to a menu item. Subscribe to Clicked / ValueChanged."""
    def __init__(self, native_handle):
        self._native = native_handle
        # _subscribe appends to this list to keep CFUNCTYPE trampolines pinned
        # (ctypes holds weak refs by default). MenuItem isn't a Widget subclass
        # so we have to set it up ourselves.
        self._event_trampolines = []
        self.Clicked = _Event()
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_clicked, self.Clicked)
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    @property
    def Selected(self):
        out = native.out_int()
        native.ui_dropdown_get_selected(self._native, native.ref(out))
        return out.value


class Calendar(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_calendar_create(parent._native, native.ref(h))
        super().__init__(h.value)

    def SetShowedDate(self, year, month):
        native.ui_calendar_set_showed_date(self._native, int(year), int(month))

    def SetToday(self, year, month, day):
        native.ui_calendar_set_today(self._native, int(year), int(month), int(day))


class DataGrid(Widget):
    """Simple grid of text cells (TinyCLR-style simple API)."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_datagrid_create(parent._native, native.ref(h))
        super().__init__(h.value)

    def SetSize(self, columns, rows):
        native.ui_datagrid_set_size(self._native, int(columns), int(rows))

    def SetCell(self, row, column, text):
        native.ui_datagrid_set_cell(self._native, int(row), int(column), (text or "").encode("utf-8"))

    def SetColumnWidth(self, column, px):
        native.ui_datagrid_set_column_width(self._native, int(column), int(px))


class TreeView(Widget):
    """Hierarchical list of indented nodes."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_treeview_create(parent._native, native.ref(h))
        super().__init__(h.value)

    def AddNode(self, depth, is_expanded, has_children, header):
        out = native.out_handle()
        native.ui_treeview_add_node(
            self._native, int(depth), 1 if is_expanded else 0, 1 if has_children else 0,
            (header or "").encode("utf-8"), native.ref(out))

    def Clear(self):
        native.ui_treeview_clear(self._native)


class ChartMode:
    Line = 0
    Rectangle = 1
    Area = 2


class Chart(Widget):
    """TinyCLR-style simple chart (not standard .NET)."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_chart_create(parent._native, native.ref(h))
        super().__init__(h.value)

    @property
    def Mode(self): raise AttributeError("Mode is write-only")
    @Mode.setter
    def Mode(self, m): native.ui_chart_set_mode(self._native, int(m))

    def SetColors(self, point_color, area_color, label_color):
        native.ui_chart_set_colors(self._native, int(point_color), int(area_color), int(label_color))

    @property
    def DivisionAxisY(self): raise AttributeError("DivisionAxisY is write-only")
    @DivisionAxisY.setter
    def DivisionAxisY(self, d): native.ui_chart_set_division(self._native, int(d))

    def SetPoints(self, values):
        buf = (ctypes.c_int * len(values))(*[int(v) for v in values])
        self._points_buf = buf
        native.ui_chart_set_points(self._native, buf, len(values))


class Gauge(Widget):
    """Radial gauge (TinyCLR-style)."""
    def __init__(self, parent, width, height):
        h = native.out_handle()
        native.ui_gauge_create(parent._native, int(width), int(height), native.ref(h))
        super().__init__(h.value)

    def SetRange(self, min_value, max_value):
        native.ui_gauge_set_range(self._native, int(min_value), int(max_value))

    @property
    def Value(self): raise AttributeError("Value is write-only")
    @Value.setter
    def Value(self, v): native.ui_gauge_set_value(self._native, int(v))

    @property
    def SweepAngle(self): raise AttributeError("SweepAngle is write-only")
    @SweepAngle.setter
    def SweepAngle(self, deg): native.ui_gauge_set_sweep(self._native, int(deg))

    def SetNeedleShape(self, center_y, length):
        native.ui_gauge_set_needle_shape(self._native, int(center_y * 1000), int(length * 1000))

    @property
    def EnableScale(self): raise AttributeError("EnableScale is write-only")
    @EnableScale.setter
    def EnableScale(self, v): native.ui_gauge_show_scale(self._native, 1 if v else 0)


class Border(Widget):
    """Container with WPF-style Thickness / BorderBrush / CornerRadius /
    Padding. Set Background via the inherited BackgroundColor."""
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_border_create(parent._native, native.ref(h))
        super().__init__(h.value)

    @property
    def Thickness(self): raise AttributeError("Thickness is write-only")
    @Thickness.setter
    def Thickness(self, px): native.ui_border_set_thickness(self._native, int(px))

    @property
    def BorderBrush(self): raise AttributeError("BorderBrush is write-only")
    @BorderBrush.setter
    def BorderBrush(self, rgb): native.ui_border_set_brush(self._native, int(rgb))

    @property
    def CornerRadius(self): raise AttributeError("CornerRadius is write-only")
    @CornerRadius.setter
    def CornerRadius(self, px): native.ui_border_set_corner_radius(self._native, int(px))

    @property
    def Padding(self): raise AttributeError("Padding is write-only")
    @Padding.setter
    def Padding(self, px): native.ui_border_set_padding(self._native, int(px))


def _subscribe(widget, native_subscribe, event):
    """Register one trampoline for `event`, bind it to the given native
    endpoint_ui_subscribe_* function."""
    def trampoline(_context):
        event._fire()
    cb = UiEventCallback(trampoline)
    widget._event_trampolines.append(cb)
    native_subscribe(widget._native, cb, None)


class Button(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_button_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.Clicked = _Event()
        _subscribe(self, native.ui_subscribe_clicked, self.Clicked)

    @property
    def Text(self): raise AttributeError("Text is write-only")
    @Text.setter
    def Text(self, value):
        native.ui_button_set_text(self._native, value.encode("utf-8"))

    @property
    def BackgroundColor(self): raise AttributeError("BackgroundColor is write-only")
    @BackgroundColor.setter
    def BackgroundColor(self, value):
        native.ui_button_set_bg_color(self._native, value)

    @property
    def TextColor(self): raise AttributeError("TextColor is write-only")
    @TextColor.setter
    def TextColor(self, value):
        native.ui_button_set_text_color(self._native, value)

    @property
    def FontSize(self): raise AttributeError("FontSize is write-only")
    @FontSize.setter
    def FontSize(self, value):
        native.ui_button_set_font_size(self._native, value)


class TextAlignment:
    """Horizontal alignment for Label.Text. Matches .NET's
    GHIElectronics.Endpoint.UI.TextAlignment enum."""
    Left = 0
    Center = 1
    Right = 2


class Label(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_label_create(parent._native, native.ref(h))
        super().__init__(h.value)

    @property
    def Text(self): raise AttributeError("Text is write-only")
    @Text.setter
    def Text(self, value):
        native.ui_label_set_text(self._native, value.encode("utf-8"))

    @property
    def Color(self): raise AttributeError("Color is write-only")
    @Color.setter
    def Color(self, value):
        native.ui_label_set_color(self._native, value)

    @property
    def FontSize(self): raise AttributeError("FontSize is write-only")
    @FontSize.setter
    def FontSize(self, value):
        native.ui_label_set_font_size(self._native, value)

    @property
    def TextAlignment(self): raise AttributeError("TextAlignment is write-only")
    @TextAlignment.setter
    def TextAlignment(self, value):
        native.ui_label_set_text_align(self._native, int(value))


class Image(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_image_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._pixels = None  # keep user bytes alive while LVGL points into them

    def SetSource(self, rgb565_pixels, width, height):
        # Hold a reference so GC doesn't free the buffer underneath LVGL.
        self._pixels = bytes(rgb565_pixels)
        ptr = (ctypes.c_uint8 * len(self._pixels)).from_buffer_copy(self._pixels)
        self._pixels_ptr = ptr  # also hold the ctypes array
        native.ui_image_set_source(self._native, ptr, len(self._pixels), width, height)


class TextBox(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_textbox_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    @property
    def Text(self):
        buf = ctypes.create_string_buffer(256)
        native.ui_textbox_get_text(self._native, buf, 256)
        return buf.value.decode("utf-8", errors="replace")

    @Text.setter
    def Text(self, value):
        native.ui_textbox_set_text(self._native, value.encode("utf-8"))

    @property
    def Placeholder(self): raise AttributeError("Placeholder is write-only")
    @Placeholder.setter
    def Placeholder(self, value):
        native.ui_textbox_set_placeholder(self._native, value.encode("utf-8"))


class Slider(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_slider_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._min = 0
        self._max = 100
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    # LVGL stores min and max as a pair, so we track them on the Python side
    # and push both whenever either changes.
    @property
    def Minimum(self):
        return self._min

    @Minimum.setter
    def Minimum(self, value):
        self._min = value
        native.ui_slider_set_range(self._native, self._min, self._max)

    @property
    def Maximum(self):
        return self._max

    @Maximum.setter
    def Maximum(self, value):
        self._max = value
        native.ui_slider_set_range(self._native, self._min, self._max)

    @property
    def Value(self):
        v = native.out_int()
        native.ui_slider_get_value(self._native, native.ref(v))
        return v.value

    @Value.setter
    def Value(self, value):
        native.ui_slider_set_value(self._native, value)


class CheckBox(Widget):
    def __init__(self, parent):
        h = native.out_handle()
        native.ui_checkbox_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    @property
    def Text(self): raise AttributeError("Text is write-only")
    @Text.setter
    def Text(self, value):
        native.ui_checkbox_set_text(self._native, value.encode("utf-8"))

    @property
    def IsChecked(self):
        v = native.out_int()
        native.ui_checkbox_get_checked(self._native, native.ref(v))
        return v.value != 0

    @IsChecked.setter
    def IsChecked(self, value):
        native.ui_checkbox_set_checked(self._native, 1 if value else 0)


class MessageBoxButton:
    """A button inside a MessageBox. Subscribe to Clicked (an _Event)."""

    def __init__(self, native_handle):
        self._native = native_handle
        self.Clicked = _Event()
        cb = UiEventCallback(lambda _ctx: self.Clicked._fire())
        self._cb = cb  # keep alive
        native.ui_subscribe_clicked(self._native, cb, None)


class MessageBox(Widget):
    """Modal dialog with title, body, and footer buttons. AddButton returns
    a handle whose Clicked event you subscribe to. Hide dismisses."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_messagebox_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._buttons = []  # keep MessageBoxButton refs alive

    def SetTitle(self, title):
        native.ui_messagebox_set_title(self._native, title.encode("utf-8"))

    def SetBody(self, body):
        native.ui_messagebox_set_body(self._native, body.encode("utf-8"))

    def AddButton(self, text):
        out = native.out_handle()
        native.ui_messagebox_add_button(self._native, text.encode("utf-8"), native.ref(out))
        btn = MessageBoxButton(out.value)
        self._buttons.append(btn)
        return btn

    def Hide(self):
        native.ui_messagebox_hide(self._native)


class ListBoxItem:
    """An item inside a ListBox. Subscribe to Clicked (an _Event)."""

    def __init__(self, native_handle):
        self._native = native_handle
        self.Clicked = _Event()
        cb = UiEventCallback(lambda _ctx: self.Clicked._fire())
        self._cb = cb
        native.ui_subscribe_clicked(self._native, cb, None)


class ListBox(Widget):
    """Vertical scrollable list. AddItem returns a handle whose Clicked
    event you subscribe to. Clear drops all items."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_listbox_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._items = []

    def AddItem(self, text):
        out = native.out_handle()
        native.ui_listbox_add_item(self._native, text.encode("utf-8"), native.ref(out))
        item = ListBoxItem(out.value)
        self._items.append(item)
        return item

    def Clear(self):
        native.ui_listbox_clear(self._native)
        self._items.clear()


class RadioButton(Widget):
    """Circular-indicator CheckBox. Grouping (mutual exclusion) is up to the
    caller — subscribe to ValueChanged on each and clear the others on select."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_radiobutton_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    @property
    def Text(self): raise AttributeError("Text is write-only")
    @Text.setter
    def Text(self, value):
        native.ui_radiobutton_set_text(self._native, value.encode("utf-8"))

    @property
    def IsChecked(self):
        v = native.out_int()
        native.ui_radiobutton_get_checked(self._native, native.ref(v))
        return v.value != 0

    @IsChecked.setter
    def IsChecked(self, value):
        native.ui_radiobutton_set_checked(self._native, 1 if value else 0)


class Dropdown(Widget):
    """Select one item from a list. ValueChanged fires on selection change."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_dropdown_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    def SetOptions(self, options):
        """Set items; iterable of strings, joined with newline internally."""
        joined = "\n".join(options)
        native.ui_dropdown_set_options(self._native, joined.encode("utf-8"))

    @property
    def Selected(self):
        v = native.out_int()
        native.ui_dropdown_get_selected(self._native, native.ref(v))
        return v.value

    @Selected.setter
    def Selected(self, value):
        native.ui_dropdown_set_selected(self._native, value)


class ProgressBar(Widget):
    """Horizontal progress bar. No events; owner drives it via Value."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_progressbar_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self._min = 0
        self._max = 100

    @property
    def Minimum(self): return self._min
    @Minimum.setter
    def Minimum(self, value):
        self._min = value
        native.ui_progressbar_set_range(self._native, self._min, self._max)

    @property
    def Maximum(self): return self._max
    @Maximum.setter
    def Maximum(self, value):
        self._max = value
        native.ui_progressbar_set_range(self._native, self._min, self._max)

    @property
    def Value(self):
        v = native.out_int()
        native.ui_progressbar_get_value(self._native, native.ref(v))
        return v.value

    @Value.setter
    def Value(self, value):
        native.ui_progressbar_set_value(self._native, value)


class Switch(Widget):
    """Binary on/off toggle. ValueChanged fires on each flip."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_switch_create(parent._native, native.ref(h))
        super().__init__(h.value)
        self.ValueChanged = _Event()
        _subscribe(self, native.ui_subscribe_value_changed, self.ValueChanged)

    @property
    def IsChecked(self):
        v = native.out_int()
        native.ui_switch_get_checked(self._native, native.ref(v))
        return v.value != 0

    @IsChecked.setter
    def IsChecked(self, value):
        native.ui_switch_set_checked(self._native, 1 if value else 0)


class Keyboard(Widget):
    """Virtual on-screen keyboard. Attach to a TextBox via SetTarget so taps
    on keys type into it. Positions itself at the bottom half of the display
    by default; override via inherited X / Y / Width / Height."""

    def __init__(self, parent):
        h = native.out_handle()
        native.ui_keyboard_create(parent._native, native.ref(h))
        super().__init__(h.value)

    def SetTarget(self, textbox):
        native.ui_keyboard_set_textbox(self._native, textbox._native)

    def ClearTarget(self):
        native.ui_keyboard_set_textbox(self._native, None)
