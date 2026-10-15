namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>
    /// Settings for a WiFi interface. Inherits IP / DNS / MAC fields from
    /// <see cref="NetworkInterfaceSettings"/> and adds the SSID and
    /// password used to associate with the access point.
    /// </summary>
    public class WiFiNetworkInterfaceSettings : NetworkInterfaceSettings {
        /// <summary>The network name to connect to.</summary>
        public string Ssid { get; set; } = string.Empty;

        /// <summary>Plain-text passphrase. Ignored when <see cref="Security"/> is <see cref="WiFiSecurityType.None"/>.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Which WPA variant the access point uses. Default: WPA2.</summary>
        public WiFiSecurityType Security { get; set; } = WiFiSecurityType.Wpa2;
    }
}
