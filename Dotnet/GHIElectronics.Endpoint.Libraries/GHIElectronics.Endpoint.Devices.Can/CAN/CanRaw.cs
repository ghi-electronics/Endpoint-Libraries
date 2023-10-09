// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace GHIElectronic.Endpoint.Devices.Can {
    /// <summary>
    /// Allows reading and writing raw frames to CAN Bus
    /// </summary>
    internal class CanRaw : IDisposable {
        private SafeCanRawSocketHandle _handle;

        /// <summary>
        /// Constructs CanRaw instance
        /// </summary>
        /// <param name="networkInterface">Name of the network interface</param>
        public CanRaw(string networkInterface = "can0") => this._handle = new SafeCanRawSocketHandle(networkInterface);

        /// <summary>
        /// Writes frame to the CAN Bus
        /// </summary>
        /// <param name="data">Data to write (at most 8 bytes)</param>
        /// <param name="id">Recipient identifier</param>
        /// <remarks><paramref name="id"/> can be ignored by recipient - anyone connected to the bus can read or write any frames</remarks>
        public void WriteFrameFd(ReadOnlySpan<byte> data, CanId id, bool bitrateswitch = false) {
            if (!id.IsValid) {
                throw new ArgumentException("Id is not valid. Ensure Error flag is not set and that id is in the valid range (11-bit for standard frame and 29-bit for extended frame).", nameof(id));
            }

            if (data.Length > CanFrameFd.MaxLength) {
                throw new ArgumentException($"Data length cannot exceed {CanFrameFd.MaxLength} bytes.", nameof(data));
            }

            var frame = new CanFrameFd {
                Id = id,
                Length = (byte)data.Length,
                BitRateSwitch = (bitrateswitch == true) ? (byte)1 : (byte)0,
            };

            Debug.Assert(frame.IsValid, "Frame is not valid");

            unsafe {
                var frameData = new Span<byte>(frame.Data, data.Length);
                data.CopyTo(frameData);

                var buff = (byte*)&frame;
                Interop.Write(this._handle, buff, Marshal.SizeOf<CanFrameFd>());
            }
        }

        /// <summary>
        /// Writes frame to the CAN Bus
        /// </summary>
        /// <param name="data">Data to write (at most 8 bytes)</param>
        /// <param name="id">Recipient identifier</param>
        /// <remarks><paramref name="id"/> can be ignored by recipient - anyone connected to the bus can read or write any frames</remarks>
        public void WriteFrame(ReadOnlySpan<byte> data, CanId id) {
            if (!id.IsValid) {
                throw new ArgumentException("Id is not valid. Ensure Error flag is not set and that id is in the valid range (11-bit for standard frame and 29-bit for extended frame).", nameof(id));
            }

            if (data.Length > CanFrame.MaxLength) {
                throw new ArgumentException($"Data length cannot exceed {CanFrameFd.MaxLength} bytes.", nameof(data));
            }

            var frame = new CanFrame {
                Id = id,
                Length = (byte)data.Length,                
            };

            Debug.Assert(frame.IsValid, "Frame is not valid");

            unsafe {
                var frameData = new Span<byte>(frame.Data, data.Length);
                data.CopyTo(frameData);

                var buff = (byte*)&frame;
                Interop.Write(this._handle, buff, Marshal.SizeOf<CanFrame>());
            }
        }

        /// <summary>
        /// Reads frame from the bus
        /// </summary>
        /// <param name="data">Data where output data should be written to</param>
        /// <param name="frameLength">Length of the data read</param>
        /// <param name="id">Recipient identifier</param>
        /// <returns></returns>
        public bool TryReadFrameFd(Span<byte> data, out int frameLength, out CanId id, out bool bitrateSwitch) {
            if (data.Length < CanFrameFd.MaxLength) {
                throw new ArgumentException($"Value must be a minimum of {CanFrameFd.MaxLength} bytes.", nameof(data));
            }

            var frame = new CanFrameFd();
            var remainingBytes = Marshal.SizeOf<CanFrameFd>();

            unsafe {
                var read = Interop.Read(this._handle, (byte*)&frame, remainingBytes);
            }

            id = frame.Id;
            frameLength = frame.Length;
            bitrateSwitch = frame.BitRateSwitch > 0 ? true : false;

            if (!frame.IsValid) {
                // invalid frame
                // we will leave id filled in case it is useful for anyone
                frameLength = 0;
                return false;
            }

            // This is guaranteed by minimum buffer length and the fact that frame is valid
            Debug.Assert(frame.Length <= data.Length, "Invalid frame length");

            unsafe {
                // We should not use input buffer directly for reading:
                // - we do not know how many bytes will be read up front without reading length first
                // - we should not write anything more than pointed by frameLength
                // - we still need to read the remaining bytes to read the full frame
                // Considering there are at most 8 bytes to read it is cheaper
                // to copy rather than doing multiple syscalls.
                var frameData = new Span<byte>(frame.Data, frame.Length);
                frameData.CopyTo(data);
            }

            return true;
        }

        /// <summary>
        /// Reads frame from the bus
        /// </summary>
        /// <param name="data">Data where output data should be written to</param>
        /// <param name="frameLength">Length of the data read</param>
        /// <param name="id">Recipient identifier</param>
        /// <returns></returns>
        public bool TryReadFrame(Span<byte> data, out int frameLength, out CanId id) {
            if (data.Length < CanFrameFd.MaxLength) {
                throw new ArgumentException($"Value must be a minimum of {CanFrameFd.MaxLength} bytes.", nameof(data));
            }

            var frame = new CanFrame();
            var remainingBytes = Marshal.SizeOf<CanFrame>();

            unsafe {
                var read = Interop.Read(this._handle, (byte*)&frame, remainingBytes);
            }

            id = frame.Id;
            frameLength = frame.Length;            

            if (!frame.IsValid) {
                // invalid frame
                // we will leave id filled in case it is useful for anyone
                frameLength = 0;
                return false;
            }

            // This is guaranteed by minimum buffer length and the fact that frame is valid
            Debug.Assert(frame.Length <= data.Length, "Invalid frame length");

            unsafe {
                // We should not use input buffer directly for reading:
                // - we do not know how many bytes will be read up front without reading length first
                // - we should not write anything more than pointed by frameLength
                // - we still need to read the remaining bytes to read the full frame
                // Considering there are at most 8 bytes to read it is cheaper
                // to copy rather than doing multiple syscalls.
                var frameData = new Span<byte>(frame.Data, frame.Length);
                frameData.CopyTo(data);
            }

            return true;
        }

        /// <summary>
        /// Set filter on the bus to read only from specified recipient.
        /// </summary>
        /// <param name="id">Recipient identifier</param>
        //public void Filter(CanId id)
        //{
        //    if (!id.IsValid)
        //    {
        //        throw new ArgumentException("Value must be a valid CanId", nameof(id));
        //    }

        //    Span<Interop.CanFilter> filters = stackalloc Interop.CanFilter[1];
        //    filters[0].can_id = id.Raw;
        //    filters[0].can_mask = id.Value | (uint)CanFlags.ExtendedFrameFormat | (uint)CanFlags.RemoteTransmissionRequest;

        //    Interop.SetCanRawSocketOption<Interop.CanFilter>(this._handle, Interop.CanSocketOption.CAN_RAW_FILTER, filters);
        //}

        /// <summary>
        /// Set filter on the bus to read only from specified recipient.
        /// </summary>
        /// <param name="id">Recipient identifier</param>
        /// <param name="mask">mask</param>
        public void Filter(uint[] id, uint[] mask, bool invert) {

            Span<Interop.CanFilter> filters = stackalloc Interop.CanFilter[id.Length];
            for (int i = 0; i < id.Length; i++) {
                filters[i].can_id = id[i];

                if (invert) {
                    filters[i].can_id |= 0x20000000U;
                }
                filters[i].can_mask = mask[i];
            }

            Interop.SetCanRawSocketOption<Interop.CanFilter>(this._handle, Interop.CanSocketOption.CAN_RAW_FILTER, filters);

            if (invert) {
                Span<uint> filter = stackalloc uint[1];

                filter[0] = 1;
                Interop.SetCanRawSocketOption<uint>(this._handle, Interop.CanSocketOption.CAN_RAW_JOIN_FILTERS, filter);

            }
        }

        /// <summary>
        /// Set error on the bus
        /// </summary>
        public void FilterError(uint mask) {
            Span<uint> filters = stackalloc uint[1];

            filters[0] = mask;

            Interop.SetCanRawSocketOption<uint>(this._handle, Interop.CanSocketOption.CAN_RAW_ERR_FILTER, filters);
        }


        /// <summary>
        /// Enable CANFD
        /// </summary>
        public void EnableCanFd() {
            Span<uint> filters = stackalloc uint[1];

            filters[0] = 1;

            Interop.SetCanRawSocketOption<uint>(this._handle, Interop.CanSocketOption.CAN_RAW_FD_FRAMES, filters);
        }


        private static bool IsEff(uint address) =>
            // has explicit flag or address does not fit in SFF addressing mode
            (address & (uint)CanFlags.ExtendedFrameFormat) != 0
                || (address & Interop.CAN_EFF_MASK) != (address & Interop.CAN_SFF_MASK);

        /// <inheritdoc/>
        public void Dispose() => this._handle.Dispose();
    }
}
