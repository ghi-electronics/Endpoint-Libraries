using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Camera;
using static GHIElectronics.Endpoint.Core.EPM815;

namespace GHIElectronics.Endpoint.Devices.Dcmi {

    public abstract class DcmiController : IDisposable {

        private const string LibNativeDcmi = "nativecamera.so";
        internal static IntPtr InvalidHandleValue;

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

        public int Width { get; private set; }
        public int Height { get; private set; }

        private string devicePath;

        private byte[] buffer;

        public bool IsVideoStreaming { get; private set; }

        static object lockObj = new object();

        const int CLOCK_PIN = EPM815.Gpio.Pin.PA13;

        public delegate void FrameReceived(DcmiController sender, byte[] data);
        private FrameReceived onFrameReceivedCallbacks;

        private static int initializeCount = 0;

        private bool isOpened = false;

        private int ImageQuality { get; set; } = 70;

        public DcmiController(string devicePath= "/dev/video0") {

            InvalidHandleValue = new IntPtr(-1);

            var currentAssembly = typeof(DcmiController).Assembly;

            AssemblyLoadContext.GetLoadContext(currentAssembly)!.ResolvingUnmanagedDll += (assembly, libmytestlibName) => {
                if (assembly != currentAssembly || libmytestlibName != LibNativeDcmi) {
                    return IntPtr.Zero;
                }


                return IntPtr.Zero;
            };

            this.devicePath = devicePath;
            this.Acquire();
           

            
        }

        private void Acquire() {
            if (initializeCount == 0) {
                this.LoadResources();
            }

            initializeCount++;
        }

        private void Release() {
            initializeCount--;

            if (initializeCount == 0) {
                this.UnLoadResources();
            }

        }

