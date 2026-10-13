using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>
    /// A HID device that is no keyboard, mouse or joystick (a sensor, a USB I/O board, a
    /// UPS...): its reports, as hidapi gives them.
    /// </summary>
    /// <remarks>
    /// Open it with the DeviceName of a HID device from UsbHostController ("/dev/hidraw0").
    /// A report's first byte is its report ID: 0 for a device that does not number its reports.
    /// <code>
    /// var hid = new HidDevice(e.DeviceName);
    /// hid.Write([0x00, 0x01, 0x02]);                    // report ID 0, then the report
    /// var buffer = new byte[64];
    /// var count = hid.Read(buffer, 1000);              // an input report, 0 after 1 s without one
    /// </code>
    /// </remarks>
    public unsafe class HidDevice : BaseDevice {
        /// <summary>Opens the HID device that UsbHostController reported ("/dev/hidraw0").</summary>
        public HidDevice(string deviceName) : base(deviceName, DeviceType.HID) {
            var name = stackalloc byte[256];
            Libendpoint.Check(Libendpoint.HidInfo(this.Handle(), out _, out _, out _, name, 256));
            this.ProductName = Marshal.PtrToStringUTF8((nint)name) ?? "";
        }

        /// <summary>The device's name ("Microchip Technology Inc. MCP2221 USB-I2C/UART Combo")</summary>
        public string ProductName { get; }

        /// <summary>The report descriptor: what reports the device has and what is in them.</summary>
        public byte[] GetReportDescriptor() {
            var buffer = new byte[4096];
            fixed (byte* bytes = buffer) {
                Libendpoint.Check(Libendpoint.HidReportDescriptor(this.Handle(), bytes, buffer.Length, out var length));
                return buffer[..Math.Min(length, buffer.Length)];
            }
        }

        /// <summary>Sends an output report. Its first byte is the report ID (0 if the device has none).</summary>
        /// <returns>The number of bytes written.</returns>
        public int Write(ReadOnlySpan<byte> report) {
            fixed (byte* bytes = report) {
                Libendpoint.Check(Libendpoint.HidWrite(this.Handle(), bytes, report.Length, out var written));
                return written;
            }
        }

        /// <summary>Reads an input report (its first byte is its report ID if the device numbers its reports).</summary>
        /// <param name="buffer">Where the report goes.</param>
        /// <param name="timeout">How long to wait for one, in milliseconds (Timeout.Infinite: as long as it takes).</param>
        /// <returns>The report's length, 0 if none came in time.</returns>
        public int Read(Span<byte> buffer, int timeout = Timeout.Infinite) {
            fixed (byte* bytes = buffer) {
                Libendpoint.Check(Libendpoint.HidRead(this.Handle(), bytes, buffer.Length, timeout, out var length));
                return length;
            }
        }

        /// <summary>Gets a feature report. buffer[0] is the report ID (0 if the device has none).</summary>
        /// <returns>The report's length, with its report ID.</returns>
        public int GetFeatureReport(Span<byte> buffer) {
            fixed (byte* bytes = buffer) {
                Libendpoint.Check(Libendpoint.HidGetFeature(this.Handle(), bytes, buffer.Length, out var length));
                return length;
            }
        }

        /// <summary>Sends a feature report. Its first byte is the report ID (0 if the device has none).</summary>
        public void SendFeatureReport(ReadOnlySpan<byte> report) {
            fixed (byte* bytes = report) {
                Libendpoint.Check(Libendpoint.HidSendFeature(this.Handle(), bytes, report.Length));
            }
        }
    }
}
