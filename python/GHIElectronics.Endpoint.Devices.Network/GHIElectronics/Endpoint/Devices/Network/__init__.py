"""Network on Endpoint, by type (Ethernet, WiFi, PcShared) instead of
system identifiers (eth0, wlan0, usb0) that change with plug order. Status
- addresses, link state, DNS, speed - is the standard Python surface
(``socket``, ``ipaddress``, parsing ``/proc/net/route`` or ``ip``). This
module answers one question: given a type, what is the system name?

    from GHIElectronics.Endpoint.Devices.Network import NetworkType, NetworkController

    name = NetworkController.GetName(NetworkType.Ethernet)
    if name:
        print(f"ethernet is {name}")

The full NetworkController (Enable, Disable, GetInterfaceSettings,
SetInterfaceSettings, IsEnabled, IsConnected) is in .NET; Python gets
the same shape in a later release."""

import ctypes
from enum import IntEnum

from GHIElectronics.Endpoint.Core._native import native


class NetworkType(IntEnum):
    """What a network interface is, as the user sees it in network.json."""

    Ethernet = 0
    Wifi = 1
    PcShared = 2


class NetworkController:
    """Helpers that resolve a user-facing :class:`NetworkType` into the
    system identifier for that interface on this board, so programs never
    depend on names (eth0, wlan0, usb0) that change with plug order."""

    @staticmethod
    def GetName(type_):
        """The system identifier for the first interface of the given type
        (e.g. "eth0", "wlan0", "usb0"), or an empty string if none is
        currently present."""
        buffer = ctypes.create_string_buffer(32)   # identifier is at most 15 chars; room to spare
        native.network_get_interface_name(int(type_), buffer, len(buffer))
        return buffer.value.decode("ascii")
