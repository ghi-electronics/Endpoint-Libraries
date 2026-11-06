using System;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// A TrueType / OpenType font loaded from raw file bytes, ready to assign
/// to any widget's <see cref="Widget.Font"/> property. Font bytes are copied
/// into native storage at <see cref="Load"/>; the caller can release their
/// <c>byte[]</c> as soon as the call returns. The same <c>Font</c> can be
/// assigned to any number of widgets.
/// </summary>
/// <example>
/// <code>
/// // Standard .NET: &lt;EmbeddedResource Include="Resources\Montserrat.ttf" /&gt;
/// using var stream = Assembly.GetExecutingAssembly()
///     .GetManifestResourceStream("MyApp.Resources.Montserrat.ttf");
/// var bytes = new byte[stream.Length];
/// stream.ReadExactly(bytes);
/// using var font = Font.Load(bytes, sizePx: 24);
/// button.Font  = font;
/// label.Font   = font;
/// </code>
/// </example>
public sealed unsafe class Font : IDisposable {
    private nint _handle;

    private Font(nint handle) { this._handle = handle; }

    internal nint Handle {
        get {
            ObjectDisposedException.ThrowIf(this._handle == 0, this);
            return this._handle;
        }
    }

    /// <summary>
    /// Load a TTF / OTF font at the given pixel height. Font is rasterized
    /// on demand per glyph by LVGL's built-in tiny_ttf decoder; results are
    /// cached inside the font so repeated glyphs are free.
    /// </summary>
    public static Font Load(byte[] ttfBytes, int sizePx) {
        ArgumentNullException.ThrowIfNull(ttfBytes);
        if (ttfBytes.Length == 0) {
            throw new ArgumentException("Font bytes are empty.", nameof(ttfBytes));
        }
        if (sizePx <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sizePx), sizePx, "Size must be positive.");
        }
        fixed (byte* p = ttfBytes) {
            Libendpoint.Check(Libendpoint.UiFontLoadTtf(p, ttfBytes.Length, sizePx, out var h));
            return new Font(h);
        }
    }

    /// <summary>
    /// Release the native font. Do NOT dispose while any widget still uses
    /// it — call after <c>UI.Stop()</c> returns, or let the process exit.
    /// </summary>
    public void Dispose() {
        var h = this._handle;
        if (h != 0) {
            this._handle = 0;
            Libendpoint.UiFontFree(h);
        }
        GC.SuppressFinalize(this);
    }

    ~Font() {
        if (this._handle != 0) Libendpoint.UiFontFree(this._handle);
    }
}
