using System.Collections;
using GHIElectronic.Endpoint.Core;

namespace GHIElectronic.Endpoint.Devices.UsbHost {
    public enum DeviceConnectionStatus {
        Disconnected = 0,
        Connected = 1,
        Bad = 2,
    };

    public delegate void OnConnectionChanged(UsbHostController sender, DeviceConnectionEventArgs e);

    public class DeviceConnectionEventArgs : EventArgs {
        private readonly uint id;
        private readonly byte interfaceIndex;
        private readonly BaseDevice.DeviceType type;
        private readonly ushort vendorId;
        private readonly ushort productId;
        private readonly byte portNumber;
        private readonly DeviceConnectionStatus deviceStatus;

        /// <summary>The device id.</summary>
        public uint Id => this.id;

        /// <summary>The logical device interface index.</summary>
        public byte InterfaceIndex => this.interfaceIndex;

        /// <summary>The device's type.</summary>
        public BaseDevice.DeviceType Type => this.type;

        /// <summary>The devic's vendor id.</summary>
        public ushort VendorId => this.vendorId;

        /// <summary>The device's product id.</summary>
        public ushort ProductId => this.productId;

        /// <summary>The device's USB port number.</summary>
        public byte PortNumber => this.portNumber;

        public DeviceConnectionStatus DeviceStatus => this.deviceStatus;


        internal DeviceConnectionEventArgs(uint id, byte interfaceIndex, BaseDevice.DeviceType type, ushort vendorId, ushort productId, byte portNumber, DeviceConnectionStatus deviceStatus) {
            this.id = id;
            this.interfaceIndex = interfaceIndex;
            this.type = type;
            this.vendorId = vendorId;
            this.productId = productId;
            this.portNumber = portNumber;
            this.deviceStatus = deviceStatus;
        }
    }
    public class UsbHostController : IDisposable {

        private static bool enabled;
        private static ArrayList devices;
        private static object listLock;
        private static int initializeCount;

        private bool disposed = false;

        private OnConnectionChanged onConnectionChangedCallbacks;

        public UsbHostController() {
            devices = new ArrayList();
            enabled = false;
            listLock = new object();

            this.Acquire();
        }
        public void Enable() {
            enabled = true; ;

          

        }

    

        public void Disable() {
            enabled = false; ;

            
        }

        public static BaseDevice[]? GetConnectedDevices() {
            if (enabled == false)
                return null;

            lock (listLock)
                return (BaseDevice[])devices.ToArray(typeof(BaseDevice));
        }

        internal static void RegisterDevice(BaseDevice device) {
            lock (listLock)
                devices.Add(device);
        }

        private static void OnDisconnect(object sender, DeviceConnectionEventArgs e) {
            lock (listLock) {
                var newList = new ArrayList();

                foreach (BaseDevice d in devices) {
                    if (d.Id == e.Id) {
                        d.OnDisconnected();
                        d.Dispose();
                    }
                    else {
                        newList.Add(d);
                    }
                }

                devices = newList;
            }
        }

        private void UsbHostEventChanged(string eventlog) {
            var device_id = -1;
            if (eventlog.Contains("USB device number ")) {
                if (eventlog.Contains("detected")) {

                    

                    var s = Core.Utils.FindAndSplitUntil(eventlog, "USB device number ", ' ');

                    var end = s.Length-1;

                    var s_num = "";

                    while (end > 0 && s[end] != ' ') {
                        s_num += s[end];
                        end--;
                    }


                    try {
                        device_id = int.Parse(s_num);
                        this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs((uint)device_id, 1, BaseDevice.DeviceType.Unknown, 0, 0, 0, DeviceConnectionStatus.Connected));
                    }
                    catch { }
                    
                }
            }
            if (eventlog.Contains("USB disconnect")) {

                var s = Core.Utils.FindAndSplitUntil(eventlog, "device number ", ' ');

                var end = s.Length - 1;

                var s_num = "";

                while (end > 0 && s[end] != ' ') {
                    s_num += s[end];
                    end--;
                }

                try {

                    device_id = int.Parse(s_num);
                    this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs((uint)device_id, 1, BaseDevice.DeviceType.Unknown, 0, 0, 0, DeviceConnectionStatus.Disconnected));
                }
                catch { }   

                
            }
        }
        private void OnConnectionChangedCallBack(UsbHostController sender, DeviceConnectionEventArgs e) {
            if (e.DeviceStatus == DeviceConnectionStatus.Disconnected) {
                OnDisconnect(sender, e);
            }

            this.onConnectionChangedCallbacks?.Invoke(this, e);
        }


        public event OnConnectionChanged OnConnectionChangedEvent {
            add => this.onConnectionChangedCallbacks += value;
            remove {
                if (this.onConnectionChangedCallbacks != null) {
                    this.onConnectionChangedCallbacks -= value;
                }
            }
        }

        private void Acquire() {
            if (initializeCount == 0) {

                Core.Events.SystemEventChanged += this.UsbHostEventChanged;
                Core.Events.StartSystemEventDetectionTask();

                this.LoadResources();

            }
            initializeCount++;
        }

        private void Release() {
            initializeCount--;

            if (initializeCount == 0) {

                Core.Events.SystemEventChanged -= this.UsbHostEventChanged;
                Core.Events.StopSystemEventDetectionTask();

                this.UnLoadResources();
            }
        }

        private void LoadResources() {

            //TODO
        }

        private void UnLoadResources() {

            //TODO
        }

        ~UsbHostController() {
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
