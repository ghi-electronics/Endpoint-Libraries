// Display on Endpoint. DisplayController is an abstract class: customers
// subclass it when they wire up a non-standard panel (SPI, I2C, or anything
// else) and drive the hardware through our Core Spi/I2c/Gpio APIs. For the
// one transport the library ships out of the box - the SoC's parallel RGB
// panel into /dev/fb0 - we provide ParallelDisplayController; GetDefault()
// returns one when /config/display.json names type = parallel.
//
//   using namespace GHIElectronics::Endpoint::Devices::Display;
//   auto d = DisplayController::GetDefault();
//   if (d) {
//       d->Enable();
//       d->ShowLogo();                                           // optional
//       d->Flush(pixels, PixelFormat::Format32bppArgb);
//   }

#pragma once

#include <cstdint>
#include <cstdio>
#include <fstream>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include "Core.h"
#include "detail/CApi.h"

namespace GHIElectronics::Endpoint::Devices::Display {

// What a display is, as the user sees it in display.json.
// None   = no built-in display; a custom subclass of DisplayController owns it.
// Parallel = the SoC's parallel RGB LCD controller writing /dev/fb0.
enum class DisplayType : int {
    None = 0,
    Parallel = 1,
};

// Pixel format of a pixel buffer passed to DisplayController::Flush.
// Names mirror System.Drawing.Imaging.PixelFormat so the API reads the same
// way across .NET, Python and C++.
enum class PixelFormat : int {
    Format1bppIndexed  = 0x30101,
    Format8bppIndexed  = 0x30803,
    Format16bppRgb555  = 0x21005,
    Format16bppRgb565  = 0x21005 | 0x00001,  // 0x21006
    Format24bppRgb     = 0x21808,
    Format32bppRgb     = 0x22009,
    Format32bppArgb    = 0x26200A,
};

// Signal polarity for the panel's control signals (DE, backlight).
enum class SignalPolarity : int {
    ActiveHigh = 0,
    ActiveLow = 1,
};

// Edge of the pixel clock on which the panel latches data.
enum class ClockEdge : int {
    Rising = 0,
    Falling = 1,
};

// GPIO line driving the panel's backlight. Can be null (customer uses an
// external backlight controller or has none).
struct BacklightSettings {
    std::string GpioChip;
    int GpioPin = -1;
    SignalPolarity ActivePolarity = SignalPolarity::ActiveHigh;
};

// Timings for the SoC's parallel RGB panel (DRM "DPI" / parallel-RGB bus).
struct ParallelLcdSettings {
    int Width = 0;
    int Height = 0;
    int PixelClockHz = 0;
    int HFrontPorch = 0;
    int HBackPorch = 0;
    int HSync = 0;
    int VFrontPorch = 0;
    int VBackPorch = 0;
    int VSync = 0;
    int BitsPerPixel = 16;
    SignalPolarity DataEnablePolarity = SignalPolarity::ActiveHigh;
    ClockEdge PixelClockEdge = ClockEdge::Rising;
};

// Everything the library persists about the display.
struct DisplaySettings {
    DisplayType Type = DisplayType::None;
    bool EnableAtBoot = false;
    std::optional<BacklightSettings> Backlight;
    std::string Logo;
    std::optional<ParallelLcdSettings> Parallel;
};

// Abstract display. Subclass this for custom SPI/I2C/other panels; use
// GetDefault() for the built-in Parallel transport.
class DisplayController {
public:
    virtual ~DisplayController() = default;

    virtual int Width() const = 0;
    virtual int Height() const = 0;
    virtual bool IsEnabled() const = 0;

    // Load the panel's init path. Throws on failure.
    virtual void Enable() = 0;
    // Reverse of Enable. Safe to call when nothing is enabled.
    virtual void Disable() = 0;

    // Pixel transfer. The default overloads call the full one; subclasses
    // must implement the full form.
    void Flush(const uint8_t* pixels, size_t length) {
        Flush(pixels, length, PixelFormat::Format16bppRgb565);
    }
    void Flush(const uint8_t* pixels, size_t length, PixelFormat format) {
        Flush(pixels, length, format, 0, 0, Width(), Height());
    }
    virtual void Flush(const uint8_t* pixels, size_t length, PixelFormat format,
                       int x, int y, int width, int height) = 0;

