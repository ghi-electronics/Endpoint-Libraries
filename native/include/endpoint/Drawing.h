// GHIElectronics::Endpoint::UI::Drawing - off-screen 2D drawing primitives.
// API mirrors the .NET and Python packages.
//
//   using namespace GHIElectronics::Endpoint::UI::Drawing;
//   auto canvas = Canvas::Create(240, 320, PixelFormat::Rgb565);
//   canvas.Clear(Color::Black());
//   canvas.DrawLine(10, 10, 230, 310, Color::Red(), /*thickness=*/2);
//   canvas.FillCircle(120, 160, 40, Color::Blue());
//   canvas.DrawText("Hello Endpoint", font, 10, 30, 20, Color::White());
//   spi.Write(canvas.Buffer().data(), canvas.Buffer().size());
//
// Canvas drawing primitives are pure C++ pixel code (Bresenham, midpoint
// circle, disc stamp, RGB565 pack). Image and Font delegate to the
// stb_image + stb_truetype decoders inside libendpoint.
#pragma once

#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#include "Core.h"                 // detail::Check + exceptions
#include "detail/CApi.h"          // endpoint_image_* / endpoint_font_* C API

namespace GHIElectronics {
namespace Endpoint {
namespace UI {
namespace Drawing {

// Pixel layout of a Canvas buffer. Also the layout written to the destination
// device when Canvas.Buffer() is handed to it.
enum class PixelFormat : int {
    Monochrome = 1,  // 1 bpp, MSB-first packed (SSD1306 / SH1106 OLEDs)
    Rgb565     = 2,  // 16 bpp little-endian (on-board panel, most SPI TFTs)
    Rgb888     = 3,  // 24 bpp R-G-B byte order (parallel RGB panels @ 24 bpp)
    Argb8888   = 4,  // 32 bpp A-R-G-B byte order (off-screen compositing)
};

// A 32-bit color with 8 bits each of alpha, red, green, blue. Canvas maps
// it to its PixelFormat at draw time: Monochrome treats non-zero as "lit",
// Rgb565 drops the low bits, Rgb888 drops alpha, Argb8888 keeps everything.
struct Color {
    uint8_t R = 0;
    uint8_t G = 0;
    uint8_t B = 0;
    uint8_t A = 0xFF;

    constexpr Color() = default;
    constexpr Color(uint8_t r, uint8_t g, uint8_t b) : R(r), G(g), B(b), A(0xFF) {}
    constexpr Color(uint8_t r, uint8_t g, uint8_t b, uint8_t a) : R(r), G(g), B(b), A(a) {}

    // Color from a 0xAARRGGBB value.
    static constexpr Color FromArgb(uint32_t argb) {
        return Color(static_cast<uint8_t>(argb >> 16),
                     static_cast<uint8_t>(argb >> 8),
                     static_cast<uint8_t>(argb),
                     static_cast<uint8_t>(argb >> 24));
    }
    // Color from a 0xRRGGBB value, opaque.
    static constexpr Color FromRgb(uint32_t rgb) {
        return Color(static_cast<uint8_t>(rgb >> 16),
                     static_cast<uint8_t>(rgb >> 8),
                     static_cast<uint8_t>(rgb));
    }

