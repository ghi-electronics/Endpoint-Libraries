"""GHIElectronics.Endpoint.UI.Drawing — off-screen 2D drawing primitives.
API mirrors the .NET package character-for-character (PascalCase).

    from GHIElectronics.Endpoint.UI.Drawing import Canvas, Color, PixelFormat

    canvas = Canvas.Create(240, 320, PixelFormat.Rgb565)
    canvas.Clear(Color.Black)
    canvas.DrawLine(10, 10, 230, 310, Color.Red, 2)
    canvas.FillCircle(120, 160, 40, Color.Blue)
    spi.Write(canvas.Buffer)

The Canvas drawing primitives run in pure Python against an in-memory
bytearray; Image / Font delegate to the stb_image + stb_truetype decoders
inside libendpoint. Hand `Canvas.Buffer` to any device that accepts raw
bytes (DisplayController.Flush, SpiDevice.Write, I2cDevice.Write, ...)."""

import ctypes
import math

from GHIElectronics.Endpoint.Core._native import native
from GHIElectronics.Endpoint.Core._errors import (
    ArgumentException, ArgumentOutOfRangeException,
    NotSupportedException, NotImplementedException, ObjectDisposedException,
)


__all__ = ["Canvas", "Color", "Font", "Image", "PixelFormat"]


# ------------------------------------------------------------------
# PixelFormat
# ------------------------------------------------------------------

class PixelFormat:
    """Pixel layout of a Canvas buffer. Values mirror the .NET enum."""

    Monochrome = 1  # 1 bpp, MSB-first packed (SSD1306 / SH1106 OLEDs)
    Rgb565 = 2      # 16 bpp little-endian (on-board panel, most SPI TFTs)
    Rgb888 = 3      # 24 bpp R-G-B byte order (parallel RGB panels @ 24 bpp)
    Argb8888 = 4    # 32 bpp A-R-G-B byte order (off-screen compositing)


# ------------------------------------------------------------------
# Color
# ------------------------------------------------------------------

class Color:
    """A 32-bit color with 8 bits each of alpha, red, green, blue. Canvas
    maps it to its PixelFormat at draw time."""

    __slots__ = ("R", "G", "B", "A")

    def __init__(self, r, g, b, a=0xFF):
        self.R = int(r) & 0xFF
        self.G = int(g) & 0xFF
        self.B = int(b) & 0xFF
        self.A = int(a) & 0xFF

    @staticmethod
    def FromArgb(argb):
        """Color from a 0xAARRGGBB value."""
        argb = int(argb)
        return Color((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF, (argb >> 24) & 0xFF)

    @staticmethod
    def FromRgb(rgb):
        """Color from a 0xRRGGBB value, opaque."""
        rgb = int(rgb)
        return Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)


# Common colors — names match System.Drawing / CSS where they overlap.
# Attached after the class body to sidestep the forward-reference problem.
Color.Transparent = Color(0, 0, 0, 0)
Color.Black       = Color(0x00, 0x00, 0x00)
Color.White       = Color(0xFF, 0xFF, 0xFF)
Color.Red         = Color(0xFF, 0x00, 0x00)
Color.Green       = Color(0x00, 0xFF, 0x00)
Color.Blue        = Color(0x00, 0x00, 0xFF)
Color.Yellow      = Color(0xFF, 0xFF, 0x00)
Color.Cyan        = Color(0x00, 0xFF, 0xFF)
Color.Magenta     = Color(0xFF, 0x00, 0xFF)
Color.Navy        = Color(0x00, 0x00, 0x80)
Color.Maroon      = Color(0x80, 0x00, 0x00)
Color.Olive       = Color(0x80, 0x80, 0x00)
Color.Gray        = Color(0x80, 0x80, 0x80)
Color.Silver      = Color(0xC0, 0xC0, 0xC0)
Color.Orange      = Color(0xFF, 0xA5, 0x00)


# ------------------------------------------------------------------
# Image
# ------------------------------------------------------------------

