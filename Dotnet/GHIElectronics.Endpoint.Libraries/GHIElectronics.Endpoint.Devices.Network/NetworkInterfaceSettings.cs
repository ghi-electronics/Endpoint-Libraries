#nullable enable
using System.Net;
using System.Net.NetworkInformation;

namespace GHIElectronics.Endpoint.Devices.Network {
    /// <summary>
    /// Settings for an Ethernet or PC-shared network interface (IP, routing,
    /// DNS, MAC pinning). Read with <see cref="NetworkController.GetInterfaceSettings"/>
    /// and apply with <see cref="NetworkController.SetInterfaceSettings"/>.
    /// For WiFi use <see cref="WiFiNetworkInterfaceSettings"/>.
    /// </summary>
    public class NetworkInterfaceSettings {
        /// <summary>True = ask the network for an address (DHCP). False = use the static fields below.</summary>
        public bool IsDhcpEnabled { get; set; } = true;

        /// <summary>Static IPv4 address. Ignored when <see cref="IsDhcpEnabled"/> is true.</summary>
        public IPAddress? Address { get; set; }

        /// <summary>Static IPv4 subnet mask (e.g. 255.255.255.0).</summary>
        public IPAddress? SubnetMask { get; set; }

        /// <summary>Static IPv4 default gateway.</summary>
        public IPAddress? GatewayAddress { get; set; }

        /// <summary>Static DNS servers. Null or empty leaves the system default.</summary>
        public IPAddress[]? DnsAddresses { get; set; }

        /// <summary>
        /// When set, these settings apply only to the adapter with this MAC
        /// address. Null (default) means any adapter of this type.
        /// </summary>
        public PhysicalAddress? MacAddress { get; set; }
    }
}
