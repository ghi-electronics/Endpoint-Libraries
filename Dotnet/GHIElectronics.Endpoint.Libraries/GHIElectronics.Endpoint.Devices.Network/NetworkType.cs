namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>
    /// What a network interface is, as the user sees it in network.json.
    /// The driver takes care of the hardware: an Ethernet value matches the
    /// board's built-in PHY or any USB Ethernet adapter; a WiFi value matches
    /// a built-in WiFi module or any WiFi USB stick.
    /// </summary>
    public enum NetworkType {
        Ethernet = 0,
        Wifi = 1,
    }
}
