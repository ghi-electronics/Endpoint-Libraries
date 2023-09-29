using System.Collections;

namespace GHIElectronic.Endpoint.Devices.Mmc {

    public class Mmc {
        public string DeviceName { get; }        
        public int DeviceId { get; }

        public static int CurrentId;

        public MmcType Type { get; }

        public Mmc(int id, string name, MmcType type) {
            this.DeviceName = name;            
            this.DeviceId = id;
            this.Type = type;
        }

    }

    public enum MmcType: uint {
        SdCard1 = 1,
        SdCard2 = 0,
        Emmc = 2,
    }
    public enum DeviceConnectionStatus {
        Disconnected = 0,
        Connected = 1,
        Bad = 2,
    };

    public delegate void OnConnectionChanged(MmcController sender, DeviceConnectionEventArgs e);

    public class DeviceConnectionEventArgs : EventArgs {
       

        public DeviceConnectionStatus DeviceStatus { get; }

        public string DeviceName { get; }        
        public int DeviceId { get; }


        internal DeviceConnectionEventArgs(int id,  string name, DeviceConnectionStatus deviceStatus) {
            this.DeviceId = id;
            this.DeviceName = name;            
            this.DeviceStatus = deviceStatus;
        }
    }
    public class MmcController : IDisposable {

        private static bool enabled;
        private static ArrayList devices;
        private static int initializeCount;


        private bool disposed = false;

        private OnConnectionChanged onConnectionChangedCallbacks;
        MmcType Type { get; }

        public MmcController(MmcType type) {
            devices = new ArrayList();
            enabled = false;
            this.Type = type;   

            this.Acquire();
        }
        public void Enable() {
            enabled = true; ;

            this.TaskEvent();

        }

        public void Disable() {
            enabled = false; ;
        }

        private bool CheckSdConnection(string path, string pattern, MmcType type) {

            var hasRemoved = false;
            string[] files = null;


            if (Directory.Exists(path)) {
                files = Directory.GetFiles(path, pattern);

                if (files != null && files.Length > 0) {
                    foreach (var file in files) {
                        var found = false;

                        foreach (Mmc d in devices) {
                            if (d.DeviceName == file && d.Type == type) {
                                found = true;
                                break;
                            }
                        }

                        if (!found) {
                           

                            var d = new Mmc(++Mmc.CurrentId, file, type);

                            devices.Add(d);

                            this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs(d.DeviceId,  d.DeviceName, DeviceConnectionStatus.Connected));

                        }
                    }
                }
            }

            foreach (Mmc d in devices) {
                var found = false;

                if (files != null && files.Length > 0) {
                    foreach (var f in files) {
                        if (d.DeviceName == f && d.Type == type) {
                            found = true;
                            break;
                        }
                    }
                }

                if (d.Type == type && !found) { 
                    devices.Remove(d);

                    this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs(d.DeviceId, d.DeviceName, DeviceConnectionStatus.Disconnected));

                    hasRemoved = true;
                    break;

                }
            }

            return hasRemoved;
        }

        private void OnConnectionChangedCallBack(MmcController sender, DeviceConnectionEventArgs e) => this.onConnectionChangedCallbacks?.Invoke(sender, e);


        public event OnConnectionChanged OnConnectionChangedEvent {
            add => this.onConnectionChangedCallbacks += value;
            remove {
                if (this.onConnectionChangedCallbacks != null) {
                    this.onConnectionChangedCallbacks -= value;
                }
            }
        }

        private Task TaskEvent() => Task.Run(() => {

            while (!this.disposed && enabled) {

                switch (this.Type) {
                    case MmcType.SdCard1:
                        this.CheckSdConnection("/dev/", "mmcblk1", this.Type); // sd1
                        break;
                    case MmcType.SdCard2:
                        this.CheckSdConnection("/dev/", "mmcblk0", this.Type); // sd1
                        break;
                    case MmcType.Emmc:
                        this.CheckSdConnection("/dev/", "mmcblk2", this.Type); // sd1
                        break;

                }               

                Thread.Sleep(1000);

            }
        });

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

        private void LoadResources() {

            //TODO
        }

        private void UnLoadResources() {

            //TODO
        }

        ~MmcController() {
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