    // Common colors — names match System.Drawing / CSS where they overlap.
    // constexpr factory methods: C++ can't have `static const Color` as
    // class-level members cleanly.
    static constexpr Color Transparent() { return Color(0, 0, 0, 0); }
    static constexpr Color Black()       { return Color(0x00, 0x00, 0x00); }
    static constexpr Color White()       { return Color(0xFF, 0xFF, 0xFF); }
    static constexpr Color Red()         { return Color(0xFF, 0x00, 0x00); }
    static constexpr Color Green()       { return Color(0x00, 0xFF, 0x00); }
    static constexpr Color Blue()        { return Color(0x00, 0x00, 0xFF); }
    static constexpr Color Yellow()      { return Color(0xFF, 0xFF, 0x00); }
    static constexpr Color Cyan()        { return Color(0x00, 0xFF, 0xFF); }
    static constexpr Color Magenta()     { return Color(0xFF, 0x00, 0xFF); }
    static constexpr Color Navy()        { return Color(0x00, 0x00, 0x80); }
    static constexpr Color Maroon()      { return Color(0x80, 0x00, 0x00); }
    static constexpr Color Olive()       { return Color(0x80, 0x80, 0x00); }
    static constexpr Color Gray()        { return Color(0x80, 0x80, 0x80); }
    static constexpr Color Silver()      { return Color(0xC0, 0xC0, 0xC0); }
    static constexpr Color Orange()      { return Color(0xFF, 0xA5, 0x00); }
};

namespace detail {
    // Pack an sRGB Color into 5-6-5. Stored little-endian (LSB first) to
    // match /dev/fb0 on little-endian ARM, so DisplayController::Flush can
    // pass the buffer straight through. SPI TFT controllers that expect
    // MSB-first (ILI9341, ST7735) need a byte swap on write.
    constexpr uint16_t ToRgb565(Color c) {
        return static_cast<uint16_t>(((c.R & 0xF8) << 8) | ((c.G & 0xFC) << 3) | (c.B >> 3));
    }

    inline size_t ByteCountFor(int width, int height, PixelFormat f) {
        switch (f) {
            case PixelFormat::Monochrome: return static_cast<size_t>((width + 7) / 8) * height;
            case PixelFormat::Rgb565:     return static_cast<size_t>(width) * height * 2;
            case PixelFormat::Rgb888:     return static_cast<size_t>(width) * height * 3;
            case PixelFormat::Argb8888:   return static_cast<size_t>(width) * height * 4;
        }
        throw Core::ArgumentOutOfRangeException("Unsupported pixel format");
    }
}

// A decoded image, ready to blit into a Canvas. Decoding (JPG/PNG/BMP/GIF/TGA
// - picked from the file's magic bytes) happens once at Load(); Canvas
// ::DrawImage(Image, x, y) then just blits + converts to whatever PixelFormat
// the target canvas uses. Same Image instance is reusable across multiple
// canvases and multiple frames.
class Image {
public:
    int Width() const { return this->width_; }
    int Height() const { return this->height_; }
    Drawing::PixelFormat PixelFormat() const { return this->format_; }
    const std::vector<uint8_t>& Pixels() const { return this->pixels_; }

    // Decode an image file into an in-memory bitmap. The decoder (stb_image
    // inside libendpoint) picks JPG/PNG/BMP/GIF/TGA from the file's magic
    // bytes. Internal format is always Rgb888 - Canvas::DrawImage converts
    // to the target canvas's format on draw.
    static Image Load(const uint8_t* fileBytes, size_t length) {
        if (fileBytes == nullptr || length == 0) {
            throw Core::ArgumentException("Image file bytes are empty.");
        }
        uint8_t* raw = nullptr;
        int w = 0, h = 0;
        Core::detail::Check(endpoint_image_decode(fileBytes, static_cast<int>(length), &raw, &w, &h));
        try {
            Image img;
            img.width_ = w;
            img.height_ = h;
            img.format_ = PixelFormat::Rgb888;
            img.pixels_.assign(raw, raw + static_cast<size_t>(w) * h * 3);
            endpoint_image_free(raw);
            return img;
        } catch (...) {
            endpoint_image_free(raw);
            throw;
        }
    }
    static Image Load(const std::vector<uint8_t>& bytes) {
        return Load(bytes.data(), bytes.size());
    }

private:
    int width_ = 0;
    int height_ = 0;
    Drawing::PixelFormat format_ = PixelFormat::Rgb888;
    std::vector<uint8_t> pixels_;
};

// A TrueType / OpenType font for Canvas::DrawText. Rasterizes glyphs on
// demand through stb_truetype inside libendpoint. Separate from UI::Font
// (which is LVGL's lv_tiny_ttf for widget text).
class Font {
public:
    Font() = default;
    ~Font() {
        if (this->handle_) endpoint_font_free(this->handle_);
    }
    Font(const Font&) = delete;
    Font& operator=(const Font&) = delete;
    Font(Font&& other) noexcept : handle_(other.handle_) { other.handle_ = nullptr; }
    Font& operator=(Font&& other) noexcept {
        if (this != &other) {
            if (this->handle_) endpoint_font_free(this->handle_);
            this->handle_ = other.handle_;
            other.handle_ = nullptr;
        }
        return *this;
    }

