using System.Net;
using System.Numerics;
using System.Xml.Linq;
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

        public IPAddress Address { get; }
        public IPAddress Gateway { get; }
        public IPAddress[] Dns { get; }

        public string MACAddress { get; }
        internal NetworkAddressChangedEventArgs(IPAddress address, IPAddress gateway, IPAddress[] dns, string MAC, DateTime timestamp) {
            this.Timestamp = timestamp;
            this.Gateway = gateway;
            this.Address = address;
            this.Dns = dns;
            this.MACAddress = MAC;
        }
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

        private NetworkLinkConnectedChangedEventHandler? networkLinkConnectedChangedCallbacks = null!;
        private NetworkAddressChangedEventHandler? networkAddressChangedCallbacks = null!;

        private bool disposed = false;
        private bool enabled = false;

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

            this.enabled = true;

            this.InitializeEventDetectionTask();

        }

        public void Disable() {

            this.enabled = false;

            Thread.Sleep(1000); // disable event

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
                    var ssid = setting?.Ssid;
                    var pass = setting?.Password;

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

        public event NetworkLinkConnectedChangedEventHandler NetworkLinkConnectedChanged {
            add => this.networkLinkConnectedChangedCallbacks += value;
            remove => this.networkLinkConnectedChangedCallbacks -= value;
        }

        public event NetworkAddressChangedEventHandler NetworkAddressChanged {
            add => this.networkAddressChangedCallbacks += value;
            remove => this.networkAddressChangedCallbacks -= value;
        }
        private Task InitializeEventDetectionTask() {

            return Task.Run(() => {
                var lastEvent = string.Empty;

                var detect_connection_changed = false;
                var connected = false;

                while (this.enabled || !this.disposed) {

 

                    var argument = string.Format("{0}{1}", this.interfaceName, this.controllerId.ToString());
                    var script = new Script("getevent.sh", "/usr/sbin/", argument);

                    script.Start();

                    if (script.Output.CompareTo(lastEvent) != 0) {
                        lastEvent = script.Output;
                        detect_connection_changed = true;

                        if (script.Output.Contains("Link is Up") || script.Output.Contains("link becomes ready")) {
                            connected = true;

                        }
                        else {
                            connected = false;
                        }

                        this.networkLinkConnectedChangedCallbacks?.Invoke(this, new NetworkLinkConnectedChangedEventArgs(connected, DateTime.Now));
                    }
                    var try_ipv4_cnt = 10;
                    if (detect_connection_changed) {
                        try {
                            // get MAC address
                            argument = string.Format("{0}{1}", this.interfaceName, this.controllerId.ToString());

                            script = new Script("getmacadd.sh", "/sbin/", argument);

                            script.Start();

                            var macaddress = script.Output.Substring(0, script.Output.Length - 1); // remove '\n'

                            if (connected) {
// Get IP
try_get_ipv4:
                                Thread.Sleep(1000);
                                argument = string.Format("{0}{1}", this.interfaceName, this.controllerId.ToString());
                                script = new Script("getadd_ip4.sh", "/sbin/", argument);

                                script.Start();

                                var ip_out = script.Output;

                                if (ip_out.Length == 0) { // no ipv4 found yet
                                    if (try_ipv4_cnt > 0) {
                                        goto try_get_ipv4;
                                    }
                                    else {
                                        ip_out = "0.0.0.0\n";
                                    }
                                }

                                var ip = ip_out.Substring(0, script.Output.Length - 1); // remove '\n'



                                // Get gateway
                                argument = "";

                                script = new Script("getgateway.sh", "/sbin/", argument);

                                script.Start();

                                var getway = script.Output.Substring(0, script.Output.Length - 1); // remove '\n'

                                // Get DNS
                                argument = "";

                                script = new Script("getdns.sh", "/sbin/", argument);

                                script.Start();

                                var dns = script.Output.Substring(0, script.Output.Length - 1); // remove '\n'

                                this.networkAddressChangedCallbacks?.Invoke(this, new NetworkAddressChangedEventArgs(IPAddress.Parse(ip), IPAddress.Parse(getway), new IPAddress[] { IPAddress.Parse(dns) }, macaddress, DateTime.Now));
                            }
                            else {
                                // assuming default "0.0.0.0"
                                var ipdefault = "0.0.0.0";
                                this.networkAddressChangedCallbacks?.Invoke(this, new NetworkAddressChangedEventArgs(IPAddress.Parse(ipdefault), IPAddress.Parse(ipdefault), new IPAddress[] { IPAddress.Parse(ipdefault) }, macaddress, DateTime.Now));
                            }

                            detect_connection_changed = false;
                        }
                        catch  {

                        }


                    }

                    Thread.Sleep(2000);

                }
            }
            ); ;

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