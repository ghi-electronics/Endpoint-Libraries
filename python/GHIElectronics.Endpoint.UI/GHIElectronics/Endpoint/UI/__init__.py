# GHIElectronics.Endpoint.UI — widgets on top of the embedded LVGL in
# libendpoint. API mirrors the .NET package character-for-character
# (PascalCase properties and methods). Bodies raise NotImplementedError
# until the libendpoint C API (M4) is in place.
#
# Usage pattern (once implemented):
#   from GHIElectronics.Endpoint.UI import UI, Screen, Button
#   UI.Initialize()
#   screen = Screen()
#   button = Button(screen)
#   button.Text = "Click me"
#   button.Clicked += lambda e: print("clicked")
#   screen.Load()
#   UI.Run()


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

    def _fire(self, *args):
        for h in self._handlers:
            h(*args)


class UI:
    """UI subsystem lifecycle. Call Initialize() once after the display is
    enabled; call Run() to drive the event loop; Stop() to exit."""

    @staticmethod
    def Initialize():
        raise NotImplementedError  # M4

    @staticmethod
    def Run():
        raise NotImplementedError  # M4

    @staticmethod
    def Stop():
        raise NotImplementedError  # M4


class Widget:
    """Base class for every widget. Holds the native handle and shared
    geometry/visibility."""

    def __init__(self, native=None):
        self._native = native

    @property
    def X(self): raise NotImplementedError
    @X.setter
    def X(self, value): raise NotImplementedError

    @property
    def Y(self): raise NotImplementedError
    @Y.setter
    def Y(self, value): raise NotImplementedError

    @property
    def Width(self): raise NotImplementedError
    @Width.setter
    def Width(self, value): raise NotImplementedError

    @property
    def Height(self): raise NotImplementedError
    @Height.setter
    def Height(self, value): raise NotImplementedError

    @property
    def Visible(self): raise NotImplementedError
    @Visible.setter
    def Visible(self, value): raise NotImplementedError

    def Dispose(self):
        pass  # M4


class Screen(Widget):
    """A top-level container with no parent. Call Load() to make it the
    currently-displayed screen."""

    def __init__(self):
        raise NotImplementedError  # M4

    def Load(self):
        raise NotImplementedError  # M4

    @staticmethod
    def Active():
        raise NotImplementedError  # M4


class Container(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4


class Button(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4
        self.Clicked = _Event()

    @property
    def Text(self): raise NotImplementedError
    @Text.setter
    def Text(self, value): raise NotImplementedError

    @property
    def BackgroundColor(self): raise NotImplementedError
    @BackgroundColor.setter
    def BackgroundColor(self, value): raise NotImplementedError

    @property
    def TextColor(self): raise NotImplementedError
    @TextColor.setter
    def TextColor(self, value): raise NotImplementedError

    @property
    def FontSize(self): raise NotImplementedError
    @FontSize.setter
    def FontSize(self, value): raise NotImplementedError


class Label(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4

    @property
    def Text(self): raise NotImplementedError
    @Text.setter
    def Text(self, value): raise NotImplementedError

    @property
    def Color(self): raise NotImplementedError
    @Color.setter
    def Color(self, value): raise NotImplementedError

    @property
    def FontSize(self): raise NotImplementedError
    @FontSize.setter
    def FontSize(self, value): raise NotImplementedError


class Image(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4

    def SetSource(self, rgb565_pixels, width, height):
        raise NotImplementedError  # M4


class TextBox(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4
        self.ValueChanged = _Event()

    @property
    def Text(self): raise NotImplementedError
    @Text.setter
    def Text(self, value): raise NotImplementedError

    @property
    def Placeholder(self): raise NotImplementedError
    @Placeholder.setter
    def Placeholder(self, value): raise NotImplementedError


class Slider(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4
        self.ValueChanged = _Event()

    @property
    def Minimum(self): raise NotImplementedError
    @Minimum.setter
    def Minimum(self, value): raise NotImplementedError

    @property
    def Maximum(self): raise NotImplementedError
    @Maximum.setter
    def Maximum(self, value): raise NotImplementedError

    @property
    def Value(self): raise NotImplementedError
    @Value.setter
    def Value(self, value): raise NotImplementedError


class CheckBox(Widget):
    def __init__(self, parent):
        raise NotImplementedError  # M4
        self.ValueChanged = _Event()

    @property
    def Text(self): raise NotImplementedError
    @Text.setter
    def Text(self, value): raise NotImplementedError

    @property
    def IsChecked(self): raise NotImplementedError
    @IsChecked.setter
    def IsChecked(self, value): raise NotImplementedError
