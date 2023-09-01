#pragma warning disable CS8602
#pragma warning disable CS8604
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Devices.Can {
    public class CanMessage {
        private byte[] data;
        private bool remoteTransmissionRequest;
        private bool fdCan;
        private int length;

        public uint ArbitrationId { get; set; }
        public bool ExtendedId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool BitRateSwitch { get; set; }
        //public ErrorStateIndicator ErrorStateIndicator { get; }

        public bool RemoteTransmissionRequest {
            get => this.remoteTransmissionRequest;
            set {
                if (this.FdCan && value) throw new ArgumentException("No remote request in flexible data mode.");

                this.remoteTransmissionRequest = value;
            }
        }
        public int Length {
            get => this.length;
            set {

                if (value > 8 && !this.FdCan)
                    this.length = 8;
                if (value > 8) {
                    if (value != 12 && value != 16 && value != 20 && value != 24 && value != 32 && value != 48 && value != 64) {
                        throw new ArgumentException("Length is invalid.");
                    }
                }

                this.length = value;
            }
        }

        public bool FdCan {
            get => this.fdCan;
            set {
                if (this.RemoteTransmissionRequest && value) throw new ArgumentException("No remote request in flexible data mode.");

                this.fdCan = value;
            }
        }

        public byte[] Data {
            get => this.data;

            set {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length > 64) throw new ArgumentException("value must be between 0 and 64 bytes in length.", nameof(value));

                this.data = value;
            }
        }

        public CanMessage()
            : this(0, new byte[8], 0, 0, false, false) {
        }

        public CanMessage(uint arbitrationId)
            : this(arbitrationId, null!, 0, 0) {
        }

        public CanMessage(uint arbitrationId, byte[] data)
            : this(arbitrationId, data, 0, data != null ? data.Length : 0) {
        }

        public CanMessage(uint arbitrationId, byte[] data, int offset, int count)
            : this(arbitrationId, data, offset, count, false, false) {
        }

        public CanMessage(uint arbitrationId, byte[] data, int offset, int count, bool isRemoteTransmissionRequesti, bool isExtendedId)
           : this(arbitrationId, data, offset, count, isRemoteTransmissionRequesti, isExtendedId, false, false) {
        }

        public CanMessage(uint arbitrationId, byte[] data, int offset, int count, bool isRemoteTransmissionRequesti, bool isExtendedId, bool isFdCan)
           : this(arbitrationId, data, offset, count, isRemoteTransmissionRequesti, isExtendedId, isFdCan, false) {
        }

        public CanMessage(uint arbitrationId, byte[] data, int offset, int count, bool isRemoteTransmissionRequesti, bool isExtendedId, bool isFdCan, bool isBitRateSwitch) {
            if (count < 0 || count > 64) throw new ArgumentOutOfRangeException(nameof(count), "count must be between 0 and 64.");

            if (data == null && count != 0) throw new ArgumentOutOfRangeException(nameof(count), "count must be zero when data is null.");
            if (count != 0 && offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(data), "data.Length must be at least offset + count.");
            if (isExtendedId && arbitrationId > 0x1FFFFFFF) throw new ArgumentOutOfRangeException(nameof(arbitrationId), "arbitrationId must not exceed 29 bits when using an Extended ID.");
            if (!isExtendedId && arbitrationId > 0x7FF) throw new ArgumentOutOfRangeException(nameof(arbitrationId), "arbitrationId must not exceed 11 bits when not using an Extended ID.");

            this.ArbitrationId = arbitrationId;
            this.RemoteTransmissionRequest = isRemoteTransmissionRequesti;
            this.ExtendedId = isExtendedId;
            this.Timestamp = DateTime.Now;
            this.Length = count;
            this.data = new byte[64];
            this.FdCan = isFdCan;
            this.BitRateSwitch = isBitRateSwitch;

            if (count != 0)
                Array.Copy(data, offset, this.data, 0, count);
        }




    }
}
