using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>What a device gives, and its DeviceName</summary>
    public enum DeviceType {
        /// <summary>No driver on the board: the program talks to it with RawDevice ("/dev/bus/usb/001/005")</summary>
        Unknown = 0,

        /// <summary>"/dev/bus/usb/001/002"</summary>
        Hub = 1,

        /// <summary>A HID device that is no keyboard, mouse or joystick: open it with HidDevice ("/dev/hidraw0")</summary>
        HID = 2,

        /// <summary>"/dev/input/event2": open it with new Mouse(e.DeviceName)</summary>
        Mouse = 3,

        /// <summary>"/dev/input/event1": open it with new Keyboard(e.DeviceName)</summary>
        Keyboard = 4,

        /// <summary>Joysticks and gamepads ("/dev/input/event3"): open it with new Joystick(e.DeviceName)</summary>
        Joystick = 5,

        /// <summary>A drive: use StorageController (GHIElectronics.Endpoint.Devices.Storage). No name.</summary>
        MassStorage = 6,

        /// <summary>"/dev/usb/lp0"</summary>
        Printer = 7,

        /// <summary>A serial port, for System.IO.Ports.SerialPort ("/dev/ttyUSB0", "/dev/ttyACM0")</summary>
        Serial = 8,

        /// <summary>"/dev/video0"</summary>
        Webcam = 9,

        /// <summary>A sound card: speaker, microphone ("hw:1")</summary>
        Audio = 10,

        /// <summary>An Ethernet, WiFi or cellular adapter: its NetworkInterface's Name ("eth1", "wlan1")</summary>
        Network = 11,

        /// <summary>"hci0"</summary>
        Bluetooth = 12,
    }

    public sealed class UsbDeviceConnectionEventArgs : EventArgs {
        internal UsbDeviceConnectionEventArgs(DeviceConnectionStatus deviceStatus, uint id, DeviceType type, ushort vendorId,
                                              ushort productId, string deviceName, string manufacturer, string product,
                                              string serialNumber) {
            this.DeviceStatus = deviceStatus;
            this.Id = id;
            this.Type = type;
            this.VendorId = vendorId;
            this.ProductId = productId;
            this.DeviceName = deviceName;
            this.Manufacturer = manufacturer;
            this.Product = product;
            this.SerialNumber = serialNumber;
        }

        public DeviceConnectionStatus DeviceStatus { get; }

        /// <summary>The device's number: the same for all its events, a new one when it is plugged in again</summary>
        public uint Id { get; }

        public DeviceType Type { get; }

        public ushort VendorId { get; }

        public ushort ProductId { get; }

        /// <summary>What to open: "/dev/ttyUSB0", "eth1"... (see DeviceType)</summary>
        public string DeviceName { get; }

        /// <summary>The device's own name for its maker ("Logitech"), empty if it has none</summary>
        public string Manufacturer { get; }

        /// <summary>The device's own name ("USB Receiver"), empty if it has none</summary>
        public string Product { get; }

        /// <summary>Empty if the device has none</summary>
        public string SerialNumber { get; }
    }

    public delegate void UsbConnectionChangedEventHandler(UsbHostController sender, UsbDeviceConnectionEventArgs e);

    /// <summary>
    /// The USB devices plugged into the board, directly or through hubs:
    /// <code>
    /// var usb = UsbHostController.GetDefault();
    /// usb.OnConnectionChangedEvent += (sender, e) => {
    ///     if (e.DeviceStatus == DeviceConnectionStatus.Connected &amp;&amp; e.Type == DeviceType.Serial) {
    ///         var port = new SerialPort(e.DeviceName, 115200);   // "/dev/ttyUSB0"
    ///         port.Open();
    ///     }
    /// };
    /// usb.Enable();   // the devices already plugged in are reported too
    /// </code>
    /// One event for each thing a device gives: a keyboard with a touchpad gives
    /// Keyboard and Mouse, a webcam gives Webcam and Audio (its microphone).
    /// DeviceName is what to open it with (see DeviceType). The events come on
    /// the library's USB thread. USB drives come as MassStorage: use
    /// StorageController (GHIElectronics.Endpoint.Devices.Storage) for them.
    /// A controller stays alive until it is disposed.
    /// </summary>
    public sealed unsafe class UsbHostController : IDisposable {
        private readonly GCHandle self;
        private nint handle;

        private UsbHostController() {
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.UsbHostCreate(&Changed, GCHandle.ToIntPtr(this.self), out this.handle));
            }
            catch {
                this.self.Free();
                throw;
            }
        }

        public static UsbHostController GetDefault() => new();

        /// <summary>A device connected or disconnected</summary>
        public event UsbConnectionChangedEventHandler? OnConnectionChangedEvent;

        /// <summary>Starts reporting devices: first the ones already plugged in</summary>
        public void Enable() => Libendpoint.Check(Libendpoint.UsbHostEnable(this.Handle()));

        public void Disable() => Libendpoint.Check(Libendpoint.UsbHostDisable(this.Handle()));

        /// <summary>Stops the events (one being delivered finishes first)</summary>
        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.UsbHostDispose(old);
                this.self.Free();
            }
        }

        private nint Handle() => this.handle != 0 ? this.handle : throw new ObjectDisposedException(nameof(UsbHostController));

        private static string Text(byte* text) => text != null ? Marshal.PtrToStringUTF8((nint)text) ?? "" : "";

        // From the library's USB thread. A handler's exception must not stop the
        // events (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void Changed(nint context, int status, uint id, int type, int vendorId, int productId, byte* deviceName,
                                    byte* manufacturer, byte* product, byte* serialNumber) {
            try {
                if (GCHandle.FromIntPtr(context).Target is UsbHostController controller) {
                    controller.OnConnectionChangedEvent?.Invoke(controller, new UsbDeviceConnectionEventArgs(
                        (DeviceConnectionStatus)status, id, (DeviceType)type, (ushort)vendorId, (ushort)productId,
                        Text(deviceName), Text(manufacturer), Text(product), Text(serialNumber)));
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine($"Unhandled exception in OnConnectionChangedEvent: {e}");
            }
        }
    }
}
