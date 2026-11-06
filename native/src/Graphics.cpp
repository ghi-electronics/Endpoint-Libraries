// C API over stb_image (JPG/PNG/BMP/GIF/TGA decode) and stb_truetype
// (TTF parse + glyph rasterization). Exposed to the .NET / Python / C++
// wrappers as endpoint_image_* and endpoint_font_* so customers hand us raw
// file bytes and never see "convert to RGB565 first" — the library handles
// decoding + format conversion internally.
//
// Memory ownership:
//   - endpoint_image_decode   returns a malloc'd RGB888 buffer; free with
//                             endpoint_image_free.
//   - endpoint_font_rasterize returns a malloc'd 8-bit alpha bitmap; free
//                             with endpoint_image_free (same free()).
//   - endpoint_font_init      copies the TTF bytes into a heap-owned
//                             FontHandle; the caller's buffer can be
//                             released as soon as init returns.

#include <cstdint>
#include <vector>

#include "CApi.h"
#include "Errors.h"

extern "C" {
#include "stb/stb_image.h"
#include "stb/stb_truetype.h"
}

using namespace GHIElectronics::Endpoint::Internal;

namespace {

struct FontHandle {
    std::vector<unsigned char> bytes;  // owns the TTF data (stbtt holds a raw pointer)
    stbtt_fontinfo info;
};

}  // namespace

// ----------------------------------------------------------------- image

ENDPOINT_API int endpoint_image_decode(const uint8_t* data, int length,
                                       uint8_t** pixels, int* width, int* height) {
    return Call([&] {
        if (data == nullptr || length <= 0) {
            throw EndpointError(ErrorKind::Argument, "image bytes are empty");
        }
        int w = 0, h = 0, channels = 0;
        unsigned char* result = stbi_load_from_memory(data, length, &w, &h, &channels, 3);
        if (result == nullptr) {
            const char* why = stbi_failure_reason();
            throw EndpointError(ErrorKind::IO, why != nullptr ? why : "image decode failed");
        }
        *pixels = result;
        *width  = w;
        *height = h;
    });
}

ENDPOINT_API void endpoint_image_free(uint8_t* pixels) {
    if (pixels != nullptr) stbi_image_free(pixels);
}

// ----------------------------------------------------------------- font

ENDPOINT_API int endpoint_font_init(const uint8_t* ttfData, int length, void** out_handle) {
    return Call([&] {
        if (ttfData == nullptr || length <= 0) {
            throw EndpointError(ErrorKind::Argument, "font bytes are empty");
        }
        auto* font = new FontHandle;
        font->bytes.assign(ttfData, ttfData + length);
        if (!stbtt_InitFont(&font->info, font->bytes.data(),
                            stbtt_GetFontOffsetForIndex(font->bytes.data(), 0))) {
            delete font;
            throw EndpointError(ErrorKind::Argument, "not a valid TTF / OTF font");
        }
        *out_handle = font;
    });
}

ENDPOINT_API void endpoint_font_free(void* handle) {
    delete static_cast<FontHandle*>(handle);
}

// Ascent, descent, lineGap at a given pixel height — the layout hints the
// caller needs to position a text baseline.
ENDPOINT_API int endpoint_font_metrics(void* handle, int sizePx,
                                       int* ascent, int* descent, int* lineGap) {
    return Call([&] {
        auto* font = static_cast<FontHandle*>(handle);
        float scale = stbtt_ScaleForPixelHeight(&font->info, (float)sizePx);
        int a = 0, d = 0, g = 0;
        stbtt_GetFontVMetrics(&font->info, &a, &d, &g);
        *ascent  = (int)(a * scale);
        *descent = (int)(d * scale);
        *lineGap = (int)(g * scale);
    });
}

// Rasterizes one Unicode codepoint at the given pixel size. Returns a
// malloc'd 8-bit alpha bitmap through *bitmap (free via endpoint_image_free),
// plus the glyph's size, its offset from the pen position (xoff, yoff — yoff
// is negative for the part above the baseline), and the advance width to
// move the pen for the next glyph. A zero-size glyph (space, control char)
// returns *bitmap = NULL and width = height = 0 but still fills in advance.
ENDPOINT_API int endpoint_font_rasterize(void* handle, int codepoint, int sizePx,
                                         uint8_t** bitmap, int* width, int* height,
                                         int* xoff, int* yoff, int* advance) {
    return Call([&] {
        auto* font = static_cast<FontHandle*>(handle);
        float scale = stbtt_ScaleForPixelHeight(&font->info, (float)sizePx);

        int w = 0, h = 0, xo = 0, yo = 0;
        unsigned char* bmp = stbtt_GetCodepointBitmap(&font->info, 0, scale, codepoint,
                                                      &w, &h, &xo, &yo);
        *bitmap = bmp;
        *width  = w;
        *height = h;
        *xoff   = xo;
        *yoff   = yo;

        int adv = 0, lsb = 0;
        stbtt_GetCodepointHMetrics(&font->info, codepoint, &adv, &lsb);
        *advance = (int)(adv * scale);
    });
}
