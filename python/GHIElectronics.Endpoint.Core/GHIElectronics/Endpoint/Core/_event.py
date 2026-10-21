"""A .NET-style event, for the Endpoint packages: handlers are added with +=
and removed with -=, as in .NET:

    controller.OnConnectionChangedEvent += handler
    controller.OnConnectionChangedEvent -= handler

The handlers run on the thread that raises the event (the library's).
"""

import threading
import traceback


class Event:
    def __init__(self):
        self._handlers = ()
        self._lock = threading.Lock()

    def __iadd__(self, handler):
        if not callable(handler):
            raise TypeError("An event handler must be a function")
        with self._lock:
            self._handlers = self._handlers + (handler,)
        return self

    def __isub__(self, handler):
        """Removes the handler (the last one added, if it was added twice, as .NET)"""
        with self._lock:
            handlers = list(self._handlers)
            for i in range(len(handlers) - 1, -1, -1):
                if handlers[i] == handler:
                    del handlers[i]
                    break
            self._handlers = tuple(handlers)
        return self

    def __call__(self, *args):
        """Raises the event (used by the library). A handler's error is printed and
        does not stop the others, as an unhandled .NET exception is reported."""
        for handler in self._handlers:
            try:
                handler(*args)
            except Exception:
                traceback.print_exc()

    def __len__(self):
        return len(self._handlers)


class _EventProperty:
    """Descriptor that gives each instance its own Event under the named
    attribute. Used as a class attribute by widgets / controllers:

        class Touch:
            OnChangedEvent = _EventProperty("OnChangedEvent")

    Then `touch.OnChangedEvent += handler` works per-instance and `+=` /
    `-=` both go through to the underlying Event.
    """
    def __init__(self, name):
        self._storage = "_evt_" + name

    def __get__(self, obj, cls=None):
        if obj is None:
            return self
        evt = obj.__dict__.get(self._storage)
        if evt is None:
            evt = Event()
            obj.__dict__[self._storage] = evt
        return evt

    def __set__(self, obj, value):
        # Needed so `obj.OnXxx += handler` works: Python computes the
        # right-hand side via __get__ + __iadd__ and then assigns it back.
        obj.__dict__[self._storage] = value
