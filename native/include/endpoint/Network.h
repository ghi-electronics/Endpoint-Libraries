// Network on Endpoint, by type (Ethernet, WiFi, PcShared) instead of system
// identifiers (eth0, wlan0, usb0) that change with plug order. Status -
// addresses, link state, DNS, speed - is the standard C/C++ surface
// (getifaddrs, /proc/net/route, etc.). This header answers one question:
// given a type, what is the system name?
//
//   using namespace GHIElectronics::Endpoint::Devices::Network;
//   std::string name = NetworkController::GetName(NetworkType::Ethernet);
//   if (!name.empty()) {
//       std::cout << "ethernet is " << name << '\n';
//   }
//
// The full NetworkController (Enable, Disable, GetInterfaceSettings,
// SetInterfaceSettings, IsEnabled, IsConnected) is in .NET; C++ gets
// the same shape in a later release.

#pragma once

#include <string>

#include "Core.h"
#include "detail/CApi.h"

namespace GHIElectronics::Endpoint::Devices::Network {

// What a network interface is, as the user sees it in network.json. The
// driver takes care of the hardware: an Ethernet value matches the board's
// built-in PHY or any USB Ethernet adapter; a WiFi value matches a built-in
// WiFi module or any WiFi USB stick; PcShared matches the USB connection
// the board exposes to its host PC (CDC/RNDIS).
enum class NetworkType : int {
    Ethernet = 0,
    Wifi = 1,
    PcShared = 2,
};

class NetworkController {
public:
    // The system identifier (eth0, wlan0, usb0, ...) for the first interface
    // of the given network type, or an empty string if none is currently present.
    static std::string GetName(NetworkType type) {
        char buf[32] = {};  // identifier is at most 15 chars; room to spare
        Core::detail::Check(endpoint_network_get_interface_name(static_cast<int>(type), buf, sizeof buf));
        return buf;
    }
};

}  // namespace GHIElectronics::Endpoint::Devices::Network
