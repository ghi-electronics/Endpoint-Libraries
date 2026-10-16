// The hidden C layer for DisplayController. The language wrappers call
// Enable / Disable / IsEnabled / IsConnected synchronously; the work runs
// in the caller's process, so errors come back through the normal return
// code / endpoint_last_error path. Get/SetSettings read and write
// /config/display.json via nlohmann/json, matching the file produced by
// Endpoint-Firmware/external/board/endpoint/config-partition/display.json.
//
// Only the built-in Parallel transport is modelled in the JSON. Custom SPI/I2C
// panels are the customer's own subclass code and do not need our storage.

#include <cstdio>
#include <cstring>
#include <fstream>
#include <string>

#include <nlohmann/json.hpp>

#include "CApi.h"
#include "Display.h"
#include "endpoint/detail/CApi.h"
#include "Errors.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {

constexpr const char* kConfigPath = "/config/display.json";

std::string JsonStr(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_string()) ? it->get<std::string>() : std::string{};
}
int JsonInt(const nlohmann::json& j, const char* key, int fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_number_integer()) ? it->get<int>() : fallback;
}
bool JsonBool(const nlohmann::json& j, const char* key, bool fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_boolean()) ? it->get<bool>() : fallback;
}

void CopyToBuf(const std::string& s, char* out, size_t size) {
    std::snprintf(out, size, "%s", s.c_str());
}

int ParseType(const std::string& s) {
    if (s == "parallel") return 1;
    return 0;
}
const char* TypeToString(int t) {
    switch (t) {
        case 1: return "parallel";
        default: return "none";
    }
}

nlohmann::json LoadDisplayConfig() {
    try {
        std::ifstream f(kConfigPath);
        if (!f) return nlohmann::json{{"version", 1}};
        nlohmann::json j;
        f >> j;
        return j.is_object() ? j : nlohmann::json{{"version", 1}};
    } catch (...) {
        return nlohmann::json{{"version", 1}};
    }
}

void SaveDisplayConfig(const nlohmann::json& j) {
    std::string tmp = std::string(kConfigPath) + ".tmp";
    {
        std::ofstream f(tmp);
        f << j.dump(2) << '\n';
    }
    if (std::rename(tmp.c_str(), kConfigPath) != 0) {
        std::remove(tmp.c_str());
        throw EndpointError(ErrorKind::IO, "failed to write /config/display.json");
    }
}

nlohmann::json ObjectOrEmpty(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_object()) ? *it : nlohmann::json::object();
}

}  // namespace

ENDPOINT_API int endpoint_display_enable(void) {
    return Call([&] { Display::Enable(); });
}

ENDPOINT_API int endpoint_display_disable(void) {
    return Call([&] { Display::Disable(); });
}

ENDPOINT_API int endpoint_display_is_enabled(int* result) {
    return Call([&] {
        if (result != nullptr) {
            *result = Display::IsEnabled() ? 1 : 0;
        }
    });
}