    // Reads /config/display.json's "logo" filename, decodes the BMP (24-bit
    // uncompressed) from /config/<name>, and calls Flush with it.
    // Default implementation works for any subclass whose Flush handles
    // Format24bppRgb input.
    virtual void ShowLogo();

    // The configured settings for the built-in display, or null for custom.
    static DisplaySettings GetSettings();
    static void SetSettings(const DisplaySettings& settings);

    // The built-in Parallel controller if display.json says so, otherwise null.
    static std::unique_ptr<DisplayController> GetDefault();
};

// Built-in: parallel RGB panel through /dev/fb0.
class ParallelDisplayController : public DisplayController {
public:
    ParallelDisplayController() = default;

    int Width() const override;
    int Height() const override;
    bool IsEnabled() const override {
        int r = 0;
        Core::detail::Check(endpoint_display_is_enabled(&r));
        return r != 0;
    }

    void Enable() override;
    void Disable() override;

    using DisplayController::Flush;
    void Flush(const uint8_t* pixels, size_t length, PixelFormat format,
               int x, int y, int width, int height) override;

private:
    std::fstream framebuffer;
    int stride = 0;
    int targetBpp = 16;
    int width = 0;
    int height = 0;
};

// Inline implementations ------------------------------------------------------

namespace detail {

inline DisplaySettings SettingsFromRaw(const endpoint_display_settings_t& raw) {
    DisplaySettings s;
    s.Type = static_cast<DisplayType>(raw.type);
    s.EnableAtBoot = raw.enable_at_boot != 0;
    s.Logo = raw.logo;
    if (raw.has_backlight) {
        BacklightSettings b;
        b.GpioChip = raw.backlight_gpio_chip;
        b.GpioPin = raw.backlight_gpio_pin;
        b.ActivePolarity = static_cast<SignalPolarity>(raw.backlight_active_polarity);
        s.Backlight = b;
    }
    ParallelLcdSettings p;
    p.Width = raw.parallel_width;
    p.Height = raw.parallel_height;
    p.PixelClockHz = raw.parallel_pixel_clock_hz;
    p.HFrontPorch = raw.parallel_h_front_porch;
    p.HBackPorch = raw.parallel_h_back_porch;
    p.HSync = raw.parallel_h_sync;
    p.VFrontPorch = raw.parallel_v_front_porch;
    p.VBackPorch = raw.parallel_v_back_porch;
    p.VSync = raw.parallel_v_sync;
    p.BitsPerPixel = raw.parallel_bits_per_pixel;
    p.DataEnablePolarity = static_cast<SignalPolarity>(raw.parallel_de_polarity);
    p.PixelClockEdge = static_cast<ClockEdge>(raw.parallel_pixel_clock_edge);
    if (p.Width != 0 || p.Height != 0 || p.PixelClockHz != 0) {
        s.Parallel = p;
    }
    return s;
}

inline endpoint_display_settings_t RawFromSettings(const DisplaySettings& s) {
    endpoint_display_settings_t raw = {};
    raw.enable_at_boot = s.EnableAtBoot ? 1 : 0;
    raw.type = static_cast<int>(s.Type);
    std::snprintf(raw.logo, sizeof raw.logo, "%s", s.Logo.c_str());

    if (s.Backlight.has_value()) {
        raw.has_backlight = 1;
        std::snprintf(raw.backlight_gpio_chip, sizeof raw.backlight_gpio_chip,
                      "%s", s.Backlight->GpioChip.c_str());
        raw.backlight_gpio_pin = s.Backlight->GpioPin;
        raw.backlight_active_polarity = static_cast<int>(s.Backlight->ActivePolarity);
    }
    ParallelLcdSettings p = s.Parallel.value_or(ParallelLcdSettings{});
    raw.parallel_width = p.Width;
    raw.parallel_height = p.Height;
    raw.parallel_pixel_clock_hz = p.PixelClockHz;
    raw.parallel_h_front_porch = p.HFrontPorch;
    raw.parallel_h_back_porch = p.HBackPorch;
    raw.parallel_h_sync = p.HSync;
    raw.parallel_v_front_porch = p.VFrontPorch;
    raw.parallel_v_back_porch = p.VBackPorch;
    raw.parallel_v_sync = p.VSync;
    raw.parallel_bits_per_pixel = p.BitsPerPixel;
    raw.parallel_de_polarity = static_cast<int>(p.DataEnablePolarity);
    raw.parallel_pixel_clock_edge = static_cast<int>(p.PixelClockEdge);
    return raw;
}

// Minimal 24-bit uncompressed BMP reader. Reads /config/<name>, returns the
// pixel buffer in top-down 24bppRgb (B,G,R order) and the dimensions. Empty
// pixels vector if the file is missing or not a 24bpp uncompressed BMP.
inline bool ReadLogoBmp(const std::string& name,
                        std::vector<uint8_t>& outPixels,
                        int& outWidth, int& outHeight) {
    outPixels.clear();
    outWidth = outHeight = 0;
    if (name.empty()) return false;
    std::string path = "/config/" + name;
    std::ifstream f(path, std::ios::binary);
    if (!f) return false;

    uint8_t header[54];
    f.read(reinterpret_cast<char*>(header), sizeof header);
    if (!f || header[0] != 'B' || header[1] != 'M') return false;
    uint32_t dataOffset = header[10] | (header[11] << 8) | (header[12] << 16) | (header[13] << 24);
    int32_t width = static_cast<int32_t>(header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24));
    int32_t height = static_cast<int32_t>(header[22] | (header[23] << 8) | (header[24] << 16) | (header[25] << 24));
    uint16_t bpp = header[28] | (header[29] << 8);
    uint32_t compression = header[30] | (header[31] << 8) | (header[32] << 16) | (header[33] << 24);
    if (bpp != 24 || compression != 0 || width <= 0) return false;

