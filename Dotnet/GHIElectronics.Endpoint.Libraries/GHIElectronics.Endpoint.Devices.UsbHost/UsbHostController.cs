using System;
using System.Collections;
using System.Collections.Specialized;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Usb;

namespace GHIElectronics.Endpoint.Devices.UsbHost
{
    public enum DeviceConnectionStatus
    {
        Disconnected = 0,
        Connected = 1,
        Bad = 2,
    };

    public delegate void OnConnectionChanged(UsbHostController sender, DeviceConnectionEventArgs e);

    public class DeviceConnectionEventArgs : EventArgs
    {
        //private readonly uint id;
        //private readonly byte interfaceIndex;
        //private readonly BaseDevice.DeviceType type;
        //private readonly ushort vendorId;
        //private readonly ushort productId;
        //private readonly byte portNumber;
        //private readonly DeviceConnectionStatus deviceStatus;

        ///// <summary>The device id.</summary>
        //public uint Id => this.id;

        ///// <summary>The logical device interface index.</summary>
        //public byte InterfaceIndex => this.interfaceIndex;

        ///// <summary>The device's type.</summary>
        //public BaseDevice.DeviceType Type => this.type;

        ///// <summary>The devic's vendor id.</summary>
        //public ushort VendorId => this.vendorId;

        ///// <summary>The device's product id.</summary>
        //public ushort ProductId => this.productId;

        ///// <summary>The device's USB port number.</summary>
        //public byte PortNumber => this.portNumber;

        public DeviceConnectionStatus DeviceStatus { get; }

        public string DeviceName { get; }
        public DeviceType Type { get; }
        public int DeviceId { get; }


        internal DeviceConnectionEventArgs(int id, DeviceType type, string name, DeviceConnectionStatus deviceStatus)
        {
            this.DeviceId = id;
            this.DeviceName = name;
            this.Type = type;
            this.DeviceStatus = deviceStatus;
        }
    }
    public class UsbHostController : IDisposable
    {

        private static bool enabled;
        private static ArrayList devices;
        private static object listLock;
        private static int initializeCount;


        private bool disposed = false;

        private OnConnectionChanged onConnectionChangedCallbacks;

        public UsbHostController()
        {
            devices = new ArrayList();
            enabled = false;
            listLock = new object();


            this.Acquire();
        }
        public void Enable()
        {
            enabled = true; ;

            this.TaskEvent();

        }

        public void Disable()
        {
            enabled = false; ;
        }

        private bool CheckUsbConnection(string path, string pattern, DeviceType type)
        {

            var hasRemoved = false;
            string[] files = null;


            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path, pattern);

                if (files != null && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        var found = false;

                        foreach (BaseDevice d in devices)
                        {
                            if (d.DeviceName == file && d.Type == type)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            if (type == DeviceType.MassStorage && file.Length <= 8)
                                continue;

                            var d = new BaseDevice(++BaseDevice.CurrentId, file, type);

                            devices.Add(d);

                            this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs(d.DeviceId, d.Type, d.DeviceName, DeviceConnectionStatus.Connected));

                        }
                    }
                }
            }

            foreach (BaseDevice d in devices)
            {
                var found = false;

                if (files != null && files.Length > 0)
                {
                    foreach (var f in files)
                    {
                        if (d.DeviceName == f && d.Type == type)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (d.Type == type && !found)
                {
                    devices.Remove(d);

                    this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs(d.DeviceId, d.Type, d.DeviceName, DeviceConnectionStatus.Disconnected));

                    hasRemoved = true;
                    break;

                }
            }

            return hasRemoved;
        }


        private Task TaskEvent() => Task.Run(() => {

            while (!this.disposed && enabled) {

                this.CheckUsbConnection("/dev/", "sd*", DeviceType.MassStorage);
                this.CheckUsbConnection("/dev/input/", "event*", DeviceType.HID);
                this.CheckUsbConnection("/dev/", "video*", DeviceType.Webcam);

                Thread.Sleep(1000);

            }
        });

        private void OnConnectionChangedCallBack(UsbHostController sender, DeviceConnectionEventArgs e) => this.onConnectionChangedCallbacks?.Invoke(sender, e);


        public event OnConnectionChanged OnConnectionChangedEvent
        {
            add => this.onConnectionChangedCallbacks += value;
            remove
            {
                if (this.onConnectionChangedCallbacks != null)
                {
                    this.onConnectionChangedCallbacks -= value;
                }
            }
        }

        private void Acquire()
        {
            if (initializeCount == 0)
            {              

                this.LoadResources();

            }
            initializeCount++;
        }

        private void Release()
        {
            initializeCount--;

            if (initializeCount == 0)
            {

               
                this.UnLoadResources();
            }
        }

        private void LoadResources()
        {
            var dev_videos = Directory.GetFiles("/dev/", "video*");

            if (dev_videos == null || dev_videos.Length == 0) {

                var script = new Script("modprobe", "./", "uvcvideo");
                script.Start();                
            }
            
        }

        private void UnLoadResources()
        {

            //TODO
        }

        /// <exclude />
        ~UsbHostController()
        {
            this.Dispose(disposing: false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
        protected void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {

            }

            this.Release();

            this.disposed = true;
        }
    }
}
