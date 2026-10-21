// Base class for every UI widget. Holds the opaque libendpoint handle and
// exposes geometry/visibility shared by all widget types.

using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public abstract class Widget : IDisposable {
    internal nint Native;
    private readonly List<GCHandle> _eventHandles = new();
    private bool _disposed;

    private protected Widget(nint native) {
        this.Native = native;
    }

    public int X {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out var x, out _)); return x; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetPos(this.Native, value, this.Y)); }
    }

    public int Y {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out _, out var y)); return y; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetPos(this.Native, this.X, value)); }
    }

    public int Width {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out var w, out _)); return w; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetSize(this.Native, value, this.Height)); }
    }

    public int Height {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out _, out var h)); return h; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetSize(this.Native, this.Width, value)); }
    }

    public bool Visible {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetVisible(this.Native, out var v)); return v != 0; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetVisible(this.Native, value ? 1 : 0)); }
    }

    public virtual void Dispose() {
        if (this._disposed) return;
        this._disposed = true;
        if (this.Native != 0) {
            Libendpoint.UiWidgetDestroy(this.Native);
            this.Native = 0;
        }
        foreach (var h in this._eventHandles) if (h.IsAllocated) h.Free();
        this._eventHandles.Clear();
        GC.SuppressFinalize(this);
    }

    // Called by subclasses when they wire a native event trampoline. The
    // GCHandle keeps the managed Action alive for the lifetime of the widget;
    // Dispose frees them all.
    private protected void TrackEventHandle(GCHandle handle) => this._eventHandles.Add(handle);
}
