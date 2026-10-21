// Base class for every UI widget. Holds the opaque libendpoint handle and
// exposes geometry/visibility shared by all widget types.

using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public enum GradientDirection { Vertical = 0, Horizontal = 1 }

public abstract class Widget : IDisposable {
    internal nint Native;
    private readonly List<GCHandle> _eventHandles = new();
    private bool _disposed;

    private protected Widget(nint native) {
        this.Native = native;
    }

    // Setters forward to the single-axis C API: LVGL's getters return 0 until
    // the first layout pass, so a read-then-write-both would zero out the
    // untouched axis.
    public int X {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out var x, out _)); return x; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetX(this.Native, value)); }
    }

    public int Y {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out _, out var y)); return y; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetY(this.Native, value)); }
    }

    public int Width {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out var w, out _)); return w; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetWidth(this.Native, value)); }
    }

    public int Height {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out _, out var h)); return h; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetHeight(this.Native, value)); }
    }

    public bool Visible {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetVisible(this.Native, out var v)); return v != 0; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetVisible(this.Native, value ? 1 : 0)); }
    }

    // Solid background color (0x00RRGGBB).
    public uint BackgroundColor {
        set => Libendpoint.Check(Libendpoint.UiWidgetSetBgColor(this.Native, value));
    }

    /// <summary>Linear gradient background. direction: Vertical = top->bottom, Horizontal = left->right.</summary>
    public void SetBackgroundGradient(uint rgbStart, uint rgbEnd, GradientDirection direction = GradientDirection.Vertical) {
        Libendpoint.Check(Libendpoint.UiWidgetSetBgGradient(this.Native, rgbStart, rgbEnd, (int)direction));
    }

    /// <summary>Tiled background from raw RGB565 pixel bytes (little-endian).</summary>
    public unsafe void SetBackgroundImage(byte[] rgb565Pixels, int width, int height) {
        // Keep the managed buffer alive for the widget's lifetime (LVGL holds
        // a pointer into it). The GCHandle is freed in Dispose.
        var handle = GCHandle.Alloc(rgb565Pixels, GCHandleType.Pinned);
        this.TrackEventHandle(handle);  // reuse the handle list so Dispose frees it
        var p = (byte*)handle.AddrOfPinnedObject();
        Libendpoint.Check(Libendpoint.UiWidgetSetBgImage(this.Native, p, rgb565Pixels.Length, width, height));
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