    // Load a TTF / OTF font from raw file bytes. Bytes are copied into the
    // native font handle, so the caller's buffer can be released as soon as
    // this returns.
    static Font Load(const uint8_t* ttfBytes, size_t length) {
        if (ttfBytes == nullptr || length == 0) {
            throw Core::ArgumentException("Font bytes are empty.");
        }
        void* h = nullptr;
        Core::detail::Check(endpoint_font_init(ttfBytes, static_cast<int>(length), &h));
        Font f;
        f.handle_ = h;
        return f;
    }
    static Font Load(const std::vector<uint8_t>& bytes) {
        return Load(bytes.data(), bytes.size());
    }

    // Font-wide vertical metrics at the given pixel height. ascent is from
    // the baseline to the top of the tallest glyph; descent is negative
    // (below the baseline). line_height = ascent - descent + lineGap.
    struct Metrics { int Ascent; int Descent; int LineGap; };
    Metrics GetMetrics(int sizePx) const {
        Metrics m{};
        Core::detail::Check(endpoint_font_metrics(this->handle_, sizePx,
                                                  &m.Ascent, &m.Descent, &m.LineGap));
        return m;
    }

    void* Handle() const { return this->handle_; }

private:
    void* handle_ = nullptr;
};

// A pixel buffer plus the primitives to draw into it. Pick the size and
// PixelFormat at Create() time; when done, hand Buffer() to whichever
// device accepts bytes - DisplayController::Flush, SpiDevice::Write,
// I2cDevice::Write, a file, anywhere.
class Canvas {
public:
    int Width() const { return this->width_; }
    int Height() const { return this->height_; }
    Drawing::PixelFormat PixelFormat() const { return this->format_; }
    // Raw pixel bytes. Length and layout match Width/Height/PixelFormat().
    const std::vector<uint8_t>& Buffer() const { return this->buffer_; }

    // Allocate a new Canvas of the given size and pixel format.
    static Canvas Create(int width, int height, Drawing::PixelFormat format) {
        if (width <= 0 || height <= 0) {
            throw Core::ArgumentOutOfRangeException("Width and height must be positive.");
        }
        Canvas c;
        c.width_ = width;
        c.height_ = height;
        c.format_ = format;
        c.buffer_.assign(detail::ByteCountFor(width, height, format), 0);
        return c;
    }

    // ---------- draw primitives (managed) ---------------------------------

    // Fill the whole buffer with a single color.
    void Clear(Color color) {
        auto& b = this->buffer_;
        switch (this->format_) {
            case PixelFormat::Monochrome: {
                uint8_t fill = ((color.R | color.G | color.B) != 0) ? 0xFF : 0x00;
                std::memset(b.data(), fill, b.size());
                break;
            }
            case PixelFormat::Rgb565: {
                uint16_t px = detail::ToRgb565(color);
                for (size_t i = 0; i < b.size(); i += 2) {
                    b[i]     = static_cast<uint8_t>(px & 0xFF);
                    b[i + 1] = static_cast<uint8_t>(px >> 8);
                }
                break;
            }
            case PixelFormat::Rgb888:
                for (size_t i = 0; i < b.size(); i += 3) {
                    b[i] = color.R; b[i + 1] = color.G; b[i + 2] = color.B;
                }
                break;
            case PixelFormat::Argb8888:
                for (size_t i = 0; i < b.size(); i += 4) {
                    b[i] = color.A; b[i + 1] = color.R; b[i + 2] = color.G; b[i + 3] = color.B;
                }
                break;
        }
    }

