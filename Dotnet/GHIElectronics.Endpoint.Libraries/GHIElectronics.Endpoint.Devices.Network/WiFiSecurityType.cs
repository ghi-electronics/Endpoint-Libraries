namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>How a WiFi network authenticates clients.</summary>
    public enum WiFiSecurityType {
        /// <summary>Open network (no password).</summary>
        None = 0,
        /// <summary>WPA (first generation; use <see cref="Wpa2"/> if the AP supports it).</summary>
        Wpa = 1,
        /// <summary>WPA2-PSK (the default for most home and office networks).</summary>
        Wpa2 = 2,
        /// <summary>WPA3-SAE.</summary>
        Wpa3 = 3,
    }
}
