using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>
    /// Provides low level access functionalities for the connected USB device. This is useful if there is not already a predefined driver for that device.
    /// </summary>
    /// <remarks>
    /// Open it with the DeviceName of an Unknown device from UsbHostController ("/dev/bus/usb/001/005").
    /// An interface that a driver of the board has is taken from it while the program uses it, and
    /// given back when the device is disposed.
    /// <code>
    /// var device = new RawDevice(e.DeviceName);
    /// var configuration = device.GetConfigurationDescriptor(0);
    /// var input = configuration.Interfaces[0].Endpoints.First(p => (p.Address &amp; 0x80) != 0);
    /// using var pipe = device.OpenPipe(input);
    /// var count = pipe.Transfer(buffer);
    /// </code>
    /// </remarks>
    public unsafe class RawDevice : BaseDevice {
        /// <summary>Opens the device that UsbHostController reported ("/dev/bus/usb/001/005").</summary>
        public RawDevice(string deviceName) : base(deviceName, DeviceType.Unknown) {
        }

        /// <summary>The timeout of SendSetupPacket in milliseconds (0: none). Defaults to 5000 ms.</summary>
        public int SetupTimeout { get; set; } = 5000;

        /// <summary>Opens a new communication pipe.</summary>
        /// <param name="endpoint">The descriptor for the communication endpoint (bulk or interrupt).</param>
        /// <returns>The new pipe.</returns>
        public RawDevice.Pipe OpenPipe(Descriptors.Endpoint endpoint) {
            ArgumentNullException.ThrowIfNull(endpoint);
            this.Handle();
            return new Pipe(this, endpoint);
        }

        /// <summary>Sends a USB setup packet.</summary>
        /// <param name="requestType">The request type, recipient, and direction.</param>
        /// <param name="request">The request to make.</param>
        /// <param name="value">The value of the request.</param>
        /// <param name="index">The index of the request.</param>
        public void SendSetupPacket(byte requestType, byte request, ushort value, ushort index) =>
            this.SendSetupPacket(requestType, request, value, index, [], 0, 0);

        /// <summary>Sends a USB setup packet.</summary>
        /// <param name="requestType">The request type, recipient, and direction.</param>
        /// <param name="request">The request to make.</param>
        /// <param name="value">The value of the request.</param>
        /// <param name="index">The index of the request.</param>
        /// <param name="data">The data to send or receive.</param>
        /// <param name="dataOffset">The offset into data at which to send or receive the data.</param>
        /// <param name="dataCount">The number of bytes to send or receive into data starting at offset.</param>
        /// <returns>The number of bytes sent or received.</returns>
        public int SendSetupPacket(byte requestType, byte request, ushort value, ushort index, byte[] data, int dataOffset, int dataCount) {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentOutOfRangeException.ThrowIfNegative(dataOffset);
            ArgumentOutOfRangeException.ThrowIfNegative(dataCount);
            if (data.Length < dataOffset + dataCount) {
                throw new ArgumentOutOfRangeException(nameof(data), "data.Length must be at least dataOffset + dataCount.");
            }
            fixed (byte* bytes = data) {
                Libendpoint.Check(Libendpoint.UsbRawControl(this.Handle(), requestType, request, value, index, bytes + dataOffset, dataCount,
                                                            this.SetupTimeout, out var transferred));
                return transferred;
            }
        }

        /// <summary>Gets the device descriptor.</summary>
        /// <returns>The device descriptor.</returns>
        public Descriptors.Device GetDeviceDescriptor() {
            var bytes = new byte[Descriptors.Device.LENGTH];
            this.SendSetupPacket(0x80, 0x06, 0x0100, 0, bytes, 0, bytes.Length);
            return new Descriptors.Device(bytes);
        }

        /// <summary>Gets the configuration descriptor.</summary>
        /// <param name="configurationIndex">The configuration index.</param>
        /// <returns>The configuration descriptor.</returns>
        public Descriptors.Configuration GetConfigurationDescriptor(byte configurationIndex) {
            var bytes = new byte[Descriptors.Configuration.LENGTH];
            this.SendSetupPacket(0x80, 0x06, (ushort)(0x0200 | configurationIndex), 0, bytes, 0, bytes.Length);
            var descriptor = new Descriptors.Configuration(bytes);
            bytes = new byte[descriptor.TotalLength];
            this.SendSetupPacket(0x80, 0x06, (ushort)(0x0200 | configurationIndex), 0, bytes, 0, bytes.Length);
            descriptor.FillChildren(bytes, Descriptors.Configuration.LENGTH);
            return descriptor;
        }

        /// <summary>Gets a string descriptor (the product's name...): its text, empty if the device has none.</summary>
        /// <param name="index">The string's index (Descriptors.Device.ProductIndex...).</param>
        public string GetString(byte index) {
            if (index == 0) {
                return "";
            }
            var bytes = new byte[255];
            var count = this.SendSetupPacket(0x80, 0x06, (ushort)(0x0300 | index), 0x0409, bytes, 0, bytes.Length);
            return count > 2 ? System.Text.Encoding.Unicode.GetString(bytes, 2, Math.Min(count, bytes[0]) - 2) : "";
        }

        /// <summary>A USB communication pipe.</summary>
        public class Pipe : IDisposable {
            private readonly RawDevice device;
            private int transferTimeout = 500;
            private bool disposed;

            internal Pipe(RawDevice device, Descriptors.Endpoint endpoint) {
                this.device = device;
                this.Endpoint = endpoint;
            }

            /// <summary>The timeout for transfer operations in milliseconds (0: none). Defaults to 500ms.</summary>
            public int TransferTimeout {
                get => this.transferTimeout;
                set {
                    ArgumentOutOfRangeException.ThrowIfNegative(value);
                    this.transferTimeout = value;
                }
            }

            /// <summary>Endpoint associated with this pipe.</summary>
            public Descriptors.Endpoint Endpoint { get; }

            /// <summary>Transfers data to/from the endpoint.</summary>
            /// <param name="buffer">The transfer buffer.</param>
            /// <returns>The number of bytes successfully transferred.</returns>
            public int Transfer(byte[] buffer) {
                ArgumentNullException.ThrowIfNull(buffer);
                return this.Transfer(buffer, 0, buffer.Length);
            }

            /// <summary>Transfers data to/from the endpoint: an IN endpoint (address 0x80 and up) fills the buffer, an OUT endpoint sends it.</summary>
            /// <param name="buffer">The transfer buffer.</param>
            /// <param name="offset">The offset into the buffer.</param>
            /// <param name="count">The amount to transfer starting at offset from the buffer.</param>
            /// <returns>The number of bytes successfully transferred.</returns>
            /// <exception cref="TimeoutException">Nothing was transferred within TransferTimeout.</exception>
            public int Transfer(byte[] buffer, int offset, int count) {
                ArgumentNullException.ThrowIfNull(buffer);
                ArgumentOutOfRangeException.ThrowIfNegative(offset);
                ArgumentOutOfRangeException.ThrowIfNegative(count);
                if (buffer.Length < offset + count) {
                    throw new ArgumentOutOfRangeException(nameof(buffer), "buffer.Length must be at least offset + count.");
                }
                ObjectDisposedException.ThrowIf(this.disposed, this);
                fixed (byte* bytes = buffer) {
                    Libendpoint.Check(Libendpoint.UsbRawTransfer(this.device.Handle(), this.Endpoint.Address, bytes + offset, count,
                                                                 this.transferTimeout, out var transferred));
                    return transferred;
                }
            }

            /// <summary>Closes the pipe.</summary>
            public void Dispose() => this.disposed = true;
        }
    }
}
