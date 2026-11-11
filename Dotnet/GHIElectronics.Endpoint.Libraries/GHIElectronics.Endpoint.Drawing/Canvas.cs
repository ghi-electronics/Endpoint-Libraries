using System;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Drawing {
    /// <summary>
    /// A pixel buffer plus the primitives to draw into it. Pick the
    /// size and <see cref="PixelFormat"/> at <see cref="Create"/> time;
    /// when done, hand <see cref="Buffer"/> to whichever device accepts
    /// bytes: <c>DisplayController.Flush</c>, <c>SpiDevice.Write</c>,
    /// <c>I2cDevice.Write</c>, a file, anywhere.
    /// </summary>
    /// <example>
    /// <code>
    /// using var canvas = Canvas.Create(240, 320, PixelFormat.Rgb565);
    /// canvas.Clear(Color.Black);
    /// canvas.DrawLine(10, 10, 230, 310, Color.Red, thickness: 2);
    /// canvas.FillCircle(120, 160, 40, Color.Blue);
    /// canvas.DrawText("Hello Endpoint", 10, 30, Color.White, size: 20);
    /// spi.Write(canvas.Buffer);
    /// </code>
    /// </example>
    public sealed unsafe class Canvas : IDisposable {
        private readonly byte[] buffer;
        private bool disposed;

        /// <summary>Width in pixels.</summary>
        public int Width { get; }

        /// <summary>Height in pixels.</summary>
        public int Height { get; }

        /// <summary>Pixel layout of <see cref="Buffer"/>.</summary>
        public PixelFormat PixelFormat { get; }

        /// <summary>
        /// Raw pixel bytes. Length and layout match <see cref="Width"/>,
        /// <see cref="Height"/>, and <see cref="PixelFormat"/>. Hand this
        /// to any device that accepts a byte buffer.
        /// </summary>
        public byte[] Buffer {
            get {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                return this.buffer;
            }
        }

        private Canvas(int width, int height, PixelFormat format) {
            if (width <= 0 || height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
            }
            this.Width = width;
            this.Height = height;
            this.PixelFormat = format;
            this.buffer = new byte[ByteCountFor(width, height, format)];
        }

        /// <summary>
        /// Allocate a new Canvas of the given size and pixel format.
        /// Dispose it when done to release the buffer promptly.
        /// </summary>
        public static Canvas Create(int width, int height, PixelFormat format) =>
            new Canvas(width, height, format);

        private static int ByteCountFor(int w, int h, PixelFormat f) => f switch {
            PixelFormat.Monochrome => ((w + 7) / 8) * h,
            PixelFormat.Rgb565     => w * h * 2,
            PixelFormat.Rgb888     => w * h * 3,
            PixelFormat.Argb8888   => w * h * 4,
            _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Unsupported pixel format"),
        };

        // ---------- draw primitives (managed prototype) -------------------

        /// <summary>Fill the whole buffer with a single color.</summary>
        public void Clear(Color color) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            switch (this.PixelFormat) {
                case PixelFormat.Monochrome:
                    Array.Fill(this.buffer, (byte)((color.R | color.G | color.B) != 0 ? 0xFF : 0x00));
                    break;
                case PixelFormat.Rgb565: {
                    ushort px = ToRgb565(color);
                    for (int i = 0; i < this.buffer.Length; i += 2) {
                        this.buffer[i]     = (byte)px;
                        this.buffer[i + 1] = (byte)(px >> 8);
                    }
                    break;
                }
                case PixelFormat.Rgb888:
                    for (int i = 0; i < this.buffer.Length; i += 3) {
                        this.buffer[i]     = color.R;
                        this.buffer[i + 1] = color.G;
                        this.buffer[i + 2] = color.B;
                    }
                    break;
                case PixelFormat.Argb8888:
                    for (int i = 0; i < this.buffer.Length; i += 4) {
                        this.buffer[i]     = color.A;
                        this.buffer[i + 1] = color.R;
                        this.buffer[i + 2] = color.G;
                        this.buffer[i + 3] = color.B;
                    }
                    break;
            }
        }

        /// <summary>Set a single pixel. No-op if (x, y) is outside the canvas.</summary>
        public void SetPixel(int x, int y, Color color) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (x < 0 || x >= this.Width || y < 0 || y >= this.Height) return;
            switch (this.PixelFormat) {
                case PixelFormat.Monochrome: {
                    int byteIndex = y * ((this.Width + 7) / 8) + (x / 8);
                    int bit = 7 - (x & 7);
                    if ((color.R | color.G | color.B) != 0) {
                        this.buffer[byteIndex] |= (byte)(1 << bit);
                    } else {
                        this.buffer[byteIndex] &= (byte)~(1 << bit);
                    }
                    break;
                }
                case PixelFormat.Rgb565: {
                    ushort px = ToRgb565(color);
                    int i = (y * this.Width + x) * 2;
                    this.buffer[i]     = (byte)px;
                    this.buffer[i + 1] = (byte)(px >> 8);
                    break;
                }
                case PixelFormat.Rgb888: {
                    int i = (y * this.Width + x) * 3;
                    this.buffer[i]     = color.R;
                    this.buffer[i + 1] = color.G;
                    this.buffer[i + 2] = color.B;
                    break;
                }
                case PixelFormat.Argb8888: {
                    int i = (y * this.Width + x) * 4;
                    this.buffer[i]     = color.A;
                    this.buffer[i + 1] = color.R;
                    this.buffer[i + 2] = color.G;
                    this.buffer[i + 3] = color.B;
                    break;
                }
            }
        }

        /// <summary>Bresenham line, honoring thickness by stamping an r-pixel disc at each step.</summary>
        public void DrawLine(int x0, int y0, int x1, int y1, Color color, int thickness = 1) {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true) {
                StampDisc(x0, y0, thickness / 2, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Stroke a rectangle outline.</summary>
        public void DrawRectangle(int x, int y, int width, int height, Color color, int thickness = 1) {
            for (int t = 0; t < thickness; t++) {
                DrawLine(x + t, y + t, x + width - 1 - t, y + t, color);
                DrawLine(x + t, y + height - 1 - t, x + width - 1 - t, y + height - 1 - t, color);
                DrawLine(x + t, y + t, x + t, y + height - 1 - t, color);
                DrawLine(x + width - 1 - t, y + t, x + width - 1 - t, y + height - 1 - t, color);
            }
        }

        /// <summary>Fill a rectangle.</summary>
        public void FillRectangle(int x, int y, int width, int height, Color color) {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++)
                    SetPixel(xx, yy, color);
        }

        /// <summary>Midpoint-algorithm circle outline.</summary>
        public void DrawCircle(int cx, int cy, int radius, Color color, int thickness = 1) {
            for (int t = 0; t < thickness; t++) MidpointCircle(cx, cy, radius - t, color, fill: false);
        }

        /// <summary>Filled circle.</summary>
        public void FillCircle(int cx, int cy, int radius, Color color) =>
            MidpointCircle(cx, cy, radius, color, fill: true);

        /// <summary>
        /// Draw an arc between two angles (0° = east, growing counter-clockwise).
        /// Planned; not implemented in the managed prototype. Needs LVGL
        /// native backend.
        /// </summary>
        public void DrawArc(int cx, int cy, int radius, int startDeg, int endDeg, Color color, int thickness = 1) =>
            throw new NotImplementedException("DrawArc is pending the LVGL-backed native implementation.");

        /// <summary>
        /// Rasterize <paramref name="text"/> with <paramref name="font"/> at
        /// <paramref name="sizePx"/>, with the top-left of the text box at
        /// (<paramref name="x"/>, <paramref name="y"/>). Alpha-blends against
        /// whatever is already in the canvas so text sits cleanly on top of
        /// images and primitives. The font is rasterized on every call; cache
        /// into a <see cref="Canvas"/> and re-blit if you need high FPS.
        /// </summary>
        public void DrawText(string text, Font font, int x, int y, int sizePx, Color color) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(font);
            if (this.PixelFormat != PixelFormat.Rgb565 && this.PixelFormat != PixelFormat.Monochrome) {
                throw new NotSupportedException(
                    $"DrawText into a {this.PixelFormat} canvas is not implemented (Rgb565 and Monochrome targets only).");
            }
            if (sizePx <= 0 || text.Length == 0) return;

            Libendpoint.Check(Libendpoint.FontMetrics(font.Handle, sizePx,
                                                     out int ascent, out _, out _));
            int penX = x;
            int baselineY = y + ascent;
            bool mono = this.PixelFormat == PixelFormat.Monochrome;
            bool lit = (color.R | color.G | color.B) != 0;

            // Enumerate UTF-16 code points (handles surrogate pairs for emoji etc.)
            for (int i = 0; i < text.Length; ) {
                int codepoint = char.ConvertToUtf32(text, i);
                i += char.IsHighSurrogate(text[i]) ? 2 : 1;

                Libendpoint.Check(Libendpoint.FontRasterize(font.Handle, codepoint, sizePx,
                    out byte* bmp, out int gw, out int gh, out int gxo, out int gyo, out int advance));
                try {
                    if (bmp != null && gw > 0 && gh > 0) {
                        if (mono) {
                            StampGlyphMonochrome(bmp, gw, gh, penX + gxo, baselineY + gyo, lit);
                        } else {
                            AlphaBlendGlyph(bmp, gw, gh, penX + gxo, baselineY + gyo, color);
                        }
                    }
                }
                finally {
                    if (bmp != null) Libendpoint.ImageFree(bmp);
                }
                penX += advance;
            }
        }

        /// <summary>
        /// Blit a pre-decoded <see cref="Image"/> into this canvas at
        /// (<paramref name="x"/>, <paramref name="y"/>). The image's internal
        /// pixels are converted to this canvas's <see cref="PixelFormat"/> on
        /// the fly, with no re-decode and no cached intermediate. Prefer this over
        /// <see cref="DrawImage(byte[], int, int)"/> in redraw loops: decoding
        /// a JPG is ~150 ms; blitting a cached Image is a few ms.
        /// </summary>
        public void DrawImage(Image image, int x, int y) {
            ArgumentNullException.ThrowIfNull(image);
            DrawImage(image.Pixels, x, y, image.Width, image.Height, image.PixelFormat);
        }

        /// <summary>
        /// Decode an image file (JPEG / PNG / BMP / GIF / TGA; the format is
        /// detected from the file's magic bytes) and blit it into this canvas
        /// at (<paramref name="x"/>, <paramref name="y"/>). Clips to the canvas
        /// bounds. The decoder runs inside <c>libendpoint</c>; callers hand
        /// over the raw file bytes and never see a pixel format or stride.
        /// Convenience one-shot. For repeated draws, cache the decode via
        /// <see cref="Image.Load"/> and call <see cref="DrawImage(Image, int, int)"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// using var stream = Assembly.GetExecutingAssembly()
        ///     .GetManifestResourceStream("MyApp.Resources.background.jpg");
        /// var bytes = new byte[stream.Length];
        /// stream.ReadExactly(bytes);
        /// canvas.DrawImage(bytes, 0, 0);
        /// </code>
        /// </example>
        public void DrawImage(byte[] fileBytes, int x, int y) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArgumentNullException.ThrowIfNull(fileBytes);
            if (fileBytes.Length == 0) {
                throw new ArgumentException("Image file bytes are empty.", nameof(fileBytes));
            }

            byte* pixels;
            int w, h;
            fixed (byte* src = fileBytes) {
                Libendpoint.Check(Libendpoint.ImageDecode(src, fileBytes.Length, out pixels, out w, out h));
            }
            try {
                // Hand the decoded RGB888 buffer to the lower-level DrawImage
                // overload. One extra copy, but keeps the fast path (already
                // decoded pixels) first-class for callers that need it.
                var managed = new byte[w * h * 3];
                System.Runtime.InteropServices.Marshal.Copy((nint)pixels, managed, 0, managed.Length);
                DrawImage(managed, x, y, w, h, PixelFormat.Rgb888);
            }
            finally {
                Libendpoint.ImageFree(pixels);
            }
        }

        /// <summary>
        /// Blit a decoded pixel buffer into this canvas at (x, y). The source
        /// may extend outside the canvas on any side; pixels that fall
        /// outside are clipped. Prefer <see cref="DrawImage(byte[], int, int)"/>
        /// if you have raw file bytes and want the library to decode; this
        /// overload is for callers that already have pixels (e.g. from a
        /// framebuffer or a cached glyph atlas).
        /// </summary>
        public void DrawImage(byte[] pixels, int x, int y, int width, int height, PixelFormat format) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArgumentNullException.ThrowIfNull(pixels);
            if (width <= 0 || height <= 0) return;
            if (this.PixelFormat != PixelFormat.Rgb565) {
                throw new NotSupportedException(
                    $"DrawImage into a {this.PixelFormat} canvas is not implemented in the managed prototype (Rgb565 target only).");
            }
            int srcBpp = format switch {
                PixelFormat.Rgb565 => 2,
                PixelFormat.Rgb888 => 3,
                _ => throw new NotSupportedException(
                    $"DrawImage source format {format} is not implemented in the managed prototype (Rgb565 and Rgb888 only).")
            };
            if (pixels.Length < width * height * srcBpp) {
                throw new ArgumentException("Pixel buffer is smaller than width * height * bytes-per-pixel.");
            }

            // Clip the (x, y, width, height) rectangle to the canvas bounds.
            int srcX = Math.Max(0, -x);
            int srcY = Math.Max(0, -y);
            int dstX = x + srcX;
            int dstY = y + srcY;
            int clipW = Math.Min(width  - srcX, this.Width  - dstX);
            int clipH = Math.Min(height - srcY, this.Height - dstY);
            if (clipW <= 0 || clipH <= 0) return;

            int srcStride = width * srcBpp;
            int dstStride = this.Width * 2;

            for (int row = 0; row < clipH; row++) {
                int srcRow = (srcY + row) * srcStride + srcX * srcBpp;
                int dstRow = (dstY + row) * dstStride + dstX * 2;
                if (format == PixelFormat.Rgb565) {
                    System.Buffer.BlockCopy(pixels, srcRow, this.buffer, dstRow, clipW * 2);
                }
                else {
                    for (int col = 0; col < clipW; col++) {
                        byte r = pixels[srcRow + col * 3 + 0];
                        byte g = pixels[srcRow + col * 3 + 1];
                        byte b = pixels[srcRow + col * 3 + 2];
                        ushort px = (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
                        this.buffer[dstRow + col * 2 + 0] = (byte)px;
                        this.buffer[dstRow + col * 2 + 1] = (byte)(px >> 8);
                    }
                }
            }
        }

        // ---------- internals --------------------------------------------

        private void StampDisc(int cx, int cy, int r, Color color) {
            if (r <= 0) { SetPixel(cx, cy, color); return; }
            int r2 = r * r;
            for (int yy = -r; yy <= r; yy++)
                for (int xx = -r; xx <= r; xx++)
                    if (xx * xx + yy * yy <= r2) SetPixel(cx + xx, cy + yy, color);
        }

        private void MidpointCircle(int cx, int cy, int r, Color color, bool fill) {
            if (r <= 0) { SetPixel(cx, cy, color); return; }
            int x = r, y = 0, err = 1 - r;
            while (x >= y) {
                if (fill) {
                    for (int i = cx - x; i <= cx + x; i++) { SetPixel(i, cy + y, color); SetPixel(i, cy - y, color); }
                    for (int i = cx - y; i <= cx + y; i++) { SetPixel(i, cy + x, color); SetPixel(i, cy - x, color); }
                } else {
                    SetPixel(cx + x, cy + y, color); SetPixel(cx - x, cy + y, color);
                    SetPixel(cx + x, cy - y, color); SetPixel(cx - x, cy - y, color);
                    SetPixel(cx + y, cy + x, color); SetPixel(cx - y, cy + x, color);
                    SetPixel(cx + y, cy - x, color); SetPixel(cx - y, cy - x, color);
                }
                y++;
                if (err <= 0) { err += 2 * y + 1; }
                else           { x--; err += 2 * (y - x) + 1; }
            }
        }

        // Alpha-blends an 8-bit grayscale glyph bitmap (the alpha channel of
        // the text pixel) over the current Rgb565 canvas contents at the given
        // top-left position. Clipping is handled per-pixel; glyphs that run
        // off the edge of the canvas are safe to pass in.
        private void AlphaBlendGlyph(byte* glyph, int gw, int gh, int dstX, int dstY, Color color) {
            int srcX0 = Math.Max(0, -dstX);
            int srcY0 = Math.Max(0, -dstY);
            int dstX0 = dstX + srcX0;
            int dstY0 = dstY + srcY0;
            int clipW = Math.Min(gw - srcX0, this.Width  - dstX0);
            int clipH = Math.Min(gh - srcY0, this.Height - dstY0);
            if (clipW <= 0 || clipH <= 0) return;

            int srcR = color.R, srcG = color.G, srcB = color.B;
            int dstStride = this.Width * 2;

            for (int row = 0; row < clipH; row++) {
                int srcRow = (srcY0 + row) * gw + srcX0;
                int dstRow = (dstY0 + row) * dstStride + dstX0 * 2;
                for (int col = 0; col < clipW; col++) {
                    int a = glyph[srcRow + col];
                    if (a == 0) continue;
                    int dstOff = dstRow + col * 2;
                    if (a == 255) {
                        ushort px = (ushort)(((srcR & 0xF8) << 8) | ((srcG & 0xFC) << 3) | (srcB >> 3));
                        this.buffer[dstOff]     = (byte)px;
                        this.buffer[dstOff + 1] = (byte)(px >> 8);
                    }
                    else {
                        // Unpack existing RGB565 pixel, blend, pack back.
                        ushort old = (ushort)(this.buffer[dstOff] | (this.buffer[dstOff + 1] << 8));
                        int oldR = ((old >> 11) & 0x1F) << 3;
                        int oldG = ((old >> 5)  & 0x3F) << 2;
                        int oldB = (old        & 0x1F) << 3;
                        int inv  = 255 - a;
                        int r = (srcR * a + oldR * inv) / 255;
                        int g = (srcG * a + oldG * inv) / 255;
                        int b = (srcB * a + oldB * inv) / 255;
                        ushort px = (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
                        this.buffer[dstOff]     = (byte)px;
                        this.buffer[dstOff + 1] = (byte)(px >> 8);
                    }
                }
            }
        }

        // Stamp an 8-bit grayscale glyph bitmap into a 1bpp Monochrome canvas.
        // Thresholds alpha: >= 128 sets/clears the bit (depending on whether
        // the text color is non-black). Anti-aliasing is meaningless at 1bpp.
        private void StampGlyphMonochrome(byte* glyph, int gw, int gh, int dstX, int dstY, bool lit) {
            int srcX0 = Math.Max(0, -dstX);
            int srcY0 = Math.Max(0, -dstY);
            int dstX0 = dstX + srcX0;
            int dstY0 = dstY + srcY0;
            int clipW = Math.Min(gw - srcX0, this.Width  - dstX0);
            int clipH = Math.Min(gh - srcY0, this.Height - dstY0);
            if (clipW <= 0 || clipH <= 0) return;

            int rowBytes = (this.Width + 7) / 8;
            for (int row = 0; row < clipH; row++) {
                int srcRow = (srcY0 + row) * gw + srcX0;
                int dstRow = (dstY0 + row) * rowBytes;
                for (int col = 0; col < clipW; col++) {
                    if (glyph[srcRow + col] < 128) continue;
                    int x = dstX0 + col;
                    int bit = 7 - (x & 7);
                    int off = dstRow + (x >> 3);
                    if (lit) this.buffer[off] |= (byte)(1 << bit);
                    else     this.buffer[off] &= (byte)~(1 << bit);
                }
            }
        }

        // Pack an sRGB Color into 5-6-5. Stored little-endian (LSB first)
        // to match /dev/fb0 on little-endian ARM, so DisplayController.Flush
        // can pass the buffer straight through. SPI TFT controllers that
        // expect MSB-first (ILI9341, ST7735) need a byte swap on write.
        private static ushort ToRgb565(Color c) =>
            (ushort)(((c.R & 0xF8) << 8) | ((c.G & 0xFC) << 3) | (c.B >> 3));

        /// <summary>Release the pixel buffer.</summary>
        public void Dispose() {
            if (!this.disposed) {
                this.disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
