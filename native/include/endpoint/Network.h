// Network on Endpoint, by type (Ethernet, WiFi) instead of system
// identifiers (eth0, wlan0) that change with plug order.
//
// Enable / Disable are runtime-only and do not touch /config/network.json.
// Get/SetInterfaceSettings read and write that file. Status (addresses,
// link state, DNS, speed) is the standard C/C++ surface (getifaddrs,
// /proc/net/route, etc.).
//
//   using namespace GHIElectronics::Endpoint::Devices::Network;
//   auto eth = NetworkController::FromType(NetworkType::Ethernet);
//   if (eth) {
//       eth->Enable();
//       while (!eth->IsConnected()) usleep(500 * 1000);
//       std::cout << eth->Name() << " is connected\n";
//   }

#pragma once

#include <cstring>
#include <memory>
#include <string>
#include <vector>

#include "Core.h"
#include "detail/CApi.h"

namespace GHIElectronics::Endpoint::Devices::Network {

// What a network interface is, as the user sees it in network.json.
enum class NetworkType : int {
    Ethernet = 0,
    Wifi = 1,
};

// How a WiFi network authenticates clients.
enum class WiFiSecurityType : int {
    None = 0,
    Wpa = 1,
    Wpa2 = 2,
    Wpa3 = 3,
};

// Settings for an Ethernet interface (IP, routing, DNS, MAC pinning).
// Empty string means "not set"; empty DnsAddresses leaves the system
// default. For WiFi use WiFiNetworkInterfaceSettings.
struct NetworkInterfaceSettings {
    bool IsDhcpEnabled = true;
    std::string Address;
    std::string SubnetMask;
    std::string GatewayAddress;
    std::vector<std::string> DnsAddresses;
    std::string MacAddress;
    virtual ~NetworkInterfaceSettings() = default;
};

struct WiFiNetworkInterfaceSettings : NetworkInterfaceSettings {
    std::string Ssid;
    std::string Password;
    WiFiSecurityType Security = WiFiSecurityType::Wpa2;
};

class NetworkController {
public:
    // The controller for the given type, or nullptr if no interface of that
    // type is currently present on the board.
    static std::unique_ptr<NetworkController> FromType(NetworkType type);

    // The system identifier (eth0, wlan0, ...) for the first interface
    // of the given network type, or an empty string if none is currently present.
    static std::string GetName(NetworkType type) {
        char buf[32] = {};
        Core::detail::Check(endpoint_network_get_interface_name(static_cast<int>(type), buf, sizeof buf));
        return buf;
    }

    NetworkType Type() const { return this->type; }
    const std::string& Name() const { return this->name; }

    bool IsEnabled() const {
        int result = 0;
        Core::detail::Check(endpoint_network_is_enabled(static_cast<int>(this->type), &result));
        return result != 0;
    }

    bool IsConnected() const {
        int result = 0;
        Core::detail::Check(endpoint_network_is_connected(static_cast<int>(this->type), &result));
        return result != 0;
    }

    // Applies the settings from /config/network.json synchronously; throws
    // on failure. Runtime only - does not touch the JSON.
    void Enable() { Core::detail::Check(endpoint_network_enable(static_cast<int>(this->type))); }

    // Stops DHCP and brings the interface down. Runtime only.
    void Disable() { Core::detail::Check(endpoint_network_disable(static_cast<int>(this->type))); }

    // Reads the current settings for this type from /config/network.json.
    // For WiFi, the returned object is a WiFiNetworkInterfaceSettings;
    // dynamic_cast to it to access ssid/password/security.
    std::unique_ptr<NetworkInterfaceSettings> GetInterfaceSettings() const {
        endpoint_network_settings_t raw;
        Core::detail::Check(endpoint_network_get_interface_settings(static_cast<int>(this->type), &raw));

        std::unique_ptr<NetworkInterfaceSettings> s;
        if (this->type == NetworkType::Wifi) {
            auto w = std::make_unique<WiFiNetworkInterfaceSettings>();
            w->Ssid = raw.ssid;
            w->Password = raw.password;
            w->Security = static_cast<WiFiSecurityType>(raw.security);
            s = std::move(w);
        } else {
            s = std::make_unique<NetworkInterfaceSettings>();
        }
        s->IsDhcpEnabled = raw.is_dhcp_enabled != 0;
        s->Address = raw.address;
        s->SubnetMask = raw.subnet_mask;
        s->GatewayAddress = raw.gateway;
        s->MacAddress = raw.mac;
        for (int i = 0; i < raw.dns_count; ++i) {
            s->DnsAddresses.emplace_back(raw.dns[i]);
        }
        return s;
    }

    // Writes the settings to /config/network.json (persisted across
    // reboots). Does NOT apply them - call Enable() for that. Pass a
    // WiFiNetworkInterfaceSettings for WiFi type.
    void SetInterfaceSettings(const NetworkInterfaceSettings& settings) {
        endpoint_network_settings_t raw = {};
        raw.is_dhcp_enabled = settings.IsDhcpEnabled ? 1 : 0;
        std::snprintf(raw.address, sizeof raw.address, "%s", settings.Address.c_str());
        std::snprintf(raw.subnet_mask, sizeof raw.subnet_mask, "%s", settings.SubnetMask.c_str());
        std::snprintf(raw.gateway, sizeof raw.gateway, "%s", settings.GatewayAddress.c_str());
        std::snprintf(raw.mac, sizeof raw.mac, "%s", settings.MacAddress.c_str());
        int i = 0;
        for (const auto& dns : settings.DnsAddresses) {
            if (i >= ENDPOINT_NETWORK_MAX_DNS) break;
            std::snprintf(raw.dns[i], ENDPOINT_NETWORK_IP_SIZE, "%s", dns.c_str());
            ++i;
        }
        raw.dns_count = i;
        raw.security = static_cast<int>(WiFiSecurityType::Wpa2);

        if (auto* w = dynamic_cast<const WiFiNetworkInterfaceSettings*>(&settings)) {
            std::snprintf(raw.ssid, sizeof raw.ssid, "%s", w->Ssid.c_str());
            std::snprintf(raw.password, sizeof raw.password, "%s", w->Password.c_str());
            raw.security = static_cast<int>(w->Security);
        }

        Core::detail::Check(endpoint_network_set_interface_settings(static_cast<int>(this->type), &raw));
    }

private:
    NetworkController(NetworkType t, std::string n) : type(t), name(std::move(n)) {}
    NetworkType type;
    std::string name;
};

inline std::unique_ptr<NetworkController> NetworkController::FromType(NetworkType type) {
    std::string name = GetName(type);
    if (name.empty()) return nullptr;
    return std::unique_ptr<NetworkController>(new NetworkController(type, std::move(name)));
}

}  // namespace GHIElectronics::Endpoint::Devices::Network
