using System.Net;
using System.Numerics;
using GHIElectronic.Endpoint.Libraries;

namespace GHIElectronic.Endpoint.Devices.Network {
    public delegate void NetworkLinkConnectedChangedEventHandler(NetworkController sender, NetworkLinkConnectedChangedEventArgs e);
    public delegate void NetworkAddressChangedEventHandler(NetworkController sender, NetworkAddressChangedEventArgs e);

    public class NetworkLinkConnectedChangedEventArgs : EventArgs {
        public bool Connected { get; }
        public DateTime Timestamp { get; }

        internal NetworkLinkConnectedChangedEventArgs(bool connected, DateTime timestamp) {
            this.Connected = connected;
            this.Timestamp = timestamp;
        }
    }

    public class NetworkAddressChangedEventArgs : EventArgs {
        public DateTime Timestamp { get; }

        internal NetworkAddressChangedEventArgs(DateTime timestamp) => this.Timestamp = timestamp;
    }
    public enum NetworkInterfaceType {
        Ethernet = 0,
        WiFi = 1,
    }

    public class NetworkInterfaceSettings {
        public IPAddress? Address { get; set; }
        public IPAddress? SubnetMask { get; set; }
        public IPAddress? GatewayAddress { get; set; }
        public IPAddress[]? DnsAddresses { get; set; }
        public byte[]? MacAddress { get; set; }
        public bool DhcpEnable { get; set; } = true;
        //public bool DynamicDnsEnable { get; set; } = true;
        //public byte[] TlsEntropy { get; set; }
        //public bool MulticastDnsEnable { get; set; } = false;
    }

    public class WiFiNetworkInterfaceSettings : NetworkInterfaceSettings {
        public string? Ssid { get; set; }
        public string? Password { get; set; }
    }
    public class NetworkController : IDisposable {
        static int initializeCount;

        private NetworkLinkConnectedChangedEventHandler networkLinkConnectedChangedCallbacks;
        private NetworkAddressChangedEventHandler networkAddressChangedCallbacks;

        private bool disposed = false;

        public NetworkInterfaceSettings ActiveInterfaceSettings { get; private set; }

        public NetworkInterfaceType InterfaceType { get; private set; }

        private string interfaceName = string.Empty;

        private int controllerId = 0;
        public NetworkController(NetworkInterfaceType interfaceType, NetworkInterfaceSettings interfaceSetting) {
            this.ActiveInterfaceSettings = interfaceSetting;
            this.InterfaceType = interfaceType;

            if (this.InterfaceType == NetworkInterfaceType.Ethernet) {
                this.interfaceName = "eth";
            }

            if (this.InterfaceType == NetworkInterfaceType.WiFi) {
                this.interfaceName = "wlan";
            }

            this.Acquire();

        }
        private void Acquire() {
            if (initializeCount == 0) {
                this.LoadResources();
            }

            initializeCount++;
        }

        private void Release() {
            initializeCount--;

            if (initializeCount == 0) {
                this.UnLoadResources();
            }
        }

        public void Enable() {
            this.Disable();

            var name = this.interfaceName.ToString() + this.controllerId.ToString();

            var address = this.ActiveInterfaceSettings.Address != null ? this.ActiveInterfaceSettings.Address.ToString() : "0.0.0.0";
            var gateway = this.ActiveInterfaceSettings.GatewayAddress != null ? this.ActiveInterfaceSettings.GatewayAddress.ToString() : "0.0.0.0";
            var subnetmask = this.ActiveInterfaceSettings.SubnetMask != null ? this.ActiveInterfaceSettings.SubnetMask.ToString() : "0.0.0.0";
            var dns = this.ActiveInterfaceSettings.DnsAddresses != null ? this.ActiveInterfaceSettings.DnsAddresses[0].ToString() : "0.0.0.0";



            Script script;


            if (this.ActiveInterfaceSettings.DhcpEnable) {

                script = new Script("dhcpcd", "/sbin/", name);

                script.Start();
            }
            else {

                var cidr = 24; //https://en.wikipedia.org/wiki/Classless_Inter-Domain_Routing ;//support 255.255.255.0 for now


                var argument = string.Format("-S ip_address={0}/{1}  -S routers={2} -S domain_name_servers={3} {4}", address, cidr, gateway, dns, name);

                script = new Script("dhcpcd", "/sbin/", argument);

                script.Start();
            }

            if (script.Error.Contains("sending commands to dhcpcd process")) { // already config, only up

                var argument = string.Format("{0} up", name);
                script = new Script("ifconfig", "/sbin/", argument);

                script.Start();

            }

        }

        public void Disable() {
            var name = this.interfaceName.ToString() + this.controllerId.ToString();

            var script = new Script("ifconfig", "/sbin/", name + " down");

            script.Start();
        }

        private void LoadResources() {

            if (this.InterfaceType == NetworkInterfaceType.WiFi) {

                var setting = this.ActiveInterfaceSettings as WiFiNetworkInterfaceSettings;

                var script = new Script("lsmod", "/sbin/", "");
                script.Start();

                if (script.Output.IndexOf("8188eu") < 0) {
                    var ssid = setting.Ssid;
                    var pass = setting.Password;

                    var argument = string.Format("{0} {1}", ssid, pass);
                    script = new Script("init_wifi.sh", "/sbin/", argument);

                    script.Start();

                    if (!script.Output.Contains("Successfully initialized wpa_supplicant")) {

                        script = new Script("rmmod", "/sbin/", "8188eu");

                        script.Start();

                        throw new Exception("Could not start WiFi. Check ssid or password");
                    }
                }
            }


        }

        private void UnLoadResources() {

            if (this.InterfaceType == NetworkInterfaceType.WiFi) {

                var setting = this.ActiveInterfaceSettings as WiFiNetworkInterfaceSettings;

                var script = new Script("lsmod", "/sbin/", "");
                script.Start();

                if (script.Output.IndexOf("8188eu") > 0) {
                    script = new Script("rmmod", "/sbin/", "8188eu");

                    script.Start();
                }
            }
        }
        ~NetworkController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {

            }

            this.Release();

            this.disposed = true;
        }
    }
}
