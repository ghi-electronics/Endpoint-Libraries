using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    public class Webcam : IDisposable {

        private const string LibNativeWebcam = "nativewebcam.so";
        internal static IntPtr InvalidHandleValue;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public int ImageQuality { get; private set; }

        public string DeviceName { get; private set; }

        public bool IsOpened { get; private set; }

        private byte[] buffer;

        public bool IsVideoStreaming { get; private set; }

        static object lockObj = new object();

        public delegate void FrameReceived(Webcam sender, byte[] data);
        private FrameReceived onFrameReceivedCallbacks;



        public Webcam(string deviceName, int width, int height, int imageQuality = 70) {
            InvalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(Webcam).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libmytestlibName) => {
                if (assembly != currentAssembly || libmytestlibName != LibNativeWebcam) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };
            
            var dev_videos = Directory.GetFiles("/dev/", "video*");            

            if (dev_videos == null || dev_videos.Length == 0) {
                                   
                // wait for driver is loaded completely
                var timeout = 0;
                while (dev_videos == null || dev_videos.Length == 0) {
                    Thread.Sleep(100);

                    dev_videos = Directory.GetFiles("/dev/", "video*");

                    timeout++;

                    if (timeout == 20) {
                        throw new ArgumentException("No camera found.");
                    }
                }

            }
            
            

            this.DeviceName = deviceName;
            this.Width = width;
            this.Height = height;
            this.ImageQuality = imageQuality;

            this.buffer = new byte[width * height * 2]; // 888;


            this.Open();

        }

        private bool disposed;
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                this.Close();
            }

            this.disposed = true;
        }


        public event FrameReceived FrameReceivedEvent {
            add => this.onFrameReceivedCallbacks += value;
            remove {
                if (this.onFrameReceivedCallbacks != null) {
                    this.onFrameReceivedCallbacks -= value;
                }
            }
        }
        public int Dotest() {
            return NativeDoTest1(); ;
        }

        private void Open() {
            if (this.IsOpened)
                throw new Exception($"Device {this.DeviceName} is already opened.");

            var error = NativeDeviceOpen($"{this.DeviceName}", this.Width, this.Height, this.ImageQuality);
            if (0 != error)
                throw new Exception($"Could not open the device {this.DeviceName}. Error: {error}");

            error = NativeDeviceInit();

            if (0 != error) {
                throw new Exception($"Could not initialize the device {this.DeviceName}.Error: {error}");
            }

            this.IsOpened = true;
        }

        private void Close() {
            if (!this.IsOpened)
                return;

            this.VideoStreamStop();

            var error = NativeDeviceUninit();
            if (0 != error)
                throw new Exception($"Could not uninit the device {this.DeviceName}. Error: {error}");

            error = NativeDeviceClose();
            if (0 != error)
                throw new Exception($"Could not close the device {this.DeviceName}. Error: {error}");

            this.IsOpened = false;
        }

        //public void CaptureStart() {
        //    NativeCaptureStart(); ;
        //}
        public byte[] Capture() {
            if (!this.IsOpened)
                throw new Exception($"The device {this.DeviceName} need to be opened.");

            if (this.IsVideoStreaming) {
                throw new Exception($"The device {this.DeviceName} is busy for streaming.");
            }


            var data = new byte[this.buffer.Length];

            lock (lockObj) {
                var error = NativeCaptureStart();
                if (0 != error)
                    throw new Exception($"Could not capture. Error: {error}");

                error = NativeMainLoop(this.buffer);

                if (0 != error)
                    throw new Exception($"Could not progress image. Error: {error}");

                error = NativeCaptureStop();
                if (0 != error)
                    throw new Exception($"Could not stop. Error: {error}");

                Array.Copy(this.buffer, 0, data, 0, this.buffer.Length);
            }

            return data;

        }



        //public void CaptureStop() {
        //    NativeCaptureStop(); ;
        //}


        bool videostreamstart = false;
        public void VideoStreamStart() {
            this.IsVideoStreaming = true;
            this.videostreamstart = true;

            Task.Run(() => {
                var data = new byte[this.buffer.Length];

                var error = NativeCaptureStart();
                if (0 != error)
                    throw new Exception($"Could not capture. Error: {error}");

                while (this.IsVideoStreaming) {

                    lock (lockObj) {


                        error = NativeMainLoop(this.buffer);

                        if (0 != error)
                            throw new Exception($"Could not progress image. Error: {error}");

                        Array.Copy(this.buffer, 0, data, 0, this.buffer.Length);

                        this.onFrameReceivedCallbacks?.Invoke(this, data);


                    }

                    Thread.Sleep(10);

                }

                error = NativeCaptureStop();
                if (0 != error)
                    throw new Exception($"Could not stop. Error: {error}");
            });

            this.videostreamstart = false; ;

        }

        public void VideoStreamStop() {
            this.IsVideoStreaming = false;

            while (this.videostreamstart) Thread.Sleep(10); // make sure the streaming thread stop

        }

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDoTest1();

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDeviceOpen(string dev, int width, int height, int imageQuality);

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDeviceInit();

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeCaptureStart();

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeMainLoop(byte[] data);

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeCaptureStop();

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDeviceUninit();

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDeviceClose();


    }
}
