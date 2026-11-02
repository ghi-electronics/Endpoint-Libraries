using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Displays a raster image fed as raw RGB565 pixel bytes.</summary>
public class Image : Widget {
    // Keeps the managed byte[] alive while LVGL holds a pointer into it.
    private GCHandle _pixelsHandle;

    /// <summary>Create an image inside <paramref name="parent"/>. Call <see cref="SetSource"/> to supply pixels.</summary>
    /// <param name="parent">Containing widget.</param>
    public Image(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiImageCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>
    /// Set the image from RGB565 pixel bytes (little-endian). Width and height
    /// describe the source bitmap; use <see cref="Widget.Width"/>/
    /// <see cref="Widget.Height"/> for displayed size (LVGL scales).
    /// </summary>
    public unsafe void SetSource(byte[] rgb565Pixels, int width, int height) {
        if (this._pixelsHandle.IsAllocated) this._pixelsHandle.Free();
        this._pixelsHandle = GCHandle.Alloc(rgb565Pixels, GCHandleType.Pinned);
        var ptr = (byte*)this._pixelsHandle.AddrOfPinnedObject();
        Libendpoint.Check(Libendpoint.UiImageSetSource(this.Native, ptr, rgb565Pixels.Length, width, height));
    }

    /// <summary>Release the pinned pixel buffer, then destroy the widget. Idempotent.</summary>
    public override void Dispose() {
        if (this._pixelsHandle.IsAllocated) this._pixelsHandle.Free();
        base.Dispose();
    }
}
