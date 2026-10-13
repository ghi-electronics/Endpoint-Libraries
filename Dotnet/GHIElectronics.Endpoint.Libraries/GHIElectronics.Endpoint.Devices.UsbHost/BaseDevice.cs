using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>
    /// A USB keyboard, mouse or joystick, opened with the DeviceName that its
    /// UsbHostController event gives ("/dev/input/event2"). Its events come on
    /// the device's own thread. When the device is unplugged, Disconnected is
    /// raised. A device stays alive until it is disposed.
    /// </summary>
    public abstract unsafe class BaseDevice : IDisposable {
        private readonly GCHandle self;
        private nint handle;
        private protected readonly int capabilities;

        /// <summary>The event handler type for when the device disconnects.</summary>
        public delegate void DisconnectedEventHandler(BaseDevice sender, EventArgs e);

        /// <summary>The event is fired when the device disconnects.</summary>
        public event DisconnectedEventHandler? Disconnected;

        private protected BaseDevice(string deviceName, DeviceType type) {
            ArgumentNullException.ThrowIfNull(deviceName);
            this.DeviceName = deviceName;
            this.Type = type;
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.InputOpen(deviceName, (int)type, &Changed, GCHandle.ToIntPtr(this.self), out this.handle));
                Libendpoint.Check(Libendpoint.InputInfo(this.handle, out var vendorId, out var productId, out _, out this.capabilities));
                this.VendorId = (ushort)vendorId;
                this.ProductId = (ushort)productId;
            }
            catch {
                if (this.handle != 0) {
                    Libendpoint.InputClose(this.handle);
                    this.handle = 0;
                }
                this.self.Free();
                throw;
            }
        }

        /// <summary>What the device was opened with: "/dev/input/event2"</summary>
        public string DeviceName { get; }

        /// <summary>The device's type.</summary>
        public DeviceType Type { get; }

        /// <summary>The device's vendor id.</summary>
        public ushort VendorId { get; }

        /// <summary>The device's product id.</summary>
        public ushort ProductId { get; }

        /// <summary>Whether or not the device is connected.</summary>
        public bool Connected {
            get {
                var device = this.handle;
                if (device == 0) {
                    return false;
                }
                Libendpoint.Check(Libendpoint.InputInfo(device, out _, out _, out var connected, out _));
                return connected != 0;
            }
        }

        /// <summary>Stops the device's events (one being delivered finishes first)</summary>
        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.InputClose(old);
                this.self.Free();
            }
        }

        private protected nint Handle() => this.handle != 0 ? this.handle : throw new ObjectDisposedException(this.GetType().Name);

        // The device's events: see Input::Event in libendpoint
        private protected abstract void OnEvent(int kind, int a, int b, int c, int d, int e);

        // From the device's thread. A handler's exception must not stop the
        // events (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void Changed(nint context, int kind, int a, int b, int c, int d, int e) {
            try {
                if (GCHandle.FromIntPtr(context).Target is BaseDevice device) {
                    if (kind == 8) {
                        device.Disconnected?.Invoke(device, EventArgs.Empty);
                    }
                    else {
                        device.OnEvent(kind, a, b, c, d, e);
                    }
                }
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in a USB device event: {exception}");
            }
        }
    }
}
