using System.Runtime.InteropServices;
using System.Text;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>What a printer reports about itself.</summary>
    [Flags]
    public enum PrinterStatus {
        /// <summary>Ready to print.</summary>
        Ready = 0,

        /// <summary>Out of paper.</summary>
        PaperOut = 0x01,

        /// <summary>Not online (switched off line, cover open...).</summary>
        Offline = 0x02,

        /// <summary>An error the printer reports.</summary>
        Error = 0x04,
    }

    /// <summary>
    /// A USB printer: the bytes it understands (ESC/POS for receipt printers, ZPL or EPL
    /// for label printers, PCL or PostScript for office printers) go to it as they are.
    /// </summary>
    /// <remarks>
    /// Open it with the DeviceName of a Printer from UsbHostController ("/dev/usb/lp0").
    /// <code>
    /// var printer = new Printer(e.DeviceName);
    /// printer.Write("Hello\n\n\n");
    /// printer.Write([0x1D, 0x56, 0x00]);          // ESC/POS: cut the paper
    /// </code>
    /// </remarks>
    public unsafe class Printer : BaseDevice {
        /// <summary>Opens the printer that UsbHostController reported ("/dev/usb/lp0").</summary>
        public Printer(string deviceName) : base(deviceName, DeviceType.Printer) {
        }

        /// <summary>How long Write waits for the printer to take the data, in milliseconds (Timeout.Infinite: as long as it takes).</summary>
        public int WriteTimeout { get; set; } = Timeout.Infinite;

        /// <summary>The printer's IEEE 1284 Device ID: "MFG:EPSON;CMD:ESC/POS;MDL:TM-T20II;CLS:PRINTER;"</summary>
        public string DeviceId {
            get {
                var text = stackalloc byte[1024];
                Libendpoint.Check(Libendpoint.PrinterDeviceId(this.Handle(), text, 1024));
                return Marshal.PtrToStringUTF8((nint)text) ?? "";
            }
        }

        /// <summary>Paper out, off line or an error, as the printer reports them (Ready: none).</summary>
        public PrinterStatus Status {
            get {
                Libendpoint.Check(Libendpoint.PrinterStatus(this.Handle(), out var status));
                return (PrinterStatus)status;
            }
        }

        /// <summary>Sends bytes to the printer.</summary>
        /// <returns>The number of bytes sent: all of them.</returns>
        /// <exception cref="TimeoutException">The printer did not take them within WriteTimeout.</exception>
        public int Write(ReadOnlySpan<byte> data) {
            fixed (byte* bytes = data) {
                Libendpoint.Check(Libendpoint.PrinterWrite(this.Handle(), bytes, data.Length, this.WriteTimeout, out var written));
                return written;
            }
        }

        /// <summary>Sends text to the printer, as UTF-8.</summary>
        public int Write(string text) {
            ArgumentNullException.ThrowIfNull(text);
            return this.Write(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Reads what the printer answers (to a status request).</summary>
        /// <param name="buffer">Where the answer goes.</param>
        /// <param name="timeout">How long to wait, in milliseconds (Timeout.Infinite: as long as it takes).</param>
        /// <returns>The answer's length, 0 if none came in time.</returns>
        public int Read(Span<byte> buffer, int timeout = Timeout.Infinite) {
            fixed (byte* bytes = buffer) {
                Libendpoint.Check(Libendpoint.PrinterRead(this.Handle(), bytes, buffer.Length, timeout, out var length));
                return length;
            }
        }
    }
}
