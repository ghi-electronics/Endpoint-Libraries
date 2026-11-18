#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>
    /// One of the board's network interfaces, by type (Ethernet, WiFi,
    /// PC-shared). The system takes care of the hardware - an Ethernet
    /// controller matches the built-in PHY or any USB Ethernet adapter; a
    /// WiFi controller matches a built-in WiFi module or any WiFi USB stick.
    /// Enable / Disable apply synchronously and throw on failure (no
    /// interface present, bad settings, driver / DHCP failure).
    /// Settings persist in <c>/config/network.json</c> through
    /// <see cref="GetInterfaceSettings"/> / <see cref="SetInterfaceSettings"/>.
    /// Status (addresses, link, DNS, speed) is the standard
    /// <see cref="System.Net.NetworkInformation.NetworkInterface"/> this
    /// controller exposes.
    /// </summary>
    /// <example>
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
    /// </example>
    public sealed unsafe class NetworkController {
        private const string ConfigPath = "/config/network.json";

        // Pi (and any future non-Endpoint arm64 board) reports Unknown from
        // ChipDetect and uses the generic userspace fallback. Known Endpoint
        // chips (EPM815 / EPN256 / EPN128) keep going through Libendpoint so
        // their chip-specific helpers (built-in PHY wake, firmware bind, LED
        // coupling) still run.
        private static bool UseGenericFallback => ChipDetect.Current == Chip.Unknown;

        // Paths used by the generic fallback. Kept together so future boards
        // that need a different driver module can override via a settings
        // field if ever needed.
        private const string GenericWifiModule = "brcmfmac";
        private const string WpaConfPath = "/run/endpoint-wpa.conf";
        private const string WpaPidPath = "/run/endpoint-wpa.pid";
        private const string DhcpPidPath = "/run/endpoint-udhcpc.pid";

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
            if (UseGenericFallback) {
                // .NET doesn't reliably set NetworkInterfaceType.Wireless80211
                // on all drivers (brcmfmac in particular reports Ethernet). Match
                // on the interface name instead: wlan*/wl* for wireless,
                // eth*/en* for ethernet.
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                    var n = nic.Name;
                    if (type == NetworkType.Wifi && (n.StartsWith("wlan") || n.StartsWith("wl"))) return n;
                    if (type == NetworkType.Ethernet && (n.StartsWith("eth") || n.StartsWith("en"))) return n;
                }
                return string.Empty;
            }
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

        /// <summary>Which kind of interface this controller represents (<see cref="NetworkType.Ethernet"/> or <see cref="NetworkType.Wifi"/>).</summary>
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
                if (UseGenericFallback) {
                    if (this.Type == NetworkType.Wifi) return IsPidAlive(WpaPidPath);
                    var nic = this.NetworkInterface;
                    return nic != null && nic.OperationalStatus == OperationalStatus.Up;
                }
                Libendpoint.Check(Libendpoint.NetworkIsEnabled((int)this.Type, out int result));
                return result != 0;
            }
        }

        /// <summary>True if the interface currently has an IPv4 address.</summary>
        public bool IsConnected {
            get {
                if (UseGenericFallback) {
                    var nic = this.NetworkInterface;
                    if (nic == null || nic.OperationalStatus != OperationalStatus.Up) return false;
                    return nic.GetIPProperties().UnicastAddresses
                        .Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                  && !IPAddress.IsLoopback(a.Address));
                }
                Libendpoint.Check(Libendpoint.NetworkIsConnected((int)this.Type, out int result));
                return result != 0;
            }
        }

        /// <summary>
        /// Applies the settings from <c>/config/network.json</c> to this
        /// interface: brings the link up and starts DHCP, or sets the static
        /// address, or joins the WiFi network. Throws if the apply fails.
        /// Call synchronously from the user's program; nothing runs
        /// automatically at boot.
        /// </summary>
        public void Enable() {
            if (UseGenericFallback) { EnableGeneric(); return; }
            Libendpoint.Check(Libendpoint.NetworkEnable((int)this.Type));
        }

        /// <summary>
        /// Stops DHCP and brings the interface down. Does not clear the
        /// settings file.
        /// </summary>
        public void Disable() {
            if (UseGenericFallback) { DisableGeneric(); return; }
            Libendpoint.Check(Libendpoint.NetworkDisable((int)this.Type));
        }

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

        // ------- generic fallback (used when ChipDetect = Unknown) -------
        //
        // Pure userspace: load the WiFi driver module, run the WPA supplicant
        // for WPA/WPA2/WPA3 (or skip it for open WiFi / Ethernet), then DHCP
        // or a static address. Matches what Libendpoint's native NetworkEnable
        // does on Endpoint boards, just without the chip-specific helpers
        // Endpoint needs (built-in PHY wake, firmware bind). On Pi this IS the
        // implementation; on Endpoint it never runs.

        private void EnableGeneric() {
            var s = this.GetInterfaceSettings();
            if (this.Type == NetworkType.Wifi) {
                if (s is not WiFiNetworkInterfaceSettings wifi || string.IsNullOrEmpty(wifi.Ssid))
                    throw new InvalidOperationException("WiFi SSID is empty. Call SetInterfaceSettings first.");
                RunCmd("/sbin/modprobe", GenericWifiModule);
                WaitForInterface(timeoutMs: 5000);
                var iface = this.Name;
                RunCmd("/sbin/ip", "link", "set", iface, "up");
                File.WriteAllText(WpaConfPath, BuildWpaConf(wifi));
                StopPid(WpaPidPath);
                RunCmd("/usr/sbin/wpa_supplicant", "-B", "-i", iface, "-c", WpaConfPath, "-P", WpaPidPath);
                ConfigureIp(s, iface);
            } else { // Ethernet
                var iface = this.Name;
                if (string.IsNullOrEmpty(iface))
                    throw new InvalidOperationException("No ethernet interface present.");
                RunCmd("/sbin/ip", "link", "set", iface, "up");
                ConfigureIp(s, iface);
            }
        }

        private void DisableGeneric() {
            if (this.Type == NetworkType.Wifi) {
                StopPid(DhcpPidPath);
                StopPid(WpaPidPath);
                try { File.Delete(WpaConfPath); } catch { }
            } else {
                StopPid(DhcpPidPath);
            }
            var iface = this.Name;
            if (!string.IsNullOrEmpty(iface)) RunCmdNoThrow("/sbin/ip", "link", "set", iface, "down");
        }

        private static void ConfigureIp(NetworkInterfaceSettings s, string iface) {
            StopPid(DhcpPidPath);
            if (s.IsDhcpEnabled) {
                RunCmd("/sbin/udhcpc", "-i", iface, "-p", DhcpPidPath, "-b", "-q");
            } else {
                if (s.Address == null || s.SubnetMask == null)
                    throw new InvalidOperationException("Static mode: Address and SubnetMask required.");
                int prefix = MaskToPrefix(s.SubnetMask);
                RunCmd("/sbin/ip", "addr", "flush", "dev", iface);
                RunCmd("/sbin/ip", "addr", "add", $"{s.Address}/{prefix}", "dev", iface);
                if (s.GatewayAddress != null)
                    RunCmd("/sbin/ip", "route", "replace", "default", "via", s.GatewayAddress.ToString(), "dev", iface);
                if (s.DnsAddresses is { Length: > 0 })
                    File.WriteAllText("/etc/resolv.conf",
                        string.Join("\n", s.DnsAddresses.Select(d => $"nameserver {d}")) + "\n");
            }
        }

        private static string BuildWpaConf(WiFiNetworkInterfaceSettings s) {
            // Minimal config — just the network block. Omitting ctrl_interface=
            // and update_config= avoids depending on wpa_supplicant build options
            // that some distros (Buildroot default) compile out.
            var sb = new StringBuilder();
            sb.AppendLine("network={");
            sb.AppendLine($"\tssid=\"{EscapeWpa(s.Ssid)}\"");
            switch (s.Security) {
                case WiFiSecurityType.None:
                    sb.AppendLine("\tkey_mgmt=NONE");
                    break;
                case WiFiSecurityType.Wpa3:
                    sb.AppendLine("\tkey_mgmt=SAE");
                    sb.AppendLine("\tieee80211w=2");
                    sb.AppendLine($"\tpsk=\"{EscapeWpa(s.Password)}\"");
                    break;
                default:
                    sb.AppendLine("\tkey_mgmt=WPA-PSK");
                    sb.AppendLine($"\tpsk=\"{EscapeWpa(s.Password)}\"");
                    break;
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeWpa(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void WaitForInterface(int timeoutMs) {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline) {
                if (GetName(this.Type).Length > 0) return;
                System.Threading.Thread.Sleep(100);
            }
            throw new InvalidOperationException($"{this.Type} interface did not appear after loading the driver — check kernel modules and firmware files.");
        }

        private static void StopPid(string pidFile) {
            if (!File.Exists(pidFile)) return;
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out int pid))
                RunCmdNoThrow("/bin/kill", pid.ToString(CultureInfo.InvariantCulture));
            try { File.Delete(pidFile); } catch { }
        }

        private static bool IsPidAlive(string pidFile) {
            try {
                if (!int.TryParse(File.ReadAllText(pidFile).Trim(), out int pid)) return false;
                return Directory.Exists($"/proc/{pid}");
            } catch { return false; }
        }

        private static int MaskToPrefix(IPAddress mask) {
            int prefix = 0;
            foreach (var b in mask.GetAddressBytes())
                for (int i = 7; i >= 0; i--) if ((b & (1 << i)) != 0) prefix++; else return prefix;
            return prefix;
        }

        private static void RunCmd(string path, params string[] args) {
            int code = RunCore(path, args);
            if (code != 0) throw new InvalidOperationException($"{path} {string.Join(' ', args)} failed (exit {code})");
        }

        private static void RunCmdNoThrow(string path, params string[] args) => RunCore(path, args);

        private static int RunCore(string path, string[] args) {
            var psi = new ProcessStartInfo(path) { RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"cannot start {path}");
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }
    }
}
