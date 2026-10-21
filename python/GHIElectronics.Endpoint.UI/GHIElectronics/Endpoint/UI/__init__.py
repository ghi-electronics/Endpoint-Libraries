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
        native.ui_widget_set_pos(self._native, value, self.Y)

    @property
    def Y(self):
        x, y = native.out_int(), native.out_int()
        native.ui_widget_get_pos(self._native, native.ref(x), native.ref(y))
        return y.value

    @Y.setter
    def Y(self, value):
        native.ui_widget_set_pos(self._native, self.X, value)

    @property
    def Width(self):
        w, h = native.out_int(), native.out_int()
        native.ui_widget_get_size(self._native, native.ref(w), native.ref(h))
        return w.value

    @Width.setter
    def Width(self, value):
        native.ui_widget_set_size(self._native, value, self.Height)

    @property
    def Height(self):
        w, h = native.out_int(), native.out_int()
        native.ui_widget_get_size(self._native, native.ref(w), native.ref(h))
        return h.value

    @Height.setter
    def Height(self, value):
        native.ui_widget_set_size(self._native, self.Width, value)

    @property
    def Visible(self):
        v = native.out_int()
        native.ui_widget_get_visible(self._native, native.ref(v))
        return v.value != 0

    @Visible.setter
    def Visible(self, value):
        native.ui_widget_set_visible(self._native, 1 if value else 0)

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
