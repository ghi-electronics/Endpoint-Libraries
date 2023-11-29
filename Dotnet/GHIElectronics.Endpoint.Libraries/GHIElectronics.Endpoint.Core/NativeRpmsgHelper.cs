using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static partial class NativeRpmsgHelper {

        private const string LibNativeRpmsgHelper = "nativerpmsghelper.so";
        internal static IntPtr InvalidHandleValue;


        public static int BlockDelay = 10;

        private const int CMD_RAW_DATA_START_OFFSET = 64;
        public static int BlockSize { get; } = CMD_RAW_DATA_START_OFFSET;

        public delegate void DataReceivedEventHandler(uint[] data);
        static private DataReceivedEventHandler dataReceivedEventHandlerCallback;

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

                    Thread.Sleep(BlockDelay);


                }
            }

            return index;
        }


        private static uint[] internalBuffer;
        public static Task TaskReceive() {

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



        public static int Acquire() {
            return native_rpmsg_acquire(); ;
        }

        public static int Release() {
            return native_rpmsg_release(); ;
        }

        public static int Test() {
            return native_rpmsg_test(); ;
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