    // Set a single pixel. No-op if (x, y) is outside the canvas.
    void SetPixel(int x, int y, Color color) {
        if (x < 0 || x >= this->width_ || y < 0 || y >= this->height_) return;
        auto& b = this->buffer_;
        switch (this->format_) {
            case PixelFormat::Monochrome: {
                int byteIndex = y * ((this->width_ + 7) / 8) + (x / 8);
                int bit = 7 - (x & 7);
                if ((color.R | color.G | color.B) != 0) {
                    b[byteIndex] = static_cast<uint8_t>(b[byteIndex] | (1 << bit));
                } else {
                    b[byteIndex] = static_cast<uint8_t>(b[byteIndex] & ~(1 << bit));
                }
                break;
            }
            case PixelFormat::Rgb565: {
                uint16_t px = detail::ToRgb565(color);
                int i = (y * this->width_ + x) * 2;
                b[i]     = static_cast<uint8_t>(px & 0xFF);
                b[i + 1] = static_cast<uint8_t>(px >> 8);
                break;
            }
            case PixelFormat::Rgb888: {
                int i = (y * this->width_ + x) * 3;
                b[i] = color.R; b[i + 1] = color.G; b[i + 2] = color.B;
                break;
            }
            case PixelFormat::Argb8888: {
                int i = (y * this->width_ + x) * 4;
                b[i] = color.A; b[i + 1] = color.R; b[i + 2] = color.G; b[i + 3] = color.B;
                break;
            }
        }
    }

