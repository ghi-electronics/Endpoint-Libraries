// The hidden C layer for NetworkController. The language wrappers call
// Enable / Disable / IsEnabled / IsConnected synchronously; the work runs
// in the caller's process, so errors come back through the normal return
// code / endpoint_last_error path.

#include <cstdio>
#include <cstring>
#include <fstream>
#include <string>

#include <nlohmann/json.hpp>

#include "CApi.h"
#include "Network.h"
#include "endpoint/detail/CApi.h"
#include "Errors.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {

constexpr const char* kConfigPath = "/config/network.json";

const char* SettingsSectionKey(int type) {
    switch (type) {
        case 0: return "ethernet";
        case 1: return "wifi";
        default: return "";
    }
}

std::string JsonStr(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_string()) ? it->get<std::string>() : std::string{};
}

void CopyToBuf(const std::string& s, char* out, size_t size) {
    std::snprintf(out, size, "%s", s.c_str());
}

nlohmann::json LoadNetworkConfig() {
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

void SaveNetworkConfig(const nlohmann::json& j) {
    std::string tmp = std::string(kConfigPath) + ".tmp";
    {
        std::ofstream f(tmp);
        f << j.dump(2) << '\n';
    }
    if (std::rename(tmp.c_str(), kConfigPath) != 0) {
        std::remove(tmp.c_str());
        throw EndpointError(ErrorKind::IO, "failed to write /config/network.json");
    }
}

}  // namespace

// type: 0 = Ethernet, 1 = WiFi.

ENDPOINT_API int endpoint_network_get_interface_name(int type, char* out_buf, int out_size) {
    return Call([&] {
        if (out_buf == nullptr || out_size <= 0) {
            return;
        }
        std::string name = Network::GetInterfaceName(static_cast<Network::Type>(type));
        std::snprintf(out_buf, static_cast<size_t>(out_size), "%s", name.c_str());
    });
}

ENDPOINT_API int endpoint_network_enable(int type) {
    return Call([&] { Network::Enable(static_cast<Network::Type>(type)); });
}

ENDPOINT_API int endpoint_network_disable(int type) {
    return Call([&] { Network::Disable(static_cast<Network::Type>(type)); });
}

ENDPOINT_API int endpoint_network_is_enabled(int type, int* result) {
    return Call([&] {
        if (result != nullptr) {
            *result = Network::IsEnabled(static_cast<Network::Type>(type)) ? 1 : 0;
        }
    });
}

ENDPOINT_API int endpoint_network_is_connected(int type, int* result) {
    return Call([&] {
        if (result != nullptr) {
            *result = Network::IsConnected(static_cast<Network::Type>(type)) ? 1 : 0;
        }
    });
}

ENDPOINT_API int endpoint_network_get_interface_settings(int type, endpoint_network_settings_t* out) {
    return Call([&] {
        if (out == nullptr) return;
        std::memset(out, 0, sizeof *out);
        out->is_dhcp_enabled = 1;
        out->security = 2;  // Wpa2 default

        nlohmann::json config = LoadNetworkConfig();
        auto key = SettingsSectionKey(type);
        auto it = config.find(key);
        if (it == config.end() || !it->is_object()) return;
        const auto& section = *it;

        auto dhcpIt = section.find("dhcp");
        if (dhcpIt != section.end() && dhcpIt->is_boolean()) {
            out->is_dhcp_enabled = dhcpIt->get<bool>() ? 1 : 0;
        }
        CopyToBuf(JsonStr(section, "mac"), out->mac, sizeof out->mac);

        auto staticIt = section.find("static");
        if (staticIt != section.end() && staticIt->is_object()) {
            CopyToBuf(JsonStr(*staticIt, "ip"), out->address, sizeof out->address);
            CopyToBuf(JsonStr(*staticIt, "mask"), out->subnet_mask, sizeof out->subnet_mask);
            CopyToBuf(JsonStr(*staticIt, "gateway"), out->gateway, sizeof out->gateway);
            auto dnsIt = staticIt->find("dns");
            if (dnsIt != staticIt->end() && dnsIt->is_array()) {
                int i = 0;
                for (const auto& item : *dnsIt) {
                    if (i >= ENDPOINT_NETWORK_MAX_DNS) break;
                    if (item.is_string()) {
                        CopyToBuf(item.get<std::string>(), out->dns[i], ENDPOINT_NETWORK_IP_SIZE);
                        ++i;
                    }
                }
                out->dns_count = i;
            }
        }

        CopyToBuf(JsonStr(section, "ssid"), out->ssid, sizeof out->ssid);
        CopyToBuf(JsonStr(section, "password"), out->password, sizeof out->password);
        auto sec = JsonStr(section, "security");
        if (sec == "none" || sec == "open") out->security = 0;
        else if (sec == "wpa") out->security = 1;
        else if (sec == "wpa3") out->security = 3;
        else out->security = 2;
    });
}

ENDPOINT_API int endpoint_network_set_interface_settings(int type, const endpoint_network_settings_t* in) {
    return Call([&] {
        if (in == nullptr) {
            throw EndpointError(ErrorKind::Argument, "settings must not be null");
        }
        nlohmann::json config = LoadNetworkConfig();
        auto key = SettingsSectionKey(type);
        if (key[0] == '\0') {
            throw EndpointError(ErrorKind::ArgumentOutOfRange, "unknown network type");
        }
        nlohmann::json section = (config.contains(key) && config[key].is_object())
                                     ? config[key]
                                     : nlohmann::json::object();

        section["dhcp"] = in->is_dhcp_enabled != 0;
        section["mac"] = in->mac[0] ? nlohmann::json(std::string(in->mac)) : nlohmann::json(nullptr);

        nlohmann::json static_cfg = (section.contains("static") && section["static"].is_object())
                                        ? section["static"]
                                        : nlohmann::json::object();
        static_cfg["ip"] = in->address[0] ? nlohmann::json(std::string(in->address)) : nlohmann::json(nullptr);
        static_cfg["mask"] = in->subnet_mask[0] ? nlohmann::json(std::string(in->subnet_mask)) : nlohmann::json(nullptr);
        static_cfg["gateway"] = in->gateway[0] ? nlohmann::json(std::string(in->gateway)) : nlohmann::json(nullptr);
        nlohmann::json dns = nlohmann::json::array();
        for (int i = 0; i < in->dns_count && i < ENDPOINT_NETWORK_MAX_DNS; ++i) {
            if (in->dns[i][0]) dns.push_back(std::string(in->dns[i]));
        }
        static_cfg["dns"] = dns;
        section["static"] = static_cfg;

        if (type == 1) {  // Wifi
            section["ssid"] = std::string(in->ssid);
            section["password"] = std::string(in->password);
            const char* secStr = "wpa2";
            switch (in->security) {
                case 0: secStr = "none"; break;
                case 1: secStr = "wpa"; break;
                case 3: secStr = "wpa3"; break;
                default: secStr = "wpa2"; break;
            }
            section["security"] = secStr;
        }

        config[key] = section;
        SaveNetworkConfig(config);
    });
}
