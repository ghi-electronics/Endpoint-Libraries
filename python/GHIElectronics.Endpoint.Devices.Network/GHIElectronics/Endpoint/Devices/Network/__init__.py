"""Network on Endpoint, by type (Ethernet, WiFi) instead of system
identifiers (eth0, wlan0) that change with plug order.

Enable / Disable are runtime-only and do not touch /config/network.json.
Get/SetInterfaceSettings read and write that file; agent's boot applier
honours the enableAtBoot flag per section. Status (addresses, link state,
DNS, speed) is the standard Python surface (``socket``, ``ipaddress``,
parsing ``/proc/net/route`` or ``ip``).

    from GHIElectronics.Endpoint.Devices.Network import NetworkType, NetworkController

    eth = NetworkController.FromType(NetworkType.Ethernet)
    if eth is not None:
        eth.Enable()
        while not eth.IsConnected:
            time.sleep(0.5)
        print(f"ethernet is {eth.Name}, connected")
"""

import ctypes
import json
import os
import tempfile
from enum import IntEnum

from GHIElectronics.Endpoint.Core._native import native


_CONFIG_PATH = "/config/network.json"
_SECTION_KEYS = {}  # populated after NetworkType is defined


class NetworkType(IntEnum):
    """What a network interface is, as the user sees it in network.json."""

    Ethernet = 0
    Wifi = 1


_SECTION_KEYS[NetworkType.Ethernet] = "ethernet"
_SECTION_KEYS[NetworkType.Wifi] = "wifi"


class WiFiSecurityType(IntEnum):
    """How a WiFi network authenticates clients."""

    None_ = 0
    Wpa = 1
    Wpa2 = 2
    Wpa3 = 3


class NetworkInterfaceSettings:
    """Settings for an Ethernet interface. Read with
    :meth:`NetworkController.GetInterfaceSettings` and apply with
    :meth:`NetworkController.SetInterfaceSettings`. For WiFi use
    :class:`WiFiNetworkInterfaceSettings`."""

    def __init__(self):
        self.IsDhcpEnabled = True
        self.Address = None            # str "192.168.1.100" or None
        self.SubnetMask = None         # str "255.255.255.0" or None
        self.GatewayAddress = None     # str "192.168.1.1" or None
        self.DnsAddresses = None       # list[str] or None
        self.MacAddress = None         # str "aa:bb:cc:dd:ee:ff" or None


class WiFiNetworkInterfaceSettings(NetworkInterfaceSettings):
    """Settings for a WiFi interface. Adds SSID + password + security
    on top of the common fields."""

    def __init__(self):
        super().__init__()
        self.Ssid = ""
        self.Password = ""
        self.Security = WiFiSecurityType.Wpa2


