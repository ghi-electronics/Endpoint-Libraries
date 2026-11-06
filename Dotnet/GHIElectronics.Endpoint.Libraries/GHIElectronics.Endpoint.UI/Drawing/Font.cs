using System;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI.Drawing {
    /// <summary>
    /// A TrueType / OpenType font loaded from raw file bytes. Pass to
    /// <see cref="Canvas.DrawText"/> to render strings at any pixel size.
    /// </summary>
    /// <example>
    /// <code>
    /// // Standard .NET: &lt;EmbeddedResource Include="Resources\Montserrat.ttf" /&gt;
    /// using var stream = Assembly.GetExecutingAssembly()
    ///     .GetManifestResourceStream("MyApp.Resources.Montserrat.ttf");
    /// var bytes = new byte[stream.Length];
    /// stream.ReadExactly(bytes);
    /// using var font = Font.Load(bytes);
    /// canvas.DrawText("Hello Endpoint", font, x: 20, y: 60, sizePx: 32, Color.White);
    /// </code>
    /// </example>
    public sealed unsafe class Font : IDisposable {
        private nint handle;

        private Font(nint handle) {
            this.handle = handle;
        }

        internal nint Handle {
            get {
                ObjectDisposedException.ThrowIf(this.handle == 0, this);
                return this.handle;
            }
        }

        /// <summary>
        /// Loads a TTF / OTF font from raw file bytes. The bytes are copied
        /// into the native font handle, so the caller's buffer can be
        /// released as soon as this returns.
        /// </summary>
        public static Font Load(byte[] ttfBytes) {
            ArgumentNullException.ThrowIfNull(ttfBytes);
            if (ttfBytes.Length == 0) {
                throw new ArgumentException("Font bytes are empty.", nameof(ttfBytes));
            }
            fixed (byte* p = ttfBytes) {
                Libendpoint.Check(Libendpoint.FontInit(p, ttfBytes.Length, out var h));
                return new Font(h);
            }
        }

        /// <summary>
        /// Font-wide vertical metrics at the given pixel height. Ascent is the
        /// distance from the baseline to the top of the tallest glyph; descent
        /// is negative (below the baseline). Line height = ascent - descent + lineGap.
        /// </summary>
        public (int Ascent, int Descent, int LineGap) GetMetrics(int sizePx) {
            Libendpoint.Check(Libendpoint.FontMetrics(this.Handle, sizePx, out var a, out var d, out var g));
            return (a, d, g);
        }

        /// <summary>Releases the native font handle.</summary>
        public void Dispose() {
            var h = this.handle;
            if (h != 0) {
                this.handle = 0;
                Libendpoint.FontFree(h);
            }
            GC.SuppressFinalize(this);
        }

        ~Font() {
            if (this.handle != 0) {
                Libendpoint.FontFree(this.handle);
            }
        }
    }
}