        private void LoadResources() {

            if (Gpio.IsPinReserved(CLOCK_PIN)) {
                throw new ArgumentException("PCLK pin is already in used.");
            }

            this.SetPclk(true);


            var pinConfig = EPM815.Dcmi.PinSettings[EPM815.Dcmi.Dcmi0];

            if (Gpio.IsPinReserved(pinConfig.DcmiHsync)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiHsync);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiVsync)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiVsync);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiPixclk)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiPixclk);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD0)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD0);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD1)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD1);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD2)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD2);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD3)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD3);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD4)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD4);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD5)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD5);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD6)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD6);
            }

            if (Gpio.IsPinReserved(pinConfig.DcmiD7)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.DcmiD7);
            }

            Gpio.SetModer(pinConfig.DcmiHsync, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiVsync, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiPixclk, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD0, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD1, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD2, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD3, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD4, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD5, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD6, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.DcmiD7, Gpio.Moder.Alternate);


            Gpio.SetAlternate(pinConfig.DcmiHsync, pinConfig.AlternatePinDcmiHsync);
            Gpio.SetAlternate(pinConfig.DcmiVsync, pinConfig.AlternatePinDcmiVsync);
            Gpio.SetAlternate(pinConfig.DcmiPixclk, pinConfig.AlternatePinDcmiPixclk);
            Gpio.SetAlternate(pinConfig.DcmiD0, pinConfig.AlternatePinDcmiD0);
            Gpio.SetAlternate(pinConfig.DcmiD1, pinConfig.AlternatePinDcmiD1);
            Gpio.SetAlternate(pinConfig.DcmiD2, pinConfig.AlternatePinDcmiD2);
            Gpio.SetAlternate(pinConfig.DcmiD3, pinConfig.AlternatePinDcmiD3);
            Gpio.SetAlternate(pinConfig.DcmiD4, pinConfig.AlternatePinDcmiD4);
            Gpio.SetAlternate(pinConfig.DcmiD5, pinConfig.AlternatePinDcmiD5);
            Gpio.SetAlternate(pinConfig.DcmiD6, pinConfig.AlternatePinDcmiD6);
            Gpio.SetAlternate(pinConfig.DcmiD7, pinConfig.AlternatePinDcmiD7);


            var script_insmod = new Script("modprobe", "/.", $"stm32-dcmi");
            script_insmod.Start();
           
            Gpio.PinReserve(pinConfig.DcmiHsync);
            Gpio.PinReserve(pinConfig.DcmiVsync);
            Gpio.PinReserve(pinConfig.DcmiPixclk);
            Gpio.PinReserve(pinConfig.DcmiD0);
            Gpio.PinReserve(pinConfig.DcmiD1);
            Gpio.PinReserve(pinConfig.DcmiD2);
            Gpio.PinReserve(pinConfig.DcmiD3);
            Gpio.PinReserve(pinConfig.DcmiD4);
            Gpio.PinReserve(pinConfig.DcmiD5);
            Gpio.PinReserve(pinConfig.DcmiD6);
            Gpio.PinReserve(pinConfig.DcmiD7);
        }

        private void UnLoadResources() {
            var script_insmod = new Script("rmmod", "/.", $"stm32-dcmi");
            script_insmod.Start();

            var pinConfig = EPM815.Dcmi.PinSettings[EPM815.Dcmi.Dcmi0];

            Gpio.PinRelease(pinConfig.DcmiHsync);
            Gpio.PinRelease(pinConfig.DcmiVsync);
            Gpio.PinRelease(pinConfig.DcmiPixclk);
            Gpio.PinRelease(pinConfig.DcmiD0);
            Gpio.PinRelease(pinConfig.DcmiD1);
            Gpio.PinRelease(pinConfig.DcmiD2);
            Gpio.PinRelease(pinConfig.DcmiD3);
            Gpio.PinRelease(pinConfig.DcmiD4);
            Gpio.PinRelease(pinConfig.DcmiD5);
            Gpio.PinRelease(pinConfig.DcmiD6);
            Gpio.PinRelease(pinConfig.DcmiD7);

            Gpio.SetModer(pinConfig.DcmiHsync, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiVsync, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiPixclk, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD0, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD1, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD2, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD3, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD4, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD5, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD6, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.DcmiD7, Gpio.Moder.Input);

            this.SetPclk(false);
            Gpio.PinRelease(CLOCK_PIN);
        }

        private void SetPclk(bool enable) {
            const uint RCC_BASE = 0x50000000U;
            const uint RCC_MCO1CFGR = RCC_BASE + 0x800;
            if (enable) {
                Gpio.SetOutputType(CLOCK_PIN, Gpio.OutputType.PushPull);
                Gpio.SetModer(CLOCK_PIN, Gpio.Moder.Alternate);
                Gpio.SetAlternate(CLOCK_PIN, Gpio.Alternate.AF2);


                var value = (uint)((0 << 12) | (3 << 4) | 0); // Set MCO1 = 21MHz

                Register.Write(RCC_MCO1CFGR, value);

                Register.SetBits(RCC_MCO1CFGR, 1 << 12); // Enable MCO1
            }
            else {


                Register.Write(RCC_MCO1CFGR, 0);

                Gpio.SetModer(CLOCK_PIN, Gpio.Moder.Input);
            }
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

            this.Release();
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

        public string[] GetResolution() => CameraConfiguration.GetResolution();

        protected void Open() {

            if (this.isOpened) {
                throw new Exception($"The device {this.devicePath} is already opened.");
            }

            if (!File.Exists(this.devicePath)) {
                throw new ArgumentException("No camera found.");
            }

            var error = NativeDeviceOpen($"{this.devicePath}", 0); // 0 is IO_METHOD_READ for fast
            if (0 != error)
                throw new Exception($"Could not open the device {this.devicePath}. Error: {error}");

            //error = NativeDeviceInit();

            //if (0 != error) {
            //    throw new Exception($"Could not initialize the device {this.devicePath}.Error: {error}");
            //}

            //this.buffer = new byte[GetBufferSize()];

            //this.IsOpened = true;

            // skip first 5 frame because of dask
            //for (var i = 0; i < 4; i++) {
            //    this.Capture();
            //}

            this.isOpened = true;
        }

        protected void Close() {

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

                return this.ProgressDataImage(this.buffer);
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

                        var data = this.ProgressDataImage(this.buffer);

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

        private byte[] ProgressDataImage(byte[] inBuffer) {


            if (inBuffer[0] == 0xFF && inBuffer[1] == 0xD8) {
                var jpeg_size = inBuffer.Length - 1;


                for (; jpeg_size > 2; jpeg_size--) {
                    if (inBuffer[jpeg_size] == 0xD9 && inBuffer[jpeg_size - 1] == 0xFF)
                        break;
                }

                if (jpeg_size > 2) {
                    jpeg_size++;

                    var dataJpeg = new byte[jpeg_size];

                    Array.Copy(inBuffer, 0, dataJpeg, 0, dataJpeg.Length);

                    switch (this.Setting.ImageFormat) {
                        case Format.Jpeg:
                            return dataJpeg;

                        case Format.Rgb888:
                        case Format.Rgb565:

                            var data888 = new byte[this.Setting.Width * this.Setting.Height * 3];

                            NativeUtils.JpegtoRGB888(dataJpeg, jpeg_size, data888);
                            if (this.Setting.ImageFormat == Format.Rgb888) {
                                return data888;
                            }

                            var data = new byte[this.Setting.Width * this.Setting.Height * 2];
                            var index = 0;
                            // to 565
                            for (var i = 0; i < data888.Length; i += 3) {
                                var color = (uint)(data888[i + 2] | (data888[i + 1] << 8) | (data888[i + 0] << 16));

                                data[0 + index + 0] = (byte)(((color & 0x00001c00) >> 5) | ((color & 0x000000f8) >> 3));
                                data[0 + index + 1] = (byte)(((color & 0x00f80000) >> 16) | ((color & 0x0000e000) >> 13));

                                index += 2;
                            }

                            return data;
                    }

                }

            }
            return null;


        }

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeDeviceOpen(string dev, int mode);

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeDeviceInit(int width, int height, int fps, int imageQuality);

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeCaptureStart();

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeMainLoop(byte[] data);

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeCaptureStop();

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeDeviceUninit();

        [DllImport(LibNativeDcmi)]
        internal static extern int NativeDeviceClose();

        [DllImport(LibNativeDcmi)]
        internal static extern int GetBufferSize();


    }
}