class NetworkController:
    """One of the board's network interfaces, by type (Ethernet, WiFi).
    :meth:`FromType` always returns a controller - the controller
    represents the type, not a specific interface. Hardware does not have
    to be present yet; call :meth:`SetInterfaceSettings` to configure,
    then :meth:`Enable`, which modprobes any USB driver needed and brings
    the matching interface up. Use :attr:`IsPresent` to check if hardware
    is currently present."""

    def __init__(self, type_):
        self.Type = type_
        self._section = _SECTION_KEYS[type_]

    @staticmethod
    def FromType(type_):
        """The controller for the given type. Always non-null."""
        return NetworkController(type_)

    @property
    def Name(self):
        """System identifier (eth0, wlan0, ...), or empty string if no
        interface of this type is currently present. Reads live from the
        kernel on every access."""
        return NetworkController.GetName(self.Type)

    @property
    def IsPresent(self):
        """True if an interface of this type currently exists on the board.
        WiFi stays False until :meth:`Enable` is called at least once
        (that is what modprobes the WiFi driver)."""
        return len(self.Name) > 0

    @staticmethod
    def GetName(type_):
        """The system identifier (eth0, wlan0, ...) for the first interface
        of the given type, or an empty string if none is present."""
        buffer = ctypes.create_string_buffer(32)
        native.network_get_interface_name(int(type_), buffer, len(buffer))
        return buffer.value.decode("ascii")

    @property
    def IsEnabled(self):
        """True if the interface is administratively up."""
        result = ctypes.c_int()
        native.network_is_enabled(int(self.Type), native.ref(result))
        return result.value != 0

    @property
    def IsConnected(self):
        """True if the interface has carrier (cable plugged / associated)
        AND an IPv4 address."""
        result = ctypes.c_int()
        native.network_is_connected(int(self.Type), native.ref(result))
        return result.value != 0

    def Enable(self):
        """Applies the settings from ``/config/network.json`` to this
        interface synchronously. Runtime only; does not touch the JSON."""
        native.network_enable(int(self.Type))

    def Disable(self):
        """Stops DHCP and brings the interface down. Runtime only; does
        not touch the JSON."""
        native.network_disable(int(self.Type))

    def GetInterfaceSettings(self):
        """Reads the current settings for this type from
        ``/config/network.json``. Changes to the returned object do nothing
        until passed back to :meth:`SetInterfaceSettings`."""
        config = _load_config()
        section = config.get(self._section) or {}

        if self.Type == NetworkType.Wifi:
            s = WiFiNetworkInterfaceSettings()
            s.Ssid = section.get("ssid") or ""
            s.Password = section.get("password") or ""
            s.Security = _parse_security(section.get("security"))
        else:
            s = NetworkInterfaceSettings()

        s.IsDhcpEnabled = bool(section.get("dhcp", True))
        static = section.get("static") or {}
        s.Address = static.get("ip") or None
        s.SubnetMask = static.get("mask") or None
        s.GatewayAddress = static.get("gateway") or None
        dns = static.get("dns") or []
        s.DnsAddresses = list(dns) if dns else None
        s.MacAddress = section.get("mac") or None
        return s

    def SetInterfaceSettings(self, settings):
        """Writes the settings to ``/config/network.json`` (persisted
        across reboots). Does NOT apply them to the live interface -
        call :meth:`Enable` for that."""
        if settings is None:
            raise ValueError("settings must not be None")

        config = _load_config()
        section = config.get(self._section) or {}

        section["dhcp"] = bool(settings.IsDhcpEnabled)
        section["mac"] = settings.MacAddress
        section["static"] = {
            "ip": settings.Address,
            "mask": settings.SubnetMask,
            "gateway": settings.GatewayAddress,
            "dns": list(settings.DnsAddresses) if settings.DnsAddresses else [],
        }

        if isinstance(settings, WiFiNetworkInterfaceSettings):
            section["ssid"] = settings.Ssid
            section["password"] = settings.Password
            section["security"] = settings.Security.name.lower().rstrip("_")

        config[self._section] = section
        _save_config(config)


# ---------------------------------------------------------------------------

def _load_config():
    if not os.path.exists(_CONFIG_PATH):
        return {"version": 1}
    try:
        with open(_CONFIG_PATH, "r") as f:
            data = json.load(f)
            return data if isinstance(data, dict) else {"version": 1}
    except (OSError, json.JSONDecodeError):
        return {"version": 1}


def _save_config(config):
    # Atomic write: temp file in same dir then rename
    directory = os.path.dirname(_CONFIG_PATH) or "."
    fd, tmp = tempfile.mkstemp(prefix=".network", suffix=".json.tmp", dir=directory)
    try:
        with os.fdopen(fd, "w") as f:
            json.dump(config, f, indent=2)
        os.replace(tmp, _CONFIG_PATH)
    except Exception:
        try: os.unlink(tmp)
        except OSError: pass
        raise


def _parse_security(value):
    if value is None:
        return WiFiSecurityType.Wpa2
    v = value.lower()
    if v in ("none", "open"):
        return WiFiSecurityType.None_
    if v == "wpa":
        return WiFiSecurityType.Wpa
    if v == "wpa3":
        return WiFiSecurityType.Wpa3
    return WiFiSecurityType.Wpa2
