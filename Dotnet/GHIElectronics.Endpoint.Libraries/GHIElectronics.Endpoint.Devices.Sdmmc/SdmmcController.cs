using System.Collections;
using GHIElectronics.Endpoint.Pins; 
namespace GHIElectronics.Endpoint.Devices.Mmc {

    public class Sdmmc {
        public string DeviceName { get; }        
        public int DeviceId { get; }

        public static int CurrentId;

        public SdmmcType Type { get; }

        public Sdmmc(int id, string name, SdmmcType type) {
            this.DeviceName = name;            
            this.DeviceId = id;
            this.Type = type;
        }

    }

    public enum SdmmcType: uint {
        SdCard1 = 1,
        SdCard2 = 0,
        Emmc = 2,
    }
    public enum DeviceConnectionStatus {
        Disconnected = 0,
        Connected = 1,
        Bad = 2,
    };

    public delegate void OnConnectionChanged(SdmmcController sender, DeviceConnectionEventArgs e);

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
    public class SdmmcController : IDisposable {

        private static bool enabled;
        private static ArrayList devices;
        private static int initializeCount;


        private bool disposed = false;

        private OnConnectionChanged onConnectionChangedCallbacks;
        SdmmcType Type { get; }

        public SdmmcController(SdmmcType type) {
            devices = new ArrayList();
            enabled = false;
            this.Type = type;   

            this.Acquire();
        }
        public void Enable() {
            enabled = true; ;

            this.TaskEvent();

        }

        public void Disable() => enabled = false;

        private bool CheckSdConnection(string path, string pattern, SdmmcType type) {

            var hasRemoved = false;
            string[] files = null;


            if (Directory.Exists(path)) {
                files = Directory.GetFiles(path, pattern);

                if (files != null && files.Length > 0) {
                    foreach (var file in files) {
                        var found = false;

                        foreach (Sdmmc d in devices) {
                            if (d.DeviceName == file && d.Type == type) {
                                found = true;
                                break;
                            }
                        }

                        if (!found) {
                           

                            var d = new Sdmmc(++Sdmmc.CurrentId, file, type);

                            devices.Add(d);

                            this.OnConnectionChangedCallBack(this, new DeviceConnectionEventArgs(d.DeviceId,  d.DeviceName, DeviceConnectionStatus.Connected));

                        }
                    }
                }
            }

            foreach (Sdmmc d in devices) {
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

        private void OnConnectionChangedCallBack(SdmmcController sender, DeviceConnectionEventArgs e) => this.onConnectionChangedCallbacks?.Invoke(sender, e);


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
                    case SdmmcType.SdCard1:
                        this.CheckSdConnection("/dev/", "mmcblk1", this.Type); // sd1
                        break;
                    case SdmmcType.SdCard2:
                        this.CheckSdConnection("/dev/", "mmcblk0", this.Type); // sd1
                        break;
                    case SdmmcType.Emmc:
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
            if (this.Type == SdmmcType.SdCard2) {

                var pinConfig = STM32MP1.Sdmmc.PinSettings[STM32MP1.Sdmmc.SdCard2];

                STM32MP1.GpioPin.SetModer(pinConfig.PinD0, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD1, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD2, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD3, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetModer(pinConfig.PinCmd, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetModer(pinConfig.PinClock, STM32MP1.Moder.Alternate);
                

                STM32MP1.GpioPin.SetAlternate(pinConfig.PinD0, pinConfig.AlternatePinD0);
                STM32MP1.GpioPin.SetAlternate(pinConfig.PinD1, pinConfig.AlternatePinD1);
                STM32MP1.GpioPin.SetAlternate(pinConfig.PinD2, pinConfig.AlternatePinD2);
                STM32MP1.GpioPin.SetAlternate(pinConfig.PinD3, pinConfig.AlternatePinD3);
                STM32MP1.GpioPin.SetAlternate(pinConfig.PinCmd, pinConfig.AlternatePinCmd);
                STM32MP1.GpioPin.SetAlternate(pinConfig.PinClock, pinConfig.AlternatePinClock);
                
            }
        }

        private void UnLoadResources() {
            if (this.Type == SdmmcType.SdCard2) {
                var pinConfig = STM32MP1.Sdmmc.PinSettings[STM32MP1.Sdmmc.SdCard2];                

                STM32MP1.GpioPin.SetModer(pinConfig.PinD0, STM32MP1.Moder.Input);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD1, STM32MP1.Moder.Input);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD2, STM32MP1.Moder.Input);
                STM32MP1.GpioPin.SetModer(pinConfig.PinD3, STM32MP1.Moder.Input);
                STM32MP1.GpioPin.SetModer(pinConfig.PinCmd, STM32MP1.Moder.Input);
                STM32MP1.GpioPin.SetModer(pinConfig.PinClock, STM32MP1.Moder.Input);
            }
        }

        ~SdmmcController() {
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
