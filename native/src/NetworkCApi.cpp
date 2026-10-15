// The hidden C layer for NetworkController. The language wrappers call
// Enable / Disable / IsEnabled / IsConnected synchronously; the work runs
// in the caller's process, so errors come back through the normal return
// code / endpoint_last_error path.

#include <cstdio>

#include "CApi.h"
#include "Network.h"

using namespace GHIElectronics::Endpoint::Internal;

// type: 0 = Ethernet, 1 = WiFi, 2 = UsbTethered.

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
