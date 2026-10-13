using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>
    /// A USB device that the program uses: Keyboard, Mouse, Joystick, HidDevice,
    /// Printer or RawDevice, opened with the DeviceName that its UsbHostController event gives.
    /// Its events come on the device's own thread. When the device is unplugged,
    /// Disconnected is raised. A device stays alive until it is disposed.
    /// </summary>
    public abstract unsafe class BaseDevice : IDisposable {
        private enum Kind { Input, Hid, Raw, Printer }

        private readonly GCHandle self;
        private readonly Kind kind;
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
            this.kind = type switch {
                DeviceType.Mouse or DeviceType.Keyboard or DeviceType.Joystick => Kind.Input,
                DeviceType.HID => Kind.Hid,
                DeviceType.Printer => Kind.Printer,
                _ => Kind.Raw,
            };
            this.self = GCHandle.Alloc(this);
            var context = GCHandle.ToIntPtr(this.self);
            try {
                int vendorId, productId;
                switch (this.kind) {
                    case Kind.Input:
                        Libendpoint.Check(Libendpoint.InputOpen(deviceName, (int)type, &InputChanged, context, out this.handle));
                        Libendpoint.Check(Libendpoint.InputInfo(this.handle, out vendorId, out productId, out _, out this.capabilities));
                        break;
                    case Kind.Hid:
                        Libendpoint.Check(Libendpoint.HidOpen(deviceName, &Unplugged, context, out this.handle));
                        Libendpoint.Check(Libendpoint.HidInfo(this.handle, out vendorId, out productId, out _, null, 0));
                        break;
                    case Kind.Printer:
                        Libendpoint.Check(Libendpoint.PrinterOpen(deviceName, &Unplugged, context, out this.handle));
                        Libendpoint.Check(Libendpoint.PrinterInfo(this.handle, out vendorId, out productId, out _));
                        break;
                    default:
                        Libendpoint.Check(Libendpoint.UsbRawOpen(deviceName, &Unplugged, context, out this.handle));
                        Libendpoint.Check(Libendpoint.UsbRawInfo(this.handle, out vendorId, out productId, out _));
                        break;
                }
                this.VendorId = (ushort)vendorId;
                this.ProductId = (ushort)productId;
            }
            catch {
                this.Close(Interlocked.Exchange(ref this.handle, 0));
                this.self.Free();
                throw;
            }
        }

        /// <summary>What the device was opened with: "/dev/input/event2", "/dev/hidraw0", "/dev/usb/lp0", "/dev/bus/usb/001/005"</summary>
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
                int connected;
                switch (this.kind) {
                    case Kind.Input:
                        Libendpoint.Check(Libendpoint.InputInfo(device, out _, out _, out connected, out _));
                        break;
                    case Kind.Hid:
                        Libendpoint.Check(Libendpoint.HidInfo(device, out _, out _, out connected, null, 0));
                        break;
                    case Kind.Printer:
                        Libendpoint.Check(Libendpoint.PrinterInfo(device, out _, out _, out connected));
                        break;
                    default:
                        Libendpoint.Check(Libendpoint.UsbRawInfo(device, out _, out _, out connected));
                        break;
                }
                return connected != 0;
            }
        }

        /// <summary>Stops the device's events (one being delivered finishes first) and closes it</summary>
        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                this.Close(old);
                this.self.Free();
            }
        }

        private void Close(nint device) {
            if (device == 0) {
                return;
            }
            switch (this.kind) {
                case Kind.Input: Libendpoint.InputClose(device); break;
                case Kind.Hid: Libendpoint.HidClose(device); break;
                case Kind.Printer: Libendpoint.PrinterClose(device); break;
                default: Libendpoint.UsbRawClose(device); break;
            }
        }

        private protected nint Handle() => this.handle != 0 ? this.handle : throw new ObjectDisposedException(this.GetType().Name);

        // A keyboard's, mouse's or joystick's events (see Input::Event in libendpoint)
        private protected virtual void OnEvent(int kind, int a, int b, int c, int d, int e) {
        }

        // From the device's thread. A handler's exception must not stop the
        // events (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void InputChanged(nint context, int kind, int a, int b, int c, int d, int e) {
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

        [UnmanagedCallersOnly]
        private static void Unplugged(nint context) {
            try {
                if (GCHandle.FromIntPtr(context).Target is BaseDevice device) {
                    device.Disconnected?.Invoke(device, EventArgs.Empty);
                }
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in Disconnected: {exception}");
            }
        }
    }
}