class Image:
    """A decoded image (JPEG / PNG / BMP / GIF / TGA — detected from the
    file's magic bytes), ready to blit into a Canvas. Decode runs once in
    Image.Load; Canvas.DrawImage then just blits + converts."""

    def __init__(self, width, height, pixel_format, pixels):
        self.Width = width
        self.Height = height
        self.PixelFormat = pixel_format
        # Internal RGB888 pixel bytes — read by Canvas.DrawImage / Widget.
        self._pixels = pixels

    @staticmethod
    def Load(file_bytes):
        """Decode an image file into an in-memory bitmap. The returned Image
        is the format-neutral handle — pass it to Canvas.DrawImage / Widget
        .SetBackgroundImage."""
        if file_bytes is None:
            raise ArgumentException("file_bytes must not be None")
        if len(file_bytes) == 0:
            raise ArgumentException("Image file bytes are empty.")

        data = bytes(file_bytes)
        src = (ctypes.c_uint8 * len(data)).from_buffer_copy(data)
        out_pixels = ctypes.POINTER(ctypes.c_uint8)()
        out_w = ctypes.c_int()
        out_h = ctypes.c_int()
        native.image_decode(src, len(data),
                            ctypes.byref(out_pixels), ctypes.byref(out_w), ctypes.byref(out_h))
        try:
            byte_count = out_w.value * out_h.value * 3
            # Copy pixels out of native storage into a managed bytes object so
            # the native allocation can be freed immediately.
            managed = bytes(ctypes.string_at(out_pixels, byte_count))
            return Image(out_w.value, out_h.value, PixelFormat.Rgb888, managed)
        finally:
            native.image_free(out_pixels)


# ------------------------------------------------------------------
# Font
# ------------------------------------------------------------------

class Font:
    """A TrueType / OpenType font for Canvas.DrawText. Rasterizes glyphs
    on demand through stb_truetype inside libendpoint. Separate from
    GHIElectronics.Endpoint.UI.Font (which is LVGL's lv_tiny_ttf for
    widget text)."""

    def __init__(self, handle):
        self._handle = handle

    @staticmethod
    def Load(ttf_bytes):
        """Load a TTF / OTF font from raw file bytes. The bytes are copied
        into native storage, so the caller's buffer can be released as soon
        as this returns."""
        if ttf_bytes is None:
            raise ArgumentException("ttf_bytes must not be None")
        if len(ttf_bytes) == 0:
            raise ArgumentException("Font bytes are empty.")

        data = bytes(ttf_bytes)
        src = (ctypes.c_uint8 * len(data)).from_buffer_copy(data)
        out_handle = native.out_handle()
        native.font_init(src, len(data), native.ref(out_handle))
        return Font(out_handle.value)

    def GetMetrics(self, size_px):
        """Font-wide vertical metrics at the given pixel height. Returns
        (ascent, descent, line_gap). line_height = ascent - descent + line_gap."""
        if self._handle is None:
            raise ObjectDisposedException("The Font is disposed")
        a, d, g = native.out_int(), native.out_int(), native.out_int()
        native.font_metrics(self._handle, int(size_px), native.ref(a), native.ref(d), native.ref(g))
        return a.value, d.value, g.value

    def Dispose(self):
        if self._handle is not None:
            native.font_free(self._handle)
            self._handle = None

    def __del__(self):
        try: self.Dispose()
        except Exception: pass


# ------------------------------------------------------------------
# Canvas
# ------------------------------------------------------------------

def _rgb565(c):
    # Pack an sRGB Color into 5-6-5 little-endian (matches /dev/fb0 on
    # little-endian ARM; SPI TFTs expecting MSB-first need a byte swap on write).
    return ((c.R & 0xF8) << 8) | ((c.G & 0xFC) << 3) | (c.B >> 3)


