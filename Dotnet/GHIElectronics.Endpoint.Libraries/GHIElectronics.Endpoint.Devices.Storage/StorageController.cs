using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Storage {
    public enum DeviceConnectionStatus {
        Disconnected = 0,
        Connected = 1,

        /// <summary>Plugged in, but with no file system the board reads (format it as FAT32 or exFAT)</summary>
        Bad = 2,
    }

    public sealed class DeviceConnectionEventArgs : EventArgs {
        internal DeviceConnectionEventArgs(DeviceConnectionStatus deviceStatus, IntPtr hdc, string volumeLabel) {
            this.DeviceStatus = deviceStatus;
            this.Hdc = hdc;
            this.VolumeLabel = volumeLabel;
        }

        public DeviceConnectionStatus DeviceStatus { get; }

        /// <summary>The drive, for FileSystem.Mount, Flush and Unmount, until it is pulled out</summary>
        public IntPtr Hdc { get; }

        /// <summary>
        /// The drive's name ("LOGGER"), empty if it has none. (.NET's DriveInfo.VolumeLabel
        /// gives the folder on Linux.)
        /// </summary>
        public string VolumeLabel { get; }
    }

    public delegate void ConnectionChangedEventHandler(StorageController sender, DeviceConnectionEventArgs e);

    /// <summary>
    /// The SD card slot or the USB drives:
    /// <code>
    /// var usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage);
    /// usb.OnConnectionChangedEvent += (sender, e) => {
    ///     if (e.DeviceStatus == DeviceConnectionStatus.Connected) {
    ///         var drive = FileSystem.Mount(e.Hdc);          // "/media/usb0/", usb1, ...
    ///         File.WriteAllText(drive.Name + "log.txt", "Hello");
    ///         FileSystem.Flush(e.Hdc);                       // before the drive is pulled out
    ///     }
    /// };
    /// usb.Enable();   // the drives already plugged in are reported too
    /// </code>
    /// The events come on the library's storage thread. A drive that is pulled
    /// out is unmounted by the library. The SD card is /media/sd/; USB drives get
    /// the first free /media/usbN/, as Windows gives the first free drive letter.
    /// A controller stays alive until it is disposed.
    /// </summary>
    public sealed unsafe class StorageController : IDisposable {
        private readonly GCHandle self;
        private nint handle;

        private StorageController(string name) {
            this.Name = name;
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.StorageCreate(name, &Changed, GCHandle.ToIntPtr(this.self), out this.handle));
            }
            catch {
                this.self.Free();
                throw;
            }
        }

        /// <summary>EPM815.StorageController.SdCard or EPM815.StorageController.UsbHostMassStorage</summary>
        public static StorageController FromName(string name) => new(name);

        public string Name { get; }

        /// <summary>A drive connected or disconnected</summary>
        public event ConnectionChangedEventHandler? OnConnectionChangedEvent;

        /// <summary>Starts reporting drives: first the ones already plugged in</summary>
        public void Enable() => Libendpoint.Check(Libendpoint.StorageEnable(this.Handle()));

        public void Disable() => Libendpoint.Check(Libendpoint.StorageDisable(this.Handle()));

        /// <summary>Stops the events (one being delivered finishes first)</summary>
        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.StorageDispose(old);
                this.self.Free();
            }
        }

        private nint Handle() => this.handle != 0 ? this.handle : throw new ObjectDisposedException(nameof(StorageController));

        // From the library's storage thread. A handler's exception must not stop
        // the events (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void Changed(nint context, nint hdc, int status, byte* volumeLabel) {
            try {
                if (GCHandle.FromIntPtr(context).Target is StorageController controller) {
                    var label = volumeLabel != null ? Marshal.PtrToStringUTF8((nint)volumeLabel) ?? "" : "";
                    controller.OnConnectionChangedEvent?.Invoke(controller,
                        new DeviceConnectionEventArgs((DeviceConnectionStatus)status, hdc, label));
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine($"Unhandled exception in OnConnectionChangedEvent: {e}");
            }
        }
    }

    public static unsafe class FileSystem {
        /// <summary>Mounts the drive (if it is not yet) and gives its folder, "/media/usb0/"</summary>
        public static DriveInfo Mount(IntPtr hdc) {
            var name = stackalloc byte[256];
            Libendpoint.Check(Libendpoint.StorageMount(hdc, name, 256));
            return new DriveInfo(Marshal.PtrToStringUTF8((nint)name)!);
        }

        /// <summary>Writes everything to the drive: call it before the drive is pulled out</summary>
        public static void Flush(IntPtr hdc) => Libendpoint.Check(Libendpoint.StorageFlush(hdc));

        public static void Unmount(IntPtr hdc) => Libendpoint.Check(Libendpoint.StorageUnmount(hdc));
    }
}
