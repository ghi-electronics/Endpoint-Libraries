#pragma once

#include <string>

// NetworkController in libendpoint. Everything the .NET / Python / C++
// NetworkController does is implemented here and runs synchronously in
// the user's own program - no agent daemon, no inotify watcher, no file
// watching. Settings persistence is a separate concern (JSON file at
// /config/network.json, written by the language-side Set/GetInterfaceSettings
// methods); this header only applies them to live interfaces.
//
// Enable() reads /config/network.json, resolves the interface for the given
// type, brings the link up, starts udhcpc (or applies static), starts
// wpa_supplicant for WiFi, writes DNS to /etc/resolv.conf. Throws on failure.
//
// Disable() stops udhcpc and brings the interface down. Never touches the
// board's USB gadget interface (usb0 / PcShared); that is owned by
// S40network and sits at 192.168.82.2 for endpoint-cli / SSH to work.

namespace GHIElectronics::Endpoint::Internal::Network {

enum class Type : int {
    Ethernet = 0,
    Wifi = 1,
    PcShared = 2,
    Ignore = -1,
};

std::string GetInterfaceName(Type type);

// Reads /config/network.json, applies the matching section to the interface
// of the given type. Throws EndpointError (InvalidOperation) if no interface
// of that type is currently present, or if required static fields are empty.
void Enable(Type type);

// Stops udhcpc and brings the interface of the given type down. Safe to
// call when not enabled or when no interface is present (no-op in those
// cases). Refuses to touch PcShared (usb0) link state - that interface
// is owned by the system's network scripts.
void Disable(Type type);

// True if the interface of the given type is admin-UP. False if no
// interface of that type is present.
bool IsEnabled(Type type);

// True if the interface of the given type currently has an IPv4 address.
// False if the interface does not exist or has no address.
bool IsConnected(Type type);

}  // namespace GHIElectronics::Endpoint::Internal::Network
