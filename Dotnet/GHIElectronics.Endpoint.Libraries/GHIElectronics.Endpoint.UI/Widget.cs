// Base class for every UI widget. Holds the opaque libendpoint handle and
// exposes geometry/visibility shared by all widget types.
//
// Widgets are not standalone — every widget except Screen requires a parent
// (another Widget) at construction time. The UI subsystem must be
// initialized before any widget is created; use UI.Initialize() once at
// startup (M4 — not implemented yet).

namespace GHIElectronics.Endpoint.UI;

public abstract class Widget : IDisposable {
    internal IntPtr Native;
    private bool _disposed;

    private protected Widget(IntPtr native) {
        this.Native = native;
    }

    public int X {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int Y {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int Width {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int Height {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public bool Visible {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public virtual void Dispose() {
        if (this._disposed) return;
        this._disposed = true;
        // M4: call libendpoint to destroy the native widget.
        GC.SuppressFinalize(this);
    }
}
