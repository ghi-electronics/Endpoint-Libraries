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

        /// <summary>"/dev/usb/lp0": open it with new Printer(e.DeviceName)</summary>
        Printer = 7,

        /// <summary>A serial port, for System.IO.Ports.SerialPort ("/dev/ttyUSB0", "/dev/ttyACM0")</summary>
        Serial = 8,

        /// <summary>"/dev/video0": open it with UsbCamera (GHIElectronics.Endpoint.Devices.Camera)</summary>
        Webcam = 9,

        /// <summary>A sound card: speaker, microphone ("hw:1"), for Iot.Device.Media.SoundDevice</summary>
        Audio = 10,

        /// <summary>An Ethernet, WiFi or cellular adapter: its NetworkInterface's Name ("eth1", "wlan1")</summary>
        Network = 11,

        /// <summary>"hci0"</summary>
        Bluetooth = 12,

        /// <summary>"/dev/input/eventN": open it with Touch (built-in or USB touchscreen)</summary>
        Touch = 13,
    }

    /// <summary>Event data for <see cref="UsbHostController.ConnectionChanged"/>: one USB device attached or detached.</summary>
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

        /// <summary>Whether the device was just connected or just disconnected.</summary>
        public DeviceConnectionStatus DeviceStatus { get; }

        /// <summary>The device's number: the same for all its events, a new one when it is plugged in again</summary>
        public uint Id { get; }

        /// <summary>What the device gives (see <see cref="DeviceType"/>); hints what to open <see cref="DeviceName"/> with.</summary>
        public DeviceType Type { get; }

        /// <summary>USB vendor id (VID) from the device descriptor.</summary>
        public ushort VendorId { get; }

        /// <summary>USB product id (PID) from the device descriptor.</summary>
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

    /// <summary>
    /// Handler for <see cref="UsbHostController.ConnectionChanged"/>. Called on the
    /// library's USB thread when a device is attached or detached; the devices already
    /// plugged in at <see cref="UsbHostController.Enable"/> time are reported too.
    /// </summary>
    /// <param name="sender">The controller that saw the change.</param>
    /// <param name="e">Details about the device and whether it was attached or detached.</param>
    public delegate void UsbConnectionChangedEventHandler(UsbHostController sender, UsbDeviceConnectionEventArgs e);

    /// <summary>
    /// The USB devices plugged into the board, directly or through hubs. One
    /// event for each thing a device gives: a keyboard with a touchpad gives
    /// Keyboard and Mouse, a webcam gives Webcam and Audio (its microphone).
    /// <c>DeviceName</c> is what to open it with (see <see cref="DeviceType"/>).
    /// The events come on the library's USB thread. USB drives come as
    /// MassStorage: use <c>StorageController</c>
    /// (<c>GHIElectronics.Endpoint.Devices.Storage</c>) for them. A controller
    /// stays alive until it is disposed.
    /// </summary>
    /// <example>
    /// <code>
    /// var usb = UsbHostController.GetDefault();
    /// usb.ConnectionChanged += (sender, e) => {
    ///     if (e.DeviceStatus == DeviceConnectionStatus.Connected &amp;&amp; e.Type == DeviceType.Serial) {
    ///         var port = new SerialPort(e.DeviceName, 115200);   // "/dev/ttyUSB0"
    ///         port.Open();
    ///     }
    /// };
    /// usb.Enable();   // the devices already plugged in are reported too
    /// </code>
    /// </example>
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

        /// <summary>A new USB host controller for the board. Call <see cref="Enable"/> to start seeing devices.</summary>
        public static UsbHostController GetDefault() => new();

        /// <summary>A device connected or disconnected</summary>
        public event UsbConnectionChangedEventHandler? ConnectionChanged;

        /// <summary>Starts reporting devices: first the ones already plugged in</summary>
        public void Enable() => Libendpoint.Check(Libendpoint.UsbHostEnable(this.Handle()));

        /// <summary>Stops reporting devices. Already-reported devices stay accessible until they are unplugged.</summary>
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
                    controller.ConnectionChanged?.Invoke(controller, new UsbDeviceConnectionEventArgs(
                        (DeviceConnectionStatus)status, id, (DeviceType)type, (ushort)vendorId, (ushort)productId,
                        Text(deviceName), Text(manufacturer), Text(product), Text(serialNumber)));
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine($"Unhandled exception in ConnectionChanged: {e}");
            }
        }
    }
}