    bool flipY = height > 0;  // positive height means bottom-up
    int absHeight = flipY ? height : -height;
    int rowBytes = width * 3;
    int rowPad = (4 - (rowBytes & 3)) & 3;

    f.seekg(dataOffset, std::ios::beg);
    outPixels.resize(static_cast<size_t>(rowBytes) * absHeight);
    for (int row = 0; row < absHeight; ++row) {
        int destRow = flipY ? (absHeight - 1 - row) : row;
        f.read(reinterpret_cast<char*>(outPixels.data() + destRow * rowBytes), rowBytes);
        if (!f) return false;
        if (rowPad > 0) f.ignore(rowPad);
    }
    outWidth = width;
    outHeight = absHeight;
    return true;
}

}  // namespace detail

inline DisplaySettings DisplayController::GetSettings() {
    endpoint_display_settings_t raw;
    Core::detail::Check(endpoint_display_get_settings(&raw));
    return detail::SettingsFromRaw(raw);
}

inline void DisplayController::SetSettings(const DisplaySettings& s) {
    endpoint_display_settings_t raw = detail::RawFromSettings(s);
    Core::detail::Check(endpoint_display_set_settings(&raw));
}

inline std::unique_ptr<DisplayController> DisplayController::GetDefault() {
    DisplaySettings s = GetSettings();
    if (s.Type != DisplayType::Parallel) return nullptr;
    return std::unique_ptr<DisplayController>(new ParallelDisplayController());
}

inline void DisplayController::ShowLogo() {
    DisplaySettings s = GetSettings();
    std::vector<uint8_t> pixels;
    int w = 0, h = 0;
    if (!detail::ReadLogoBmp(s.Logo, pixels, w, h)) return;
    Flush(pixels.data(), pixels.size(), PixelFormat::Format24bppRgb, 0, 0, w, h);
}

inline int ParallelDisplayController::Width() const {
    return this->width ? this->width : GetSettings().Parallel.value_or(ParallelLcdSettings{}).Width;
}
inline int ParallelDisplayController::Height() const {
    return this->height ? this->height : GetSettings().Parallel.value_or(ParallelLcdSettings{}).Height;
}

inline void ParallelDisplayController::Enable() {
    Core::detail::Check(endpoint_display_enable());
    DisplaySettings s = GetSettings();
    if (s.Parallel.has_value()) {
        this->width = s.Parallel->Width;
        this->height = s.Parallel->Height;
        this->targetBpp = s.Parallel->BitsPerPixel ? s.Parallel->BitsPerPixel : 16;
    }
    // Open /dev/fb0 and read the real stride (DMA alignment can make it >Width*bpp/8).
    this->framebuffer.open("/dev/fb0", std::ios::in | std::ios::out | std::ios::binary);
    this->stride = this->width * (this->targetBpp / 8);
    std::ifstream sf("/sys/class/graphics/fb0/stride");
    if (sf) {
        int s2 = 0;
        sf >> s2;
        if (s2 > 0) this->stride = s2;
    }
}

inline void ParallelDisplayController::Disable() {
    if (this->framebuffer.is_open()) this->framebuffer.close();
    Core::detail::Check(endpoint_display_disable());
}

inline void ParallelDisplayController::Flush(const uint8_t* pixels, size_t length, PixelFormat format,
                                             int x, int y, int width, int height) {
    if (!this->framebuffer.is_open()) {
        throw Core::InvalidOperationException("Flush called before Enable");
    }
    if (x < 0 || y < 0 || width <= 0 || height <= 0
        || x + width > this->width || y + height > this->height) {
        throw Core::ArgumentException("Flush region outside the panel");
    }
    size_t targetBytesPerPixel = static_cast<size_t>(this->targetBpp / 8);
    // Convert each row and write at (y+row) * stride + x * targetBytesPerPixel.
    size_t sourceStride;
    switch (format) {
        case PixelFormat::Format16bppRgb565: sourceStride = width * 2; break;
        case PixelFormat::Format24bppRgb:    sourceStride = width * 3; break;
        case PixelFormat::Format32bppRgb:
        case PixelFormat::Format32bppArgb:   sourceStride = width * 4; break;
        default:
            throw Core::NotSupportedException("Unsupported pixel format for Flush");
    }
    if (length < sourceStride * height) {
        throw Core::ArgumentException("Pixel buffer smaller than region");
    }

    std::vector<uint8_t> row(width * targetBytesPerPixel);
    for (int r = 0; r < height; ++r) {
        const uint8_t* src = pixels + r * sourceStride;
        uint8_t* dst = row.data();
        if (format == PixelFormat::Format16bppRgb565 && targetBytesPerPixel == 2) {
            std::memcpy(dst, src, width * 2);
        } else if (format == PixelFormat::Format24bppRgb && targetBytesPerPixel == 2) {
            for (int p = 0; p < width; ++p) {
                uint8_t b = src[p * 3 + 0], g = src[p * 3 + 1], re = src[p * 3 + 2];
                uint16_t v = ((re >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);
                dst[p * 2 + 0] = static_cast<uint8_t>(v & 0xFF);
                dst[p * 2 + 1] = static_cast<uint8_t>(v >> 8);
            }
        } else if ((format == PixelFormat::Format32bppArgb || format == PixelFormat::Format32bppRgb)
                   && targetBytesPerPixel == 2) {
            for (int p = 0; p < width; ++p) {
                uint8_t b = src[p * 4 + 0], g = src[p * 4 + 1], re = src[p * 4 + 2];
                uint16_t v = ((re >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);
                dst[p * 2 + 0] = static_cast<uint8_t>(v & 0xFF);
                dst[p * 2 + 1] = static_cast<uint8_t>(v >> 8);
            }
        } else {
            throw Core::NotSupportedException("Unsupported pixel-format / target-bpp combo");
        }
        this->framebuffer.seekp(static_cast<std::streamoff>((y + r) * this->stride
                                                            + x * targetBytesPerPixel));
        this->framebuffer.write(reinterpret_cast<const char*>(dst), width * targetBytesPerPixel);
    }
    this->framebuffer.flush();
}

}  // namespace GHIElectronics::Endpoint::Devices::Display