def _byte_count_for(width, height, fmt):
    if fmt == PixelFormat.Monochrome: return ((width + 7) // 8) * height
    if fmt == PixelFormat.Rgb565:     return width * height * 2
    if fmt == PixelFormat.Rgb888:     return width * height * 3
    if fmt == PixelFormat.Argb8888:   return width * height * 4
    raise ArgumentOutOfRangeException(f"Unsupported pixel format: {fmt}")


class Canvas:
    """A pixel buffer plus the primitives to draw into it. Pick the size
    and PixelFormat at Create() time; when done, hand Buffer to whichever
    device accepts bytes - DisplayController.Flush, SpiDevice.Write,
    I2cDevice.Write, a file, anywhere.

    Example::

        canvas = Canvas.Create(240, 320, PixelFormat.Rgb565)
        canvas.Clear(Color.Black)
        canvas.DrawLine(10, 10, 230, 310, Color.Red, 2)
        canvas.FillCircle(120, 160, 40, Color.Blue)
        canvas.DrawText("Hello Endpoint", font, 10, 30, 20, Color.White)
        spi.Write(canvas.Buffer)
    """

    def __init__(self, width, height, pixel_format):
        if width <= 0 or height <= 0:
            raise ArgumentOutOfRangeException("Width and height must be positive.")
        self.Width = int(width)
        self.Height = int(height)
        self.PixelFormat = pixel_format
        self._buffer = bytearray(_byte_count_for(self.Width, self.Height, pixel_format))
        self._disposed = False

    @staticmethod
    def Create(width, height, pixel_format):
        """Allocate a new Canvas of the given size and pixel format."""
        return Canvas(width, height, pixel_format)

    @property
    def Buffer(self):
        """Raw pixel bytes. Length and layout match Width/Height/PixelFormat."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        return self._buffer

    # ---------- draw primitives (managed) ----------------------------------

    def Clear(self, color):
        """Fill the whole buffer with a single color."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        fmt = self.PixelFormat
        buf = self._buffer
        # Slice-assignment hands the fill to the C bytearray slot: ~100x faster
        # than a per-byte Python loop on a Cortex-A7 at 800x480.
        if fmt == PixelFormat.Monochrome:
            fill = 0xFF if (color.R | color.G | color.B) != 0 else 0x00
            buf[:] = bytes([fill]) * len(buf)
        elif fmt == PixelFormat.Rgb565:
            px = _rgb565(color)
            buf[:] = bytes([px & 0xFF, (px >> 8) & 0xFF]) * (len(buf) // 2)
        elif fmt == PixelFormat.Rgb888:
            buf[:] = bytes([color.R, color.G, color.B]) * (len(buf) // 3)
        elif fmt == PixelFormat.Argb8888:
            buf[:] = bytes([color.A, color.R, color.G, color.B]) * (len(buf) // 4)

    def SetPixel(self, x, y, color):
        """Set a single pixel. No-op if (x, y) is outside the canvas."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        if x < 0 or x >= self.Width or y < 0 or y >= self.Height:
            return
        fmt = self.PixelFormat
        buf = self._buffer
        if fmt == PixelFormat.Monochrome:
            byte_index = y * ((self.Width + 7) // 8) + (x // 8)
            bit = 7 - (x & 7)
            if (color.R | color.G | color.B) != 0:
                buf[byte_index] |= (1 << bit)
            else:
                buf[byte_index] &= (~(1 << bit)) & 0xFF
        elif fmt == PixelFormat.Rgb565:
            px = _rgb565(color)
            i = (y * self.Width + x) * 2
            buf[i] = px & 0xFF
            buf[i + 1] = (px >> 8) & 0xFF
        elif fmt == PixelFormat.Rgb888:
            i = (y * self.Width + x) * 3
            buf[i] = color.R; buf[i + 1] = color.G; buf[i + 2] = color.B
        elif fmt == PixelFormat.Argb8888:
            i = (y * self.Width + x) * 4
            buf[i] = color.A; buf[i + 1] = color.R; buf[i + 2] = color.G; buf[i + 3] = color.B

    def DrawLine(self, x0, y0, x1, y1, color, thickness=1):
        """Bresenham line, honoring thickness by stamping an r-pixel disc at each step."""
        x0 = int(x0); y0 = int(y0); x1 = int(x1); y1 = int(y1)
        dx = abs(x1 - x0); sx = 1 if x0 < x1 else -1
        dy = -abs(y1 - y0); sy = 1 if y0 < y1 else -1
        err = dx + dy
        r = thickness // 2
        while True:
            self._stamp_disc(x0, y0, r, color)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy; x0 += sx
            if e2 <= dx:
                err += dx; y0 += sy

    def DrawRectangle(self, x, y, width, height, color, thickness=1):
        """Stroke a rectangle outline."""
        if thickness <= 0 or width <= 0 or height <= 0:
            return
        t = int(thickness)
        x = int(x); y = int(y); width = int(width); height = int(height)
        # Four axis-aligned bands via FillRectangle (one scanline slice per
        # row on Rgb565): orders of magnitude faster than Bresenham DrawLine
        # for borders. Top + bottom span the full width; left + right fill
        # the remaining vertical strip so corners are not double-drawn.
        self.FillRectangle(x, y, width, t, color)
        self.FillRectangle(x, y + height - t, width, t, color)
        inner_h = height - 2 * t
        if inner_h > 0:
            self.FillRectangle(x, y + t, t, inner_h, color)
            self.FillRectangle(x + width - t, y + t, t, inner_h, color)

    def FillRectangle(self, x, y, width, height, color):
        """Fill a rectangle."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        # Clip to canvas bounds.
        x0 = max(0, int(x)); y0 = max(0, int(y))
        x1 = min(self.Width,  int(x) + int(width))
        y1 = min(self.Height, int(y) + int(height))
        if x0 >= x1 or y0 >= y1:
            return
        fmt = self.PixelFormat
        buf = self._buffer
        # Rgb565 fast path: slice-assign one scanline at a time.
        if fmt == PixelFormat.Rgb565:
            px = _rgb565(color)
            row = bytes([px & 0xFF, (px >> 8) & 0xFF]) * (x1 - x0)
            stride = self.Width * 2
            for yy in range(y0, y1):
                off = yy * stride + x0 * 2
                buf[off: off + len(row)] = row
            return
        # Fallback: per-pixel (less common formats).
        for yy in range(y0, y1):
            for xx in range(x0, x1):
                self.SetPixel(xx, yy, color)

    def DrawCircle(self, cx, cy, radius, color, thickness=1):
        """Midpoint-algorithm circle outline."""
        for t in range(thickness):
            self._midpoint_circle(cx, cy, radius - t, color, fill=False)

    def FillCircle(self, cx, cy, radius, color):
        """Filled circle."""
        self._midpoint_circle(cx, cy, radius, color, fill=True)

    def DrawArc(self, cx, cy, radius, start_deg, end_deg, color, thickness=1):
        """Planned — not implemented in the managed prototype; needs an
        LVGL-backed native backend."""
        raise NotImplementedException("DrawArc is pending the LVGL-backed native implementation.")

    def DrawText(self, text, font, x, y, size_px, color):
        """Rasterize `text` with `font` at `size_px`, top-left at (x, y).
        Alpha-blends against whatever is already in the canvas. The font is
        rasterized on every call; cache into a Canvas and re-blit for high FPS."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        if text is None:
            raise ArgumentException("text must not be None")
        if font is None:
            raise ArgumentException("font must not be None")
        if self.PixelFormat not in (PixelFormat.Rgb565, PixelFormat.Monochrome):
            raise NotSupportedException(
                f"DrawText into a {self.PixelFormat} canvas is not implemented "
                "(Rgb565 and Monochrome targets only).")
        if size_px <= 0 or len(text) == 0:
            return

        ascent, _, _ = font.GetMetrics(int(size_px))
        pen_x = int(x)
        baseline_y = int(y) + ascent
        mono = self.PixelFormat == PixelFormat.Monochrome
        lit = (color.R | color.G | color.B) != 0

        for ch in text:
            codepoint = ord(ch)
            bmp = ctypes.POINTER(ctypes.c_uint8)()
            gw = ctypes.c_int(); gh = ctypes.c_int()
            gxo = ctypes.c_int(); gyo = ctypes.c_int()
            advance = ctypes.c_int()
            native.font_rasterize(font._handle, codepoint, int(size_px),
                                  ctypes.byref(bmp),
                                  ctypes.byref(gw), ctypes.byref(gh),
                                  ctypes.byref(gxo), ctypes.byref(gyo),
                                  ctypes.byref(advance))
            try:
                if bmp and gw.value > 0 and gh.value > 0:
                    if mono:
                        self._stamp_glyph_monochrome(bmp, gw.value, gh.value,
                                                     pen_x + gxo.value, baseline_y + gyo.value, lit)
                    else:
                        self._alpha_blend_glyph(bmp, gw.value, gh.value,
                                                pen_x + gxo.value, baseline_y + gyo.value, color)
            finally:
                if bmp:
                    native.image_free(bmp)
            pen_x += advance.value

    def DrawImage(self, source, x, y, width=None, height=None, pixel_format=None):
        """Blit an image into this canvas at (x, y).

        - `source` is an Image: use its cached decoded pixels (fast path).
        - `source` is bytes-like and width/height/pixel_format are None: treat
          it as encoded file bytes (JPG/PNG/BMP/GIF/TGA), decode once and blit.
        - `source` is bytes-like and width/height/pixel_format are given:
          treat as a raw pixel buffer of that size and format."""
        if self._disposed:
            raise ObjectDisposedException("The Canvas is disposed")
        if source is None:
            raise ArgumentException("source must not be None")

        if isinstance(source, Image):
            self._blit_pixels(source._pixels, x, y, source.Width, source.Height, source.PixelFormat)
            return

        if width is None or height is None or pixel_format is None:
            # Encoded file bytes: decode then blit.
            if len(source) == 0:
                raise ArgumentException("Image file bytes are empty.")
            data = bytes(source)
            src = (ctypes.c_uint8 * len(data)).from_buffer_copy(data)
            out_pixels = ctypes.POINTER(ctypes.c_uint8)()
            out_w = ctypes.c_int(); out_h = ctypes.c_int()
            native.image_decode(src, len(data),
                                ctypes.byref(out_pixels), ctypes.byref(out_w), ctypes.byref(out_h))
            try:
                byte_count = out_w.value * out_h.value * 3
                managed = bytes(ctypes.string_at(out_pixels, byte_count))
                self._blit_pixels(managed, x, y, out_w.value, out_h.value, PixelFormat.Rgb888)
            finally:
                native.image_free(out_pixels)
            return

        # Raw pixel buffer path.
        self._blit_pixels(bytes(source), x, y, int(width), int(height), pixel_format)

    def Dispose(self):
        """Release the pixel buffer. Idempotent."""
        self._disposed = True
        self._buffer = bytearray()

    def __del__(self):
        try: self.Dispose()
        except Exception: pass

    # ---------- internals -------------------------------------------------

    def _stamp_disc(self, cx, cy, r, color):
        if r <= 0:
            self.SetPixel(cx, cy, color)
            return
        # Fill the disc as horizontal spans: one FillRectangle per row (which
        # is a slice-assignment for Rgb565). Massively faster than per-pixel.
        r2 = r * r
        for yy in range(-r, r + 1):
            # Half-width of the span at this row: largest xx with xx*xx+yy*yy <= r2.
            # Closed form avoids an inner loop.
            dx2 = r2 - yy * yy
            if dx2 < 0:
                continue
            w = int(math.isqrt(dx2))
            self.FillRectangle(cx - w, cy + yy, 2 * w + 1, 1, color)

    def _midpoint_circle(self, cx, cy, r, color, fill):
        if r <= 0:
            self.SetPixel(cx, cy, color)
            return
        if fill:
            # Filled circle = _stamp_disc; avoids calling FillRectangle 2r+1
            # times for each ring of the midpoint traversal.
            self._stamp_disc(cx, cy, r, color)
            return
        x, y, err = r, 0, 1 - r
        while x >= y:
            self.SetPixel(cx + x, cy + y, color); self.SetPixel(cx - x, cy + y, color)
            self.SetPixel(cx + x, cy - y, color); self.SetPixel(cx - x, cy - y, color)
            self.SetPixel(cx + y, cy + x, color); self.SetPixel(cx - y, cy + x, color)
            self.SetPixel(cx + y, cy - x, color); self.SetPixel(cx - y, cy - x, color)
            y += 1
            if err <= 0:
                err += 2 * y + 1
            else:
                x -= 1
                err += 2 * (y - x) + 1

    def _blit_pixels(self, pixels, x, y, width, height, pixel_format):
        if width <= 0 or height <= 0:
            return
        if self.PixelFormat != PixelFormat.Rgb565:
            raise NotSupportedException(
                f"DrawImage into a {self.PixelFormat} canvas is not implemented "
                "in the managed prototype (Rgb565 target only).")
        if pixel_format == PixelFormat.Rgb565:
            src_bpp = 2
        elif pixel_format == PixelFormat.Rgb888:
            src_bpp = 3
        else:
            raise NotSupportedException(
                f"DrawImage source format {pixel_format} is not implemented "
                "(Rgb565 and Rgb888 only).")
        if len(pixels) < width * height * src_bpp:
            raise ArgumentException("Pixel buffer is smaller than width * height * bytes-per-pixel.")

        # Clip (x, y, width, height) to the canvas bounds.
        src_x = max(0, -x)
        src_y = max(0, -y)
        dst_x = x + src_x
        dst_y = y + src_y
        clip_w = min(width - src_x, self.Width - dst_x)
        clip_h = min(height - src_y, self.Height - dst_y)
        if clip_w <= 0 or clip_h <= 0:
            return

        src_stride = width * src_bpp
        dst_stride = self.Width * 2
        buf = self._buffer

        if pixel_format == PixelFormat.Rgb565:
            for row in range(clip_h):
                src_off = (src_y + row) * src_stride + src_x * src_bpp
                dst_off = (dst_y + row) * dst_stride + dst_x * 2
                buf[dst_off: dst_off + clip_w * 2] = pixels[src_off: src_off + clip_w * 2]
        else:  # Rgb888
            for row in range(clip_h):
                src_off = (src_y + row) * src_stride + src_x * src_bpp
                dst_off = (dst_y + row) * dst_stride + dst_x * 2
                for col in range(clip_w):
                    r = pixels[src_off + col * 3 + 0]
                    g = pixels[src_off + col * 3 + 1]
                    b = pixels[src_off + col * 3 + 2]
                    px = ((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3)
                    buf[dst_off + col * 2 + 0] = px & 0xFF
                    buf[dst_off + col * 2 + 1] = (px >> 8) & 0xFF

    def _stamp_glyph_monochrome(self, glyph, gw, gh, dst_x, dst_y, lit):
        # Stamp an 8-bit grayscale glyph bitmap into a 1bpp Monochrome canvas.
        # Thresholds alpha: >= 128 sets/clears the bit (anti-aliasing is
        # meaningless at 1bpp). `glyph` is a ctypes POINTER(c_uint8).
        src_x0 = max(0, -dst_x)
        src_y0 = max(0, -dst_y)
        dst_x0 = dst_x + src_x0
        dst_y0 = dst_y + src_y0
        clip_w = min(gw - src_x0, self.Width - dst_x0)
        clip_h = min(gh - src_y0, self.Height - dst_y0)
        if clip_w <= 0 or clip_h <= 0:
            return
        row_bytes = (self.Width + 7) // 8
        buf = self._buffer
        for row in range(clip_h):
            src_row = (src_y0 + row) * gw + src_x0
            dst_row = (dst_y0 + row) * row_bytes
            for col in range(clip_w):
                if glyph[src_row + col] < 128:
                    continue
                x = dst_x0 + col
                bit = 7 - (x & 7)
                off = dst_row + (x >> 3)
                if lit:
                    buf[off] |= (1 << bit)
                else:
                    buf[off] &= ~(1 << bit) & 0xFF

    def _alpha_blend_glyph(self, glyph, gw, gh, dst_x, dst_y, color):
        # Alpha-blends an 8-bit grayscale glyph bitmap over the current RGB565
        # canvas at (dst_x, dst_y). `glyph` is a ctypes POINTER(c_uint8).
        src_x0 = max(0, -dst_x)
        src_y0 = max(0, -dst_y)
        dst_x0 = dst_x + src_x0
        dst_y0 = dst_y + src_y0
        clip_w = min(gw - src_x0, self.Width - dst_x0)
        clip_h = min(gh - src_y0, self.Height - dst_y0)
        if clip_w <= 0 or clip_h <= 0:
            return

        src_r, src_g, src_b = color.R, color.G, color.B
        dst_stride = self.Width * 2
        buf = self._buffer

        for row in range(clip_h):
            src_row = (src_y0 + row) * gw + src_x0
            dst_row = (dst_y0 + row) * dst_stride + dst_x0 * 2
            for col in range(clip_w):
                a = glyph[src_row + col]
                if a == 0:
                    continue
                dst_off = dst_row + col * 2
                if a == 255:
                    px = ((src_r & 0xF8) << 8) | ((src_g & 0xFC) << 3) | (src_b >> 3)
                    buf[dst_off]     = px & 0xFF
                    buf[dst_off + 1] = (px >> 8) & 0xFF
                else:
                    old = buf[dst_off] | (buf[dst_off + 1] << 8)
                    old_r = ((old >> 11) & 0x1F) << 3
                    old_g = ((old >> 5)  & 0x3F) << 2
                    old_b = (old        & 0x1F) << 3
                    inv = 255 - a
                    r = (src_r * a + old_r * inv) // 255
                    g = (src_g * a + old_g * inv) // 255
                    b = (src_b * a + old_b * inv) // 255
                    px = ((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3)
                    buf[dst_off]     = px & 0xFF
                    buf[dst_off + 1] = (px >> 8) & 0xFF
