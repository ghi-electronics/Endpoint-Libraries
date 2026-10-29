#nullable enable
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>
    /// One of the board's network interfaces, by type (Ethernet, WiFi,
    /// PC-shared). The system takes care of the hardware - an Ethernet
    /// controller matches the built-in PHY or any USB Ethernet adapter;
    /// a WiFi controller matches a built-in WiFi module or any WiFi USB
    /// stick.
    ///
    /// Enable / Disable apply synchronously and throw on failure (no
    /// interface present, bad settings, wpa_supplicant / udhcpc failure).
    /// Settings persist in <c>/config/network.json</c> through
    /// <see cref="GetInterfaceSettings"/> / <see cref="SetInterfaceSettings"/>.
    /// Status (addresses, link, DNS, speed) is the standard
    /// <see cref="System.Net.NetworkInformation.NetworkInterface"/> this
    /// controller exposes.
    ///
    /// <code>
    /// var eth = NetworkController.FromType(NetworkType.Ethernet);
    /// if (eth != null) {
    ///     var s = eth.GetInterfaceSettings();
    ///     s.IsDhcpEnabled = true;
    ///     eth.SetInterfaceSettings(s);
    ///     eth.Enable();                    // throws if the apply fails
    ///     Console.WriteLine(eth.IsConnected);
    /// }
    /// </code>
    /// </summary>
    public sealed unsafe class NetworkController {
        private const string ConfigPath = "/config/network.json";

        private readonly string sectionName;

        private NetworkController(NetworkType type) {
            this.Type = type;
            this.sectionName = type switch {
                NetworkType.Ethernet => "ethernet",
                NetworkType.Wifi => "wifi",
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        /// <summary>
        /// The controller for the given type. Always returns a valid
        /// controller: the controller represents the <em>type</em>
        /// (Ethernet, WiFi), not a specific interface. Hardware does not
        /// have to be present yet - call
        /// <see cref="SetInterfaceSettings"/> to configure, then
        /// <see cref="Enable"/>, which modprobes any USB driver needed and
        /// brings the matching interface up. Use <see cref="IsPresent"/>
        /// to check if the hardware is currently plugged in.
        /// </summary>
        public static NetworkController FromType(NetworkType type) =>
            new NetworkController(type);

        /// <summary>
        /// The system identifier (eth0, wlan0, ...) for the first interface
        /// of the given type, or an empty string if none is present.
        /// </summary>
        public static string GetName(NetworkType type) {
            const int bufferSize = 32;
            Span<byte> buffer = stackalloc byte[bufferSize];
            fixed (byte* p = buffer) {
                Libendpoint.Check(Libendpoint.NetworkGetInterfaceName((int)type, p, bufferSize));
            }
            int length = buffer.IndexOf((byte)0);
            if (length < 0) {
                length = bufferSize;
            }
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buffer[..length]);
        }

        public NetworkType Type { get; }

        /// <summary>System identifier (eth0, wlan0, ...), or empty string if
        /// no interface of this type is currently present. Reads live from
        /// the kernel on every access - it reflects the state right now,
        /// not the state at <see cref="FromType"/> time.</summary>
        public string Name => GetName(this.Type);

        /// <summary>True if an interface of this type currently exists on
        /// the board (built-in PHY detected, or USB adapter plugged in and
        /// its driver loaded). WiFi stays false until <see cref="Enable"/>
        /// is called at least once (that is what modprobes the Wi-Fi
        /// driver).</summary>
        public bool IsPresent => this.Name.Length > 0;

        /// <summary>
        /// The standard <see cref="System.Net.NetworkInformation.NetworkInterface"/>
        /// for this controller, or null if the interface has gone away since
        /// the controller was created.
        /// </summary>
        public NetworkInterface? NetworkInterface {
            get {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (nic.Name == this.Name) {
                        return nic;
                    }
                }
                return null;
            }
        }

        /// <summary>True if the interface is administratively up.</summary>
        public bool IsEnabled {
            get {
                Libendpoint.Check(Libendpoint.NetworkIsEnabled((int)this.Type, out int result));
                return result != 0;
            }
        }

        /// <summary>True if the interface currently has an IPv4 address.</summary>
        public bool IsConnected {
            get {
                Libendpoint.Check(Libendpoint.NetworkIsConnected((int)this.Type, out int result));
                return result != 0;
            }
        }

        /// <summary>
        /// Applies the settings from <c>/config/network.json</c> to this
        /// interface: brings the link up and starts DHCP, or sets the static
        /// address, or starts wpa_supplicant (WiFi). Throws if the apply fails.
        /// Call synchronously from the user's program; nothing runs
        /// automatically at boot.
        /// </summary>
        public void Enable() => Libendpoint.Check(Libendpoint.NetworkEnable((int)this.Type));

        /// <summary>
        /// Stops DHCP and brings the interface down. Does not clear the
        /// settings file.
        /// </summary>
        public void Disable() => Libendpoint.Check(Libendpoint.NetworkDisable((int)this.Type));

        /// <summary>
        /// Reads the current settings for this type from
        /// <c>/config/network.json</c>. Changes to the returned object do
        /// nothing until passed back to <see cref="SetInterfaceSettings"/>.
        /// </summary>
        public NetworkInterfaceSettings GetInterfaceSettings() {
            var section = LoadConfig()[this.sectionName]?.AsObject();
            if (section == null) {
                return this.Type == NetworkType.Wifi ? new WiFiNetworkInterfaceSettings() : new NetworkInterfaceSettings();
            }

            NetworkInterfaceSettings s = this.Type == NetworkType.Wifi
                ? new WiFiNetworkInterfaceSettings {
                    Ssid = section["ssid"]?.GetValue<string>() ?? string.Empty,
                    Password = section["password"]?.GetValue<string>() ?? string.Empty,
                    Security = ParseSecurity(section["security"]?.GetValue<string>()),
                }
                : new NetworkInterfaceSettings();

            s.IsDhcpEnabled = section["dhcp"]?.GetValue<bool>() ?? true;

            var @static = section["static"]?.AsObject();
            if (@static != null) {
                s.Address = ParseAddress(@static["ip"]);
                s.SubnetMask = ParseAddress(@static["mask"]);
                s.GatewayAddress = ParseAddress(@static["gateway"]);
                var dns = @static["dns"]?.AsArray();
                if (dns != null) {
                    var list = new List<IPAddress>();
                    foreach (var n in dns) {
                        var a = ParseAddress(n);
                        if (a != null) list.Add(a);
                    }
                    s.DnsAddresses = list.Count == 0 ? null : list.ToArray();
                }
            }
            var mac = section["mac"]?.GetValue<string>();
            s.MacAddress = string.IsNullOrEmpty(mac)
                ? null
                : PhysicalAddress.Parse(mac.ToUpperInvariant().Replace(':', '-'));

            return s;
        }

        /// <summary>
        /// Writes the settings to <c>/config/network.json</c> (persisted
        /// across reboots). Does NOT apply them to the live interface -
        /// call <see cref="Enable"/> for that.
        /// </summary>
        public void SetInterfaceSettings(NetworkInterfaceSettings settings) {
            ArgumentNullException.ThrowIfNull(settings);

            var config = LoadConfig();
            EnsureSection(config, this.sectionName);
            var section = config[this.sectionName]!.AsObject();

            section["dhcp"] = settings.IsDhcpEnabled;
            section["mac"] = settings.MacAddress == null
                ? null
                : string.Join(":", Array.ConvertAll(settings.MacAddress.GetAddressBytes(), b => b.ToString("x2")));

            var @static = section["static"]?.AsObject() ?? new JsonObject();
            @static["ip"] = settings.Address?.ToString();
            @static["mask"] = settings.SubnetMask?.ToString();
            @static["gateway"] = settings.GatewayAddress?.ToString();
            var dnsArray = new JsonArray();
            if (settings.DnsAddresses != null) {
                foreach (var a in settings.DnsAddresses) {
                    dnsArray.Add((JsonNode?)JsonValue.Create(a.ToString()));
                }
            }
            @static["dns"] = dnsArray;
            section["static"] = @static;

            if (settings is WiFiNetworkInterfaceSettings wifi) {
                section["ssid"] = wifi.Ssid;
                section["password"] = wifi.Password;
                section["security"] = wifi.Security.ToString().ToLowerInvariant();
            }

            SaveConfig(config);
        }

        // ------- helpers -------

        private static JsonObject LoadConfig() {
            if (!File.Exists(ConfigPath)) {
                return new JsonObject { ["version"] = 1 };
            }
            using var stream = File.OpenRead(ConfigPath);
            var node = JsonNode.Parse(stream);
            return node?.AsObject() ?? new JsonObject { ["version"] = 1 };
        }

        private static void SaveConfig(JsonObject config) {
            var tmp = ConfigPath + ".tmp";
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(tmp, config.ToJsonString(options));
            File.Move(tmp, ConfigPath, overwrite: true);
        }

        private static void EnsureSection(JsonObject config, string name) {
            if (config[name] == null) {
                config[name] = new JsonObject();
            }
        }

        private static IPAddress? ParseAddress(JsonNode? node) {
            var s = node?.GetValue<string>();
            return string.IsNullOrEmpty(s) ? null : IPAddress.Parse(s);
        }

        private static WiFiSecurityType ParseSecurity(string? s) => s?.ToLowerInvariant() switch {
            "none" or "open" => WiFiSecurityType.None,
            "wpa" => WiFiSecurityType.Wpa,
            "wpa3" => WiFiSecurityType.Wpa3,
            _ => WiFiSecurityType.Wpa2,
        };
    }
}
