using System;

namespace GHIElectronics.Endpoint.UI.Drawing {
    /// <summary>
    /// A pixel buffer plus the primitives to draw into it. Pick the
    /// size and <see cref="PixelFormat"/> at <see cref="Create"/> time;
    /// when done, hand <see cref="Buffer"/> to whichever device accepts
    /// bytes — <c>DisplayController.Flush</c>, <c>SpiDevice.Write</c>,
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
    public sealed class Canvas : IDisposable {
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
                        this.buffer[i]     = (byte)(px >> 8);
                        this.buffer[i + 1] = (byte)px;
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
                    this.buffer[i]     = (byte)(px >> 8);
                    this.buffer[i + 1] = (byte)px;
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
        /// Planned — not implemented in the managed prototype; needs LVGL
        /// native backend.
        /// </summary>
        public void DrawArc(int cx, int cy, int radius, int startDeg, int endDeg, Color color, int thickness = 1) =>
            throw new NotImplementedException("DrawArc is pending the LVGL-backed native implementation.");

        /// <summary>
        /// Rasterize text using one of the built-in Montserrat sizes
        /// (14, 20, 32). Planned — not implemented in the managed
        /// prototype; needs LVGL native backend.
        /// </summary>
        public void DrawText(string text, int x, int y, Color color, int size) =>
            throw new NotImplementedException("DrawText is pending the LVGL-backed native implementation.");

        /// <summary>
        /// Blit an image buffer of the given source format into this canvas at (x, y).
        /// Planned — not implemented in the managed prototype.
        /// </summary>
        public void DrawImage(byte[] pixels, int x, int y, int width, int height, PixelFormat format) =>
            throw new NotImplementedException("DrawImage is pending the LVGL-backed native implementation.");

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

        // Pack an sRGB Color into 5-6-5 big-endian — big-endian matches
        // most SPI TFT controllers (ILI9341, ST7735) and the on-board panel
        // when it's wired MSB-first.
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