    // Bresenham line, honoring thickness by stamping an r-pixel disc at each step.
    void DrawLine(int x0, int y0, int x1, int y1, Color color, int thickness = 1) {
        int dx = std::abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -std::abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int r = thickness / 2;
        while (true) {
            StampDisc(x0, y0, r, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // Stroke a rectangle outline.
    void DrawRectangle(int x, int y, int width, int height, Color color, int thickness = 1) {
        for (int t = 0; t < thickness; ++t) {
            DrawLine(x + t, y + t, x + width - 1 - t, y + t, color);
            DrawLine(x + t, y + height - 1 - t, x + width - 1 - t, y + height - 1 - t, color);
            DrawLine(x + t, y + t, x + t, y + height - 1 - t, color);
            DrawLine(x + width - 1 - t, y + t, x + width - 1 - t, y + height - 1 - t, color);
        }
    }

    // Fill a rectangle.
    void FillRectangle(int x, int y, int width, int height, Color color) {
        for (int yy = y; yy < y + height; ++yy)
            for (int xx = x; xx < x + width; ++xx)
                SetPixel(xx, yy, color);
    }

    // Midpoint-algorithm circle outline.
    void DrawCircle(int cx, int cy, int radius, Color color, int thickness = 1) {
        for (int t = 0; t < thickness; ++t) MidpointCircle(cx, cy, radius - t, color, /*fill=*/false);
    }
    // Filled circle.
    void FillCircle(int cx, int cy, int radius, Color color) {
        MidpointCircle(cx, cy, radius, color, /*fill=*/true);
    }

    // Planned - not implemented in the managed prototype; needs an LVGL-backed
    // native backend. Matches .NET's throwing stub.
    void DrawArc(int /*cx*/, int /*cy*/, int /*radius*/, int /*startDeg*/, int /*endDeg*/,
                 Color /*color*/, int /*thickness*/ = 1) {
        throw Core::NotImplementedException("DrawArc is pending the LVGL-backed native implementation.");
    }

    // Rasterize `text` with `font` at `sizePx`, top-left at (x, y).
    // Alpha-blends against whatever is already in the canvas. The font is
    // rasterized on every call; cache into a Canvas and re-blit for high FPS.
    void DrawText(const std::string& text, const Font& font, int x, int y, int sizePx, Color color) {
        if (this->format_ != PixelFormat::Rgb565 && this->format_ != PixelFormat::Monochrome) {
            throw Core::NotSupportedException(
                "DrawText into this canvas is not implemented (Rgb565 and Monochrome targets only).");
        }
        if (sizePx <= 0 || text.empty()) return;

        int ascent = 0, descent = 0, lineGap = 0;
        Core::detail::Check(endpoint_font_metrics(font.Handle(), sizePx, &ascent, &descent, &lineGap));
        int penX = x;
        int baselineY = y + ascent;
        bool mono = this->format_ == PixelFormat::Monochrome;
        bool lit = (color.R | color.G | color.B) != 0;

        // Decode UTF-8 codepoint by codepoint.
        for (size_t i = 0; i < text.size(); ) {
            uint32_t codepoint = 0;
            unsigned char c = static_cast<unsigned char>(text[i]);
            size_t extra = 0;
            if      (c < 0x80) { codepoint = c; extra = 0; }
            else if ((c & 0xE0) == 0xC0) { codepoint = c & 0x1F; extra = 1; }
            else if ((c & 0xF0) == 0xE0) { codepoint = c & 0x0F; extra = 2; }
            else if ((c & 0xF8) == 0xF0) { codepoint = c & 0x07; extra = 3; }
            else { codepoint = c; extra = 0; }  // malformed; treat as literal
            ++i;
            for (size_t k = 0; k < extra && i < text.size(); ++k, ++i) {
                codepoint = (codepoint << 6) | (static_cast<unsigned char>(text[i]) & 0x3F);
            }

            uint8_t* bmp = nullptr;
            int gw = 0, gh = 0, gxo = 0, gyo = 0, advance = 0;
            Core::detail::Check(endpoint_font_rasterize(font.Handle(), static_cast<int>(codepoint), sizePx,
                                                        &bmp, &gw, &gh, &gxo, &gyo, &advance));
            if (bmp && gw > 0 && gh > 0) {
                if (mono) {
                    StampGlyphMonochrome(bmp, gw, gh, penX + gxo, baselineY + gyo, lit);
                } else {
                    AlphaBlendGlyph(bmp, gw, gh, penX + gxo, baselineY + gyo, color);
                }
            }
            if (bmp) endpoint_image_free(bmp);
            penX += advance;
        }
    }

    // Blit a pre-decoded Image into this canvas at (x, y). Clips to the
    // canvas bounds. Prefer this over the file-bytes overload in redraw
    // loops: decoding a JPG is ~150 ms; blitting a cached Image is a few ms.
    void DrawImage(const Image& image, int x, int y) {
        BlitPixels(image.Pixels().data(), image.Pixels().size(),
                   x, y, image.Width(), image.Height(), image.PixelFormat());
    }
    // Decode an image file (JPG/PNG/BMP/GIF/TGA - format detected from the
    // file's magic bytes) and blit it into this canvas at (x, y).
    void DrawImage(const uint8_t* fileBytes, size_t length, int x, int y) {
        if (fileBytes == nullptr || length == 0) {
            throw Core::ArgumentException("Image file bytes are empty.");
        }
        uint8_t* raw = nullptr;
        int w = 0, h = 0;
        Core::detail::Check(endpoint_image_decode(fileBytes, static_cast<int>(length), &raw, &w, &h));
        try {
            BlitPixels(raw, static_cast<size_t>(w) * h * 3, x, y, w, h, PixelFormat::Rgb888);
        } catch (...) {
            endpoint_image_free(raw);
            throw;
        }
        endpoint_image_free(raw);
    }
    void DrawImage(const std::vector<uint8_t>& fileBytes, int x, int y) {
        DrawImage(fileBytes.data(), fileBytes.size(), x, y);
    }
    // Blit a decoded pixel buffer into this canvas at (x, y). Source may
    // extend outside the canvas on any side - pixels that fall outside are
    // clipped. Prefer the file-bytes overload if you have raw file bytes.
    void DrawImage(const uint8_t* pixels, size_t length, int x, int y,
                   int width, int height, Drawing::PixelFormat format) {
        BlitPixels(pixels, length, x, y, width, height, format);
    }

private:
    int width_ = 0;
    int height_ = 0;
    Drawing::PixelFormat format_ = PixelFormat::Rgb565;
    std::vector<uint8_t> buffer_;

    void StampDisc(int cx, int cy, int r, Color color) {
        if (r <= 0) { SetPixel(cx, cy, color); return; }
        int r2 = r * r;
        for (int yy = -r; yy <= r; ++yy)
            for (int xx = -r; xx <= r; ++xx)
                if (xx * xx + yy * yy <= r2) SetPixel(cx + xx, cy + yy, color);
    }

    void MidpointCircle(int cx, int cy, int r, Color color, bool fill) {
        if (r <= 0) { SetPixel(cx, cy, color); return; }
        int x = r, y = 0, err = 1 - r;
        while (x >= y) {
            if (fill) {
                for (int i = cx - x; i <= cx + x; ++i) { SetPixel(i, cy + y, color); SetPixel(i, cy - y, color); }
                for (int i = cx - y; i <= cx + y; ++i) { SetPixel(i, cy + x, color); SetPixel(i, cy - x, color); }
            } else {
                SetPixel(cx + x, cy + y, color); SetPixel(cx - x, cy + y, color);
                SetPixel(cx + x, cy - y, color); SetPixel(cx - x, cy - y, color);
                SetPixel(cx + y, cy + x, color); SetPixel(cx - y, cy + x, color);
                SetPixel(cx + y, cy - x, color); SetPixel(cx - y, cy - x, color);
            }
            ++y;
            if (err <= 0) { err += 2 * y + 1; }
            else          { --x; err += 2 * (y - x) + 1; }
        }
    }

    void BlitPixels(const uint8_t* pixels, size_t length, int x, int y,
                    int width, int height, Drawing::PixelFormat format) {
        if (width <= 0 || height <= 0) return;
        if (this->format_ != PixelFormat::Rgb565) {
            throw Core::NotSupportedException(
                "DrawImage into a non-Rgb565 canvas is not implemented (Rgb565 target only).");
        }
        int srcBpp = 0;
        if      (format == PixelFormat::Rgb565) srcBpp = 2;
        else if (format == PixelFormat::Rgb888) srcBpp = 3;
        else {
            throw Core::NotSupportedException(
                "DrawImage source format not supported (Rgb565 and Rgb888 only).");
        }
        if (length < static_cast<size_t>(width) * height * srcBpp) {
            throw Core::ArgumentException("Pixel buffer is smaller than width * height * bytes-per-pixel.");
        }

        // Clip (x, y, width, height) to the canvas bounds.
        int srcX = std::max(0, -x);
        int srcY = std::max(0, -y);
        int dstX = x + srcX;
        int dstY = y + srcY;
        int clipW = std::min(width  - srcX, this->width_  - dstX);
        int clipH = std::min(height - srcY, this->height_ - dstY);
        if (clipW <= 0 || clipH <= 0) return;

        int srcStride = width * srcBpp;
        int dstStride = this->width_ * 2;
        auto& buf = this->buffer_;

        for (int row = 0; row < clipH; ++row) {
            int srcRow = (srcY + row) * srcStride + srcX * srcBpp;
            int dstRow = (dstY + row) * dstStride + dstX * 2;
            if (format == PixelFormat::Rgb565) {
                std::memcpy(buf.data() + dstRow, pixels + srcRow, static_cast<size_t>(clipW) * 2);
            } else {
                for (int col = 0; col < clipW; ++col) {
                    uint8_t r = pixels[srcRow + col * 3 + 0];
                    uint8_t g = pixels[srcRow + col * 3 + 1];
                    uint8_t b = pixels[srcRow + col * 3 + 2];
                    uint16_t px = static_cast<uint16_t>(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
                    buf[dstRow + col * 2 + 0] = static_cast<uint8_t>(px & 0xFF);
                    buf[dstRow + col * 2 + 1] = static_cast<uint8_t>(px >> 8);
                }
            }
        }
    }

    // Stamp an 8-bit grayscale glyph bitmap into a 1bpp Monochrome canvas.
    // Thresholds alpha: >= 128 sets/clears the bit (anti-aliasing is
    // meaningless at 1bpp).
    void StampGlyphMonochrome(const uint8_t* glyph, int gw, int gh, int dstX, int dstY, bool lit) {
        int srcX0 = std::max(0, -dstX);
        int srcY0 = std::max(0, -dstY);
        int dstX0 = dstX + srcX0;
        int dstY0 = dstY + srcY0;
        int clipW = std::min(gw - srcX0, this->width_  - dstX0);
        int clipH = std::min(gh - srcY0, this->height_ - dstY0);
        if (clipW <= 0 || clipH <= 0) return;

        int rowBytes = (this->width_ + 7) / 8;
        auto& buf = this->buffer_;
        for (int row = 0; row < clipH; ++row) {
            int srcRow = (srcY0 + row) * gw + srcX0;
            int dstRow = (dstY0 + row) * rowBytes;
            for (int col = 0; col < clipW; ++col) {
                if (glyph[srcRow + col] < 128) continue;
                int x = dstX0 + col;
                int bit = 7 - (x & 7);
                int off = dstRow + (x >> 3);
                if (lit) buf[off] |= static_cast<uint8_t>(1 << bit);
                else     buf[off] &= static_cast<uint8_t>(~(1 << bit));
            }
        }
    }

    // Alpha-blends an 8-bit grayscale glyph bitmap over the current RGB565
    // canvas contents at (dst_x, dst_y). Clipping is handled per-pixel.
    void AlphaBlendGlyph(const uint8_t* glyph, int gw, int gh, int dstX, int dstY, Color color) {
        int srcX0 = std::max(0, -dstX);
        int srcY0 = std::max(0, -dstY);
        int dstX0 = dstX + srcX0;
        int dstY0 = dstY + srcY0;
        int clipW = std::min(gw - srcX0, this->width_  - dstX0);
        int clipH = std::min(gh - srcY0, this->height_ - dstY0);
        if (clipW <= 0 || clipH <= 0) return;

        int srcR = color.R, srcG = color.G, srcB = color.B;
        int dstStride = this->width_ * 2;
        auto& buf = this->buffer_;

        for (int row = 0; row < clipH; ++row) {
            int srcRow = (srcY0 + row) * gw + srcX0;
            int dstRow = (dstY0 + row) * dstStride + dstX0 * 2;
            for (int col = 0; col < clipW; ++col) {
                int a = glyph[srcRow + col];
                if (a == 0) continue;
                int dstOff = dstRow + col * 2;
                if (a == 255) {
                    uint16_t px = static_cast<uint16_t>(((srcR & 0xF8) << 8) | ((srcG & 0xFC) << 3) | (srcB >> 3));
                    buf[dstOff]     = static_cast<uint8_t>(px & 0xFF);
                    buf[dstOff + 1] = static_cast<uint8_t>(px >> 8);
                } else {
                    uint16_t old = static_cast<uint16_t>(buf[dstOff] | (buf[dstOff + 1] << 8));
                    int oldR = ((old >> 11) & 0x1F) << 3;
                    int oldG = ((old >> 5)  & 0x3F) << 2;
                    int oldB = (old        & 0x1F) << 3;
                    int inv  = 255 - a;
                    int r = (srcR * a + oldR * inv) / 255;
                    int g = (srcG * a + oldG * inv) / 255;
                    int b = (srcB * a + oldB * inv) / 255;
                    uint16_t px = static_cast<uint16_t>(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
                    buf[dstOff]     = static_cast<uint8_t>(px & 0xFF);
                    buf[dstOff + 1] = static_cast<uint8_t>(px >> 8);
                }
            }
        }
    }
};

}  // namespace Drawing
}  // namespace UI
}  // namespace Endpoint
}  // namespace GHIElectronics
