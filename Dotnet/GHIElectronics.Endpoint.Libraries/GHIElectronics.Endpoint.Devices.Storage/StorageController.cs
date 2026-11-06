using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Storage {
    /// <summary>Event data for <see cref="StorageController.ConnectionChanged"/>: one drive attached or detached.</summary>
    public sealed class DeviceConnectionEventArgs : EventArgs {
        internal DeviceConnectionEventArgs(DeviceConnectionStatus deviceStatus, IntPtr hdc, string volumeLabel) {
            this.DeviceStatus = deviceStatus;
            this.Hdc = hdc;
            this.VolumeLabel = volumeLabel;
        }

        /// <summary>Whether the drive was just connected or just disconnected.</summary>
        public DeviceConnectionStatus DeviceStatus { get; }

        /// <summary>The drive, for FileSystem.Mount, Flush and Unmount, until it is pulled out</summary>
        public IntPtr Hdc { get; }

        /// <summary>
        /// The drive's name ("LOGGER"), empty if it has none. (On Endpoint,
        /// <see cref="System.IO.DriveInfo.VolumeLabel"/> returns the drive's mount
        /// folder, not its label; read this property instead.)
        /// </summary>
        public string VolumeLabel { get; }
    }

    /// <summary>
    /// Handler for <see cref="StorageController.ConnectionChanged"/>. Called on the library's
    /// storage thread when a drive is attached or detached; the drives already plugged in
    /// at <see cref="StorageController.Enable"/> time are reported too.
    /// </summary>
    /// <param name="sender">The controller that saw the change.</param>
    /// <param name="e">Details about the drive and whether it was attached or detached.</param>
    public delegate void ConnectionChangedEventHandler(StorageController sender, DeviceConnectionEventArgs e);

    /// <summary>
    /// The SD card slot or the USB drives. Events come on the library's storage
    /// thread. A drive that is pulled out is unmounted by the library. The SD
    /// card is at <c>/media/sd/</c>; USB drives get the first free
    /// <c>/media/usbN/</c>, as Windows gives the first free drive letter. A
    /// controller stays alive until it is disposed.
    /// </summary>
    /// <example>
    /// <code>
    /// var usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage);
    /// usb.ConnectionChanged += (sender, e) => {
    ///     if (e.DeviceStatus == DeviceConnectionStatus.Connected) {
    ///         var drive = FileSystem.Mount(e.Hdc);          // "/media/usb0/", usb1, ...
    ///         File.WriteAllText(drive.Name + "log.txt", "Hello");
    ///         FileSystem.Flush(e.Hdc);                      // before the drive is pulled out
    ///     }
    /// };
    /// usb.Enable();   // the drives already plugged in are reported too
    /// </code>
    /// </example>
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

        /// <summary>The name this controller was created with (e.g. <c>EPM815.StorageController.SdCard</c>).</summary>
        public string Name { get; }

        /// <summary>A drive connected or disconnected</summary>
        public event ConnectionChangedEventHandler? ConnectionChanged;

        /// <summary>Starts reporting drives: first the ones already plugged in</summary>
        public void Enable() => Libendpoint.Check(Libendpoint.StorageEnable(this.Handle()));

        /// <summary>Stops reporting drives. Already-reported drives stay mounted; call <see cref="FileSystem.Unmount"/> to unmount them.</summary>
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
                    controller.ConnectionChanged?.Invoke(controller,
                        new DeviceConnectionEventArgs((DeviceConnectionStatus)status, hdc, label));
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine($"Unhandled exception in ConnectionChanged: {e}");
            }
        }
    }

    /// <summary>
    /// Mount / flush / unmount a drive the <see cref="StorageController"/> reported.
    /// Pass the <see cref="DeviceConnectionEventArgs.Hdc"/> from the event.
    /// </summary>
    public static unsafe class FileSystem {
        /// <summary>Mounts the drive (if it is not yet) and gives its folder, "/media/usb0/"</summary>
        public static DriveInfo Mount(IntPtr hdc) {
            var name = stackalloc byte[256];
            Libendpoint.Check(Libendpoint.StorageMount(hdc, name, 256));
            return new DriveInfo(Marshal.PtrToStringUTF8((nint)name)!);
        }

        /// <summary>Writes everything to the drive: call it before the drive is pulled out</summary>
        public static void Flush(IntPtr hdc) => Libendpoint.Check(Libendpoint.StorageFlush(hdc));

        /// <summary>Unmounts the drive. Call <see cref="Flush"/> first if data was written and the drive is about to be pulled out.</summary>
        public static void Unmount(IntPtr hdc) => Libendpoint.Check(Libendpoint.StorageUnmount(hdc));
    }
}
