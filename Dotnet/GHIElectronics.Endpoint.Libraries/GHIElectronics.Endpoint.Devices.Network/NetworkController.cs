using System.Net;
using System.Numerics;
using System.Xml.Linq;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.Network {




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
        public IPAddress[] Gateway { get; }
        public IPAddress[] Dns { get; }

        public string MACAddress { get; }
        internal NetworkAddressChangedEventArgs(IPAddress address, string gateways, string dns, string mac, DateTime timestamp) {
            this.Timestamp = timestamp;
            this.Address = address;
            this.MACAddress = mac;


            var gw = gateways.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            this.Gateway = new IPAddress[gw.Length];
            for (var i = 0; i < gw.Length; i++) {
                this.Gateway[i] = IPAddress.Parse(gw[i]);
            }

            var dn = dns.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            this.Dns = new IPAddress[dn.Length];
            for (var i = 0; i < dn.Length; i++) {
                this.Dns[i] = IPAddress.Parse(dn[i]);
            }

        }
    }

    public enum NetworkInterfaceType {
        Ethernet = 0,
        WiFi = 1,
        UsbEthernet = 2,
    }


    public class NetworkInterfaceSettings {
        public IPAddress Address { get; set; }
        public IPAddress SubnetMask { get; set; }
        public IPAddress GatewayAddress { get; set; }
        public IPAddress[] DnsAddresses { get; set; }
        public byte[] MacAddress { get; set; }
        public bool DhcpEnable { get; set; } = true;
        //public bool DynamicDnsEnable { get; set; } = true;
        //public byte[] TlsEntropy { get; set; }
        //public bool MulticastDnsEnable { get; set; } = false;
    }


    /**<example>
    Wi-Fi Network 
    <code>
    var networkType = NetworkInterfaceType.WiFi;

    var networkSetting = new WifiNetworkInterfaceSetting {
        Ssid = ******,
        Password = ******,
        DhcpEnable = true,
    };

    var network = new NetworkController(networkType, networkSetting);

    network.NetworkLinkConnectedChanged += (a, b) => {
        if (b.Connected) {
            Console.WriteLine("Connected");
        }
        else {
            Console.WriteLine("Disconnected");
        }
    };

    network.NetworkAddressChanged += (a, b) => {
        Console.WriteLine(string.Format("Address: {0}\n gateway: {1}\n DNS: {2}\n MAC: {3} ", b.Address, b.Gateway, b.Dns[0], b.MACAddress));
    };

    network.Enable();
    Thread.Sleep(-1);
    network.Disable();
    </code>
    </example>*/


    public class WiFiNetworkInterfaceSettings : NetworkInterfaceSettings {
        public string Ssid { get; set; }
        public string Password { get; set; }
    }

    /**<example>
    Ethernet Network 
    <code>
    var networkType = NetworkInterfaceType.Ethernet;

    var networkSetting = new NetworkInterfaceSettings {
        Address = new IPAddress(new byte[] { 192, 168, 86, 106 }),
        SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 }),
        GatewayAddress = new IPAddress(new byte[] { 192, 168, 86, 1 }),
        DnsAddresses = new IPAddress[] { new IPAddress(new byte[] { 75, 75, 75, 75 }) },
        DhcpEnable = false,
    };

    var network = new NetworkController(networkType, networkSetting);

    network.NetworkLinkConnectedChanged += (a, b) => {
        if (b.Connected) {
            Console.WriteLine("Connected");
        }
        else {
            Console.WriteLine("Disconnected");
        }
    };

    network.NetworkAddressChanged += (a, b) => {
        Console.WriteLine(string.Format("Address: {0}\n gateway: {1}\n DNS: {2}\n MAC: {3} ", b.Address, b.Gateway, b.Dns[0], b.MACAddress));
    };

    network.Enable();
    Thread.Sleep(-1);
    network.Disable();
    </code>
    </example>*/


    public class NetworkController : IDisposable {
        static int[] initializeCount = new int[3];
        static int[] taskEventThreadCount = new int[3];

        private NetworkLinkConnectedChangedEventHandler networkLinkConnectedChangedCallbacks = null!;
        private NetworkAddressChangedEventHandler networkAddressChangedCallbacks = null!;

        private bool disposed = false;
        private bool enabled = false;

        /// <exclude />
        public NetworkInterfaceSettings ActiveInterfaceSettings { get; private set; }

        /// <exclude />
        public NetworkInterfaceType InterfaceType { get; private set; }

        private string interfaceName = string.Empty;

        private int controllerId = 0;
        public NetworkController(NetworkInterfaceType interfaceType, NetworkInterfaceSettings interfaceSetting) {
            this.ActiveInterfaceSettings = interfaceSetting;
            this.InterfaceType = interfaceType;

            if (this.InterfaceType == NetworkInterfaceType.Ethernet) {
                this.interfaceName = "eth";
                this.controllerId = 0;
            }

            else if (this.InterfaceType == NetworkInterfaceType.WiFi) {
                this.interfaceName = "wlan";
                this.controllerId = 0;
            }

            else if (this.InterfaceType == NetworkInterfaceType.UsbEthernet) {
                this.interfaceName = "eth";
                this.controllerId = 1;
            }

            this.Acquire();

        }
        private void Acquire() {
            if (initializeCount[(int)this.InterfaceType] == 0) {

                //Core.Events.SystemEventChanged += this.NetworkEventChanged;
                //Core.Events.StartSystemEventDetectionTask();

                this.LoadResources();
            }

            initializeCount[(int)this.InterfaceType]++;
        }

        private void Release() {
            initializeCount[(int)this.InterfaceType]--;

            if (initializeCount[(int)this.InterfaceType] == 0) {

                //Core.Events.SystemEventChanged -= this.NetworkEventChanged;
                //Core.Events.StopSystemEventDetectionTask();

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

            if (this.InterfaceType == NetworkInterfaceType.UsbEthernet) {
                Thread.Sleep(2000);// USBEthernet need delay about two seconds before enable or it doesn't work
            }


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

            taskEventThreadCount[(int)this.InterfaceType]++;

            if (taskEventThreadCount[(int)this.InterfaceType] == 1) {

                this.TaskEvents();
            }
        }

        public void Disable() {
            if (this.enabled == false) {
                return;
            }

            this.enabled = false;

            Thread.Sleep(1000); // disable event

            var name = this.interfaceName.ToString() + this.controllerId.ToString();

            var script = new Script("ifconfig", "/sbin/", name + " down");

            script.Start();

            if (taskEventThreadCount[(int)this.InterfaceType] > 0)
                taskEventThreadCount[(int)this.InterfaceType]--;

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

                    if (!script.Output.Contains("Successfully initialized wpa_supplicant") || script.Error.Contains("No such device") || script.Error.Contains("Timeout")) {

                        script = new Script("rmmod", "/sbin/", "8188eu");

                        script.Start();

                        throw new Exception("Timeout. Could not start WiFi.");
                    }
                }
            }
            else if (this.InterfaceType == NetworkInterfaceType.UsbEthernet) {
                var script = new Script("modprobe", "", "r8153_ecm");

                script.Start();
            }
        }

        private void UnLoadResources() {

            if (this.InterfaceType == NetworkInterfaceType.WiFi) {

                var script = new Script("lsmod", "/sbin/", "");
                script.Start();

                if (script.Output.IndexOf("8188eu") > 0) {
                    script = new Script("rmmod", "/sbin/", "8188eu");

                    script.Start();
                }
            }
            else if (this.InterfaceType == NetworkInterfaceType.UsbEthernet) {
                // Do we need to unload???
            }

        }

        public event NetworkLinkConnectedChangedEventHandler NetworkLinkConnectedChanged {
            add => this.networkLinkConnectedChangedCallbacks += value;
            remove {
                if (this.networkLinkConnectedChangedCallbacks != null)
                    this.networkLinkConnectedChangedCallbacks -= value;
            }
        }

        public event NetworkAddressChangedEventHandler NetworkAddressChanged {
            add => this.networkAddressChangedCallbacks += value;
            remove {
                if (this.networkAddressChangedCallbacks != null)
                    this.networkAddressChangedCallbacks -= value;
            }
        }



        private bool CheckNetworkConnection(string networktype) {
            var script = new Script("ifconfig", "./", networktype);

            lock (objlock) {
                script.Start();
            }

            if (script.Output.Contains(networktype) && script.Output.Contains("inet addr"))
                return true;

            return false;

        }

        static object objlock = new object();
        private Task TaskEvents() {

            return Task.Run(() => {
                var lastEvent = string.Empty;



                var networklist = new string[] { "eth0", "wlan0", "eth1" };
                var detect_connection_changed = new bool[] { false, false, false };
                var connect = new bool[] { false, false, false };
                var lastconnect = new bool[] { false, false, false };

                var index = (int)this.InterfaceType;

                while (this.enabled && !this.disposed) {
                    Thread.Sleep(2000);

                    connect[index] = this.CheckNetworkConnection(networklist[index]);

                    if (connect[index] != lastconnect[index]) {
                        this.networkLinkConnectedChangedCallbacks?.Invoke(this, new NetworkLinkConnectedChangedEventArgs(connect[index], DateTime.Now));


                        // get MAC address
                        var argument = string.Format("{0}{1}", this.interfaceName, this.controllerId.ToString());

                        var script = new Script("getmacadd.sh", "/sbin/", argument);

                        script.Start();

                        var macaddress = script.Output.Substring(0, script.Output.Length - 1); // remove '\n'

                        var try_ipv4_cnt = 10;

                        if (connect[index]) {
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

                            var ip = ip_out.TrimEnd('\r', '\n'); // remove '\n'



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

                            this.networkAddressChangedCallbacks?.Invoke(this, new NetworkAddressChangedEventArgs(IPAddress.Parse(ip), getway, dns, macaddress, DateTime.Now));
                        }
                        else {
                            // assuming default "0.0.0.0"
                            var ipdefault = "0.0.0.0";
                            this.networkAddressChangedCallbacks?.Invoke(this, new NetworkAddressChangedEventArgs(IPAddress.Parse(ipdefault), ipdefault, ipdefault, macaddress, DateTime.Now));
                        }

                        lastconnect[index] = connect[index];


                    }

                    //index = (index + 1) % connect.Length;





                }
            }
            ); ;

        }

        /// <exclude />
        ~NetworkController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
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