ENDPOINT_API int endpoint_display_get_settings(endpoint_display_settings_t* out) {
    return Call([&] {
        if (out == nullptr) return;
        std::memset(out, 0, sizeof *out);

        nlohmann::json config = LoadDisplayConfig();
        out->enable_at_boot = JsonBool(config, "enableAtBoot", false) ? 1 : 0;
        out->type = ParseType(JsonStr(config, "type"));
        CopyToBuf(JsonStr(config, "logo"), out->logo, sizeof out->logo);

        auto bl = ObjectOrEmpty(config, "backlight");
        auto blChip = bl.find("gpioChip");
        auto blPin = bl.find("gpioPin");
        if ((blChip != bl.end() && !blChip->is_null()) ||
            (blPin != bl.end() && !blPin->is_null())) {
            out->has_backlight = 1;
            CopyToBuf(JsonStr(bl, "gpioChip"), out->backlight_gpio_chip,
                      sizeof out->backlight_gpio_chip);
            out->backlight_gpio_pin = JsonInt(bl, "gpioPin", -1);
            out->backlight_active_polarity = JsonStr(bl, "activePolarity") == "activeLow" ? 1 : 0;
        }

        auto parallel = ObjectOrEmpty(config, "parallel");
        out->parallel_width = JsonInt(parallel, "width", 0);
        out->parallel_height = JsonInt(parallel, "height", 0);
        out->parallel_pixel_clock_hz = JsonInt(parallel, "pixelClockHz", 0);
        out->parallel_h_front_porch = JsonInt(parallel, "hFrontPorch", 0);
        out->parallel_h_back_porch = JsonInt(parallel, "hBackPorch", 0);
        out->parallel_h_sync = JsonInt(parallel, "hSync", 0);
        out->parallel_v_front_porch = JsonInt(parallel, "vFrontPorch", 0);
        out->parallel_v_back_porch = JsonInt(parallel, "vBackPorch", 0);
        out->parallel_v_sync = JsonInt(parallel, "vSync", 0);
        out->parallel_bits_per_pixel = JsonInt(parallel, "bitsPerPixel", 16);
        out->parallel_de_polarity = JsonBool(parallel, "deActiveLow", false) ? 1 : 0;
        out->parallel_pixel_clock_edge = JsonBool(parallel, "pixelClockFalling", false) ? 1 : 0;
    });
}

ENDPOINT_API int endpoint_display_set_settings(const endpoint_display_settings_t* in) {
    return Call([&] {
        if (in == nullptr) {
            throw EndpointError(ErrorKind::Argument, "settings must not be null");
        }

        nlohmann::json config = LoadDisplayConfig();
        config["version"] = 1;
        config["enableAtBoot"] = in->enable_at_boot != 0;
        config["type"] = TypeToString(in->type);
        config["logo"] = in->logo[0] ? nlohmann::json(std::string(in->logo))
                                     : nlohmann::json(nullptr);

        nlohmann::json backlight = nlohmann::json::object();
        if (in->has_backlight) {
            backlight["gpioChip"] = in->backlight_gpio_chip[0]
                ? nlohmann::json(std::string(in->backlight_gpio_chip))
                : nlohmann::json(nullptr);
            backlight["gpioPin"] = (in->backlight_gpio_pin >= 0)
                ? nlohmann::json(in->backlight_gpio_pin)
                : nlohmann::json(nullptr);
            backlight["activePolarity"] = in->backlight_active_polarity != 0
                ? "activeLow" : "activeHigh";
        } else {
            backlight["gpioChip"] = nullptr;
            backlight["gpioPin"] = nullptr;
            backlight["activePolarity"] = "activeHigh";
        }
        config["backlight"] = backlight;

        nlohmann::json parallel = nlohmann::json::object();
        parallel["width"] = in->parallel_width;
        parallel["height"] = in->parallel_height;
        parallel["pixelClockHz"] = in->parallel_pixel_clock_hz;
        parallel["hFrontPorch"] = in->parallel_h_front_porch;
        parallel["hBackPorch"] = in->parallel_h_back_porch;
        parallel["hSync"] = in->parallel_h_sync;
        parallel["vFrontPorch"] = in->parallel_v_front_porch;
        parallel["vBackPorch"] = in->parallel_v_back_porch;
        parallel["vSync"] = in->parallel_v_sync;
        parallel["bitsPerPixel"] = in->parallel_bits_per_pixel;
        parallel["deActiveLow"] = in->parallel_de_polarity != 0;
        parallel["pixelClockFalling"] = in->parallel_pixel_clock_edge != 0;
        config["parallel"] = parallel;

        // Remove dead schema from older firmwares; keeps the on-disk file tidy.
        config.erase("nativeLcd");
        config.erase("spi");
        config.erase("i2c");
        config.erase("touch");

        SaveDisplayConfig(config);
    });
}
