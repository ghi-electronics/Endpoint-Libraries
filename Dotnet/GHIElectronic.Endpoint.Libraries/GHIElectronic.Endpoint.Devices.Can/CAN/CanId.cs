// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace GHIElectronic.Endpoint.Devices.Can {
    /// <summary>
    /// Represents CAN identifier (11 or 29-bit)
    /// </summary>
    internal struct CanId
    {
        // Raw (can_id) includes EFF, RTR and ERR flags
        internal uint Raw { get; set; }

        /// <summary>
        /// Gets value of identifier (11 or 29-bit)
        /// </summary>
        public uint Value => this.ExtendedFrameFormat ? this.Extended : this.Standard;

        /// <summary>
        /// Gets or sets standard (11-bit) identifier
        /// </summary>
        public uint Standard
        {
            get
            {
                if (this.ExtendedFrameFormat)
                {
                    throw new InvalidOperationException($"{nameof(this.Standard)} can be obtained only when {nameof(this.ExtendedFrameFormat)} is not set.");
                }

                return this.Raw & Interop.CAN_SFF_MASK;
            }
            set
            {
                if ((value & ~Interop.CAN_SFF_MASK) != 0)
                {
                    throw new InvalidOperationException($"{nameof(value)} must be 11 bit identifier.");
                }

                this.ExtendedFrameFormat = false;
                // note: we clear all bits, not just SFF
                this.Raw &= ~Interop.CAN_EFF_MASK;
                this.Raw |= value;
            }
        }

        /// <summary>
        /// Gets or sets extended (29-bit) identifier
        /// </summary>
        public uint Extended
        {
            get
            {
                if (!this.ExtendedFrameFormat)
                {
                    throw new InvalidOperationException($"{nameof(this.Extended)} can be obtained only when {nameof(this.ExtendedFrameFormat)} is set.");
                }

                return this.Raw & Interop.CAN_EFF_MASK;
            }
            set
            {
                if ((value & ~Interop.CAN_EFF_MASK) != 0)
                {
                    throw new InvalidOperationException($"{nameof(value)} must be 29 bit identifier.");
                }

                this.ExtendedFrameFormat = true;
                this.Raw &= ~Interop.CAN_EFF_MASK;
                this.Raw |= value;
            }
        }

        /// <summary>
        /// Gets error (ERR) flag
        /// </summary>
        public bool Error
        {
            get => ((CanFlags)this.Raw).HasFlag(CanFlags.Error);
            set => this.SetCanFlag(CanFlags.Error, value);
        }

        /// <summary>
        /// True if extended frame format (EFF) flag is set
        /// </summary>
        public bool ExtendedFrameFormat
        {
            get => ((CanFlags)this.Raw).HasFlag(CanFlags.ExtendedFrameFormat);
            set => this.SetCanFlag(CanFlags.ExtendedFrameFormat, value);
        }

        /// <summary>
        /// Gets remote transimission request (RTR) flag
        /// </summary>
        public bool RemoteTransmissionRequest
        {
            get => ((CanFlags)this.Raw).HasFlag(CanFlags.RemoteTransmissionRequest);
            set => this.SetCanFlag(CanFlags.RemoteTransmissionRequest, value);
        }

        /// <summary>
        /// Checks if identifier is valid: error flag is not set and if address can fit selected format (11/29 bit)
        /// </summary>
        public bool IsValid
        {
            get
            {
                var idMask = this.ExtendedFrameFormat ? Interop.CAN_EFF_MASK : Interop.CAN_SFF_MASK;
                return !this.Error && (this.Raw & idMask) == (this.Raw & Interop.CAN_EFF_MASK);
            }
        }

        private void SetCanFlag(CanFlags flag, bool value)
        {
            if (value)
            {
                this.Raw |= (uint)flag;
            }
            else
            {
                this.Raw &= ~(uint)flag;
            }
        }
    }
}
