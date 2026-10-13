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
