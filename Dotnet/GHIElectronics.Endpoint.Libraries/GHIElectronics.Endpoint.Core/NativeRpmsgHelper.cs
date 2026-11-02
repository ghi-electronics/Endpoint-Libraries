using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Low-level helper used by other libraries to talk to the firmware's
    /// hardware timing helper. Data is exchanged as 32-bit words in fixed-size
    /// blocks of <see cref="BlockSize"/> words. Most applications never call
    /// this directly; <see cref="GHIElectronics.Endpoint.Core.EPM815"/>-based
    /// classes such as <c>DigitalSignalController</c> use it internally.
    /// </summary>
    public static partial class NativeRpmsgHelper {

        private const string LibNativeRpmsgHelper = "nativerpmsghelper.so";
        internal static IntPtr InvalidHandleValue;


        /// <summary>Delay in milliseconds between consecutive <see cref="Send"/> blocks (default 5).</summary>
        public static int BlockDelay { get; set; } = 5;

        private const int CMD_RAW_DATA_START_OFFSET = 64;
        /// <summary>Number of 32-bit words in one message block (64).</summary>
        public static int BlockSize { get; } = CMD_RAW_DATA_START_OFFSET;

        /// <summary>Signature for the <see cref="DataReceived"/> event.</summary>
        /// <param name="data">The received payload, 32-bit words.</param>
        public delegate void DataReceivedEventHandler(uint[] data);
        static private DataReceivedEventHandler dataReceivedEventHandlerCallback;

        /// <summary>Raised from the receive loop started by <see cref="StartReceiving"/>.</summary>
        static public event DataReceivedEventHandler DataReceived {
            add => dataReceivedEventHandlerCallback += value;
            remove {
                if (dataReceivedEventHandlerCallback != null) {
                    dataReceivedEventHandlerCallback -= value;
                }
            }
        }

        static object lockObj = new object();
        static NativeRpmsgHelper() {
            InvalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(NativeRpmsgHelper).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libmytestlibName) => {
                if (assembly != currentAssembly || libmytestlibName != LibNativeRpmsgHelper) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };

        }


        /// <summary>
        /// Sends <paramref name="count"/> 32-bit words from
        /// <paramref name="data"/> starting at <paramref name="offset"/>. The
        /// transfer is split into blocks of <see cref="BlockSize"/> words with
        /// <see cref="BlockDelay"/> milliseconds between blocks.
        /// </summary>
        /// <returns>The number of words actually written.</returns>
        public static int Send(uint[] data, int offset, int count) {
            var block = count / BlockSize;
            var remain = count % BlockSize;
            var index = 0;
            var buffer = new uint[BlockSize];

            lock (lockObj) {
                while (block > 0) {
                    Array.Copy(data, offset + index, buffer, 0, BlockSize);

                    var sent = native_rpmsg_send(buffer, 0, BlockSize);

                    if (sent > 0) {
                        index += sent;

                        if (index % BlockSize == 0)
                            block--;
                    }


                    Thread.Sleep(BlockDelay);


                }

                if (remain > 0) {
                    Array.Copy(data, offset + index, buffer, 0, remain);
                    var sent = native_rpmsg_send(buffer, 0, remain);
                    if (sent > 0) {
                        index += sent;
                    }
                }
            }

            return index;
        }


        private static uint[] internalBuffer;
        static bool started = false;
        /// <summary>
        /// Starts the background receive loop. The loop reads whole messages
        /// from the helper endpoint and raises <see cref="DataReceived"/> for
        /// each. Safe to call more than once: only the first call starts a
        /// loop; subsequent calls return a completed task.
        /// </summary>
        public static Task StartReceiving() {
            if (started) return Task.CompletedTask;

            started = true;
            return Task.Run(() => {
                while (true) {
                    var data = new uint[CMD_RAW_DATA_START_OFFSET];

                    Thread.Sleep(100);

                    var ret =  native_rpmsg_read(data, 0, data.Length);

                    var size = data[0] / 4; // convert to uint

                    
                    if (size > 0) {

                        internalBuffer = new uint[size];

                        // copy header
                        Array.Copy(data, 0, internalBuffer, 0, CMD_RAW_DATA_START_OFFSET);

                        if (size > CMD_RAW_DATA_START_OFFSET) {
                            // get raw data

                            var size_raw = size - CMD_RAW_DATA_START_OFFSET;

                            var remain_block = size_raw / CMD_RAW_DATA_START_OFFSET;

                            var remain = size_raw % CMD_RAW_DATA_START_OFFSET;

                            var idx = CMD_RAW_DATA_START_OFFSET;

                            while (remain_block > 0) {
                                var read = native_rpmsg_read(internalBuffer, idx, CMD_RAW_DATA_START_OFFSET);

                                if (read > 0) {
                                    idx += read;

                                    if (idx % CMD_RAW_DATA_START_OFFSET == 0) {
                                        remain_block--;
                                    }
                                }

                                //if (ret == -1)
                                //    break;
                                

                            }

                            if (remain > 0) {

                                while (idx < size) {
                                    var read = native_rpmsg_read(internalBuffer, idx, (int)remain);
                                    if (read > 0) {
                                        idx += read;
                                    }
                                }
                            }

                            

                        }

                        dataReceivedEventHandlerCallback?.Invoke(internalBuffer);
                    }

                }

            }); ;
        }



        /// <summary>
        /// Opens the helper endpoint and reserves it for this process.
        /// Call before <see cref="Send"/> or <see cref="StartReceiving"/>.
        /// </summary>
        /// <returns>Zero on success, a negative error code otherwise.</returns>
        public static int Acquire() {
            return native_rpmsg_acquire(); ;
        }

        /// <summary>Releases the helper endpoint previously opened by <see cref="Acquire"/>.</summary>
        /// <returns>Zero on success, a negative error code otherwise.</returns>
        public static int Release() {
            return native_rpmsg_release(); ;
        }

        /// <summary>
        /// Sends a ping message to the remote core and waits for a reply, as a
        /// quick link check.
        /// </summary>
        /// <returns>Zero when the remote responds, a negative error code otherwise.</returns>
        public static int Ping() {
            return native_rpmsg_test();
        }

        [DllImport(LibNativeRpmsgHelper)]
        internal static extern int native_rpmsg_test();

        [DllImport(LibNativeRpmsgHelper)]
        internal static extern int native_rpmsg_acquire();


        [DllImport(LibNativeRpmsgHelper)]
        internal static extern int native_rpmsg_release();

        [DllImport(LibNativeRpmsgHelper)]
        internal static extern int native_rpmsg_send(uint[] data, int offset, int count);

        [DllImport(LibNativeRpmsgHelper)]
        internal static extern int native_rpmsg_read(uint[] data, int offset, int count);
    }
}
