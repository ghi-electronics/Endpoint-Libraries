using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Camera;

namespace GHIElectronics.Endpoint.Devices.UsbHost {


    public class Webcam : IDisposable {


        private const string LibNativeWebcam = "nativecamera.so";
        internal static IntPtr InvalidHandleValue;

        public int Width { get; private set; }
        public int Height { get; private set; }

        private int ImageQuality { get; set; } = 70;

        private string devicePath;

        private bool isOpened = false;

        private CameraConfiguration setting = null;
        public CameraConfiguration Setting {
            get => this.setting;
            set {
                if (this.setting == value) return;
                this.setting = value;

                this.Width = this.setting.Width;
                this.Height = this.setting.Height;

                NativeDeviceInit(this.setting.Width, this.setting.Height, this.setting.FrameRate, this.ImageQuality);

                this.buffer = new byte[GetBufferSize()];

            }

        }


        private byte[] buffer;

        public bool IsVideoStreaming { get; private set; }

        static object lockObj = new object();

        public delegate void FrameReceived(Webcam sender, byte[] data);
        private FrameReceived onFrameReceivedCallbacks;

        public Webcam(string devicePath = "/dev/video0") {

            InvalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(Webcam).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libmytestlibName) => {
                if (assembly != currentAssembly || libmytestlibName != LibNativeWebcam) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };

            this.devicePath = devicePath;

            this.Open();

        }

        public string[] GetResolution() => CameraConfiguration.GetResolution();

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

        private void Open() {
            if (this.isOpened) {
                throw new Exception($"The device {this.devicePath} is already opened.");
            }

            if (!File.Exists(this.devicePath)) {
                throw new ArgumentException("No camera found.");
            }

            var error = NativeDeviceOpen($"{this.devicePath}", 1); // 1 is //IO_MMAP
            if (0 != error)
                throw new Exception($"Could not open the device {this.devicePath}. Error: {error}");

            this.isOpened = true;
        }

        private void Close() {

            if (!this.isOpened)
                return;
            this.VideoStreamStop();

            var error = NativeDeviceUninit();
            if (0 != error)
                throw new Exception($"Could not uninit the device {this.devicePath}. Error: {error}");

            error = NativeDeviceClose();
            if (0 != error)
                throw new Exception($"Could not close the device {this.devicePath}. Error: {error}");
            this.isOpened = false;

        }
        public byte[] Capture() {
            if (!this.isOpened)
                throw new Exception($"The device {this.devicePath} is not opened yet.");

            if (this.buffer == null)
                throw new Exception($"Invalid setting found.");

            if (this.IsVideoStreaming) {
                throw new Exception($"The device {this.devicePath} is busy for streaming.");
            }




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

                return ConvertImage.Convert(this.buffer, this.Width, this.Height, Format.Jpeg, this.setting.ImageFormat);


            }



        }

        bool videostreamstart = false;
        public void VideoStreamStart() {
            if (!this.isOpened)
                throw new Exception($"The device {this.devicePath} is not opened yet.");

            if (this.buffer == null)
                throw new Exception($"Invalid setting found.");

            this.IsVideoStreaming = true;
            this.videostreamstart = true;

            Task.Run(() => {

                var error = NativeCaptureStart();
                if (0 != error)
                    throw new Exception($"Could not capture. Error: {error}");

                while (this.IsVideoStreaming) {

                    lock (lockObj) {


                        error = NativeMainLoop(this.buffer);

                        if (0 != error)
                            throw new Exception($"Could not progress image. Error: {error}");

                        //var data = this.ProgressDataImage(this.buffer);
                        var data = ConvertImage.Convert(this.buffer, this.Width, this.Height, Format.Jpeg, this.setting.ImageFormat);

                        if (data != null) {
                            this.onFrameReceivedCallbacks?.Invoke(this, data);
                        }


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
        internal static extern int NativeDeviceOpen(string dev, int io_mode);

        [DllImport(LibNativeWebcam)]
        internal static extern int NativeDeviceInit(int width, int height, int fps, int imageQuality);

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

        [DllImport(LibNativeWebcam)]
        internal static extern int GetBufferSize();


    }
}
