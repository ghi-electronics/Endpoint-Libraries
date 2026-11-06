// Base class for every UI widget. Holds the opaque libendpoint handle and
// exposes geometry/visibility shared by all widget types.

using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Direction of a linear background gradient.</summary>
public enum GradientDirection {
    /// <summary>Top to bottom.</summary>
    Vertical = 0,
    /// <summary>Left to right.</summary>
    Horizontal = 1
}

/// <summary>
/// Abstract base class for every UI widget. Holds the opaque libendpoint
/// handle and exposes geometry, visibility, and background styling shared by
/// all widget types. All setters and events run on the UI thread; do not
/// touch widgets from background threads (LVGL is built with
/// <c>LV_USE_OS=LV_OS_NONE</c>).
/// </summary>
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
    /// <summary>X coordinate in pixels relative to the parent's top-left corner.</summary>
    public int X {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out var x, out _)); return x; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetX(this.Native, value)); }
    }

    /// <summary>Y coordinate in pixels relative to the parent's top-left corner.</summary>
    public int Y {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetPos(this.Native, out _, out var y)); return y; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetY(this.Native, value)); }
    }

    /// <summary>Width in pixels.</summary>
    public int Width {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out var w, out _)); return w; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetWidth(this.Native, value)); }
    }

    /// <summary>Height in pixels.</summary>
    public int Height {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetSize(this.Native, out _, out var h)); return h; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetHeight(this.Native, value)); }
    }

    /// <summary>Whether the widget (and its subtree) is drawn. Setting <c>false</c> hides but does not destroy it.</summary>
    public bool Visible {
        get { Libendpoint.Check(Libendpoint.UiWidgetGetVisible(this.Native, out var v)); return v != 0; }
        set { Libendpoint.Check(Libendpoint.UiWidgetSetVisible(this.Native, value ? 1 : 0)); }
    }

    private uint _backgroundColor;
    /// <summary>
    /// Solid background color as <c>0x00RRGGBB</c>. Getter returns the last
    /// value set (LVGL has no read-back); returns <c>0</c> if never set.
    /// </summary>
    public virtual uint BackgroundColor {
        get => this._backgroundColor;
        set { this._backgroundColor = value; Libendpoint.Check(Libendpoint.UiWidgetSetBgColor(this.Native, value)); }
    }

    /// <summary>Linear gradient background. direction: Vertical = top->bottom, Horizontal = left->right.</summary>
    public void SetBackgroundGradient(uint rgbStart, uint rgbEnd, GradientDirection direction = GradientDirection.Vertical) {
        Libendpoint.Check(Libendpoint.UiWidgetSetBgGradient(this.Native, rgbStart, rgbEnd, (int)direction));
    }

    /// <summary>WPF-style per-widget Margin. Works in any parent (flex, dock, or absolute).</summary>
    public void SetMargin(int left, int top, int right, int bottom) {
        Libendpoint.Check(Libendpoint.UiWidgetSetMargin(this.Native, left, top, right, bottom));
    }

    /// <summary>
    /// Background image from raw RGB565 pixel bytes (little-endian). The
    /// library copies the bytes into native storage, so the caller's buffer
    /// can be released as soon as this returns. The widget renders the
    /// image at its native size (no tiling, no scaling). The widget's own
    /// solid background is turned off so the image shows through.
    /// </summary>
    public unsafe void SetBackgroundImage(byte[] rgb565Pixels, int width, int height) {
        ArgumentNullException.ThrowIfNull(rgb565Pixels);
        fixed (byte* p = rgb565Pixels) {
            Libendpoint.Check(Libendpoint.UiWidgetSetBgImage(this.Native, p, rgb565Pixels.Length, width, height));
        }
    }

    /// <summary>
    /// Background image from a decoded <see cref="Drawing.Image"/> (JPG/PNG/BMP/…
    /// handed to <see cref="Drawing.Image.Load"/>). The image's RGB888 pixels
    /// are converted to RGB565 and copied into native storage; the same
    /// <c>Image</c> stays reusable for Canvas calls after this returns.
    /// </summary>
    public unsafe void SetBackgroundImage(Drawing.Image image) {
        ArgumentNullException.ThrowIfNull(image);
        if (image.PixelFormat != Drawing.PixelFormat.Rgb888) {
            throw new NotSupportedException(
                $"SetBackgroundImage(Image): source format {image.PixelFormat} not yet supported (Rgb888 only).");
        }
        var src = image.Pixels;
        int w = image.Width, h = image.Height;
        var rgb565 = new byte[w * h * 2];
        for (int i = 0, j = 0; i < src.Length; i += 3, j += 2) {
            byte r = src[i], g = src[i + 1], b = src[i + 2];
            ushort px = (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
            rgb565[j]     = (byte)px;
            rgb565[j + 1] = (byte)(px >> 8);
        }
        SetBackgroundImage(rgb565, w, h);
    }

    /// <summary>
    /// Custom font for any text this widget renders (Button caption, Label
    /// text, Chart axis labels, …). Loaded once via <see cref="Font.Load"/>;
    /// the same <c>Font</c> can be assigned to any number of widgets.
    /// </summary>
    public Font? Font {
        set {
            Libendpoint.Check(Libendpoint.UiWidgetSetFont(this.Native,
                value is null ? 0 : value.Handle));
        }
    }

    /// <summary>Destroy the native widget and release pinned event handles. Idempotent.</summary>
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
