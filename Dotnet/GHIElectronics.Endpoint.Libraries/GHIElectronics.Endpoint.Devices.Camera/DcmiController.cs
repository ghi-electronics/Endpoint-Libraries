using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;
using static GHIElectronics.Endpoint.Core.EPM815;

namespace GHIElectronics.Endpoint.Devices.Camera {

    /// <summary>
    /// Base controller for the board's parallel camera-interface port. Reserves the
    /// camera data and sync pins, drives the pixel clock, loads the backing kernel
    /// module, and exposes single-frame capture and streaming. Concrete sensor
    /// drivers derive from this type.
    /// </summary>
    public abstract class DcmiController : IDisposable {

        private const string LibNativeDcmi = "nativecamera.so";
        internal static IntPtr InvalidHandleValue;

        private CameraConfiguration setting = null;
        /// <summary>
        /// Resolution, pixel format and frame rate for subsequent captures.
        /// Assigning a value re-initializes the native device at the new size, FPS and JPEG quality,
        /// and reallocates the internal frame buffer.
        /// </summary>
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

        /// <summary>Frame width in pixels, taken from the last applied <see cref="Setting"/>.</summary>
        public int Width { get; private set; }
        /// <summary>Frame height in pixels, taken from the last applied <see cref="Setting"/>.</summary>
        public int Height { get; private set; }

        private string devicePath;

        private byte[] buffer;

        /// <summary><c>true</c> while <see cref="VideoStreamStart"/> is delivering frames via <see cref="FrameReceived"/>.</summary>
        public bool IsVideoStreaming { get; private set; }

        static object lockObj = new object();

        const int CLOCK_PIN = EPM815.Gpio.Pin.PA13;

        /// <summary>Handler for a single captured frame from the sensor.</summary>
        /// <param name="sender">The controller that produced the frame.</param>
        /// <param name="data">Frame bytes in the format of <see cref="CameraConfiguration.ImageFormat"/>. Raised from the streaming worker thread.</param>
        public delegate void FrameReceivedEventHandler(DcmiController sender, byte[] data);
        private FrameReceivedEventHandler onFrameReceivedCallbacks;

        private static int initializeCount = 0;

        private bool isOpened = false;

        private int ImageQuality { get; set; } = 70;

        /// <summary>
        /// Reserves the camera-interface pins, starts the pixel clock, and loads the backing
        /// capture driver. The V4L2 node is not opened until <see cref="Open"/> is called.
        /// </summary>
        /// <param name="devicePath">V4L2 device node for the capture, defaulting to <c>/dev/video0</c>.</param>
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


                var value = (uint)((0 << 12) | (0 << 4) | 1); // Set MCO1 = 8MHz

                Register.Write(RCC_MCO1CFGR, value);

                Register.SetBits(RCC_MCO1CFGR, 1 << 12); // Enable MCO1
            }
            else {


                Register.Write(RCC_MCO1CFGR, 0);

                Gpio.SetModer(CLOCK_PIN, Gpio.Moder.Input);
            }
        }

        private bool disposed;
        /// <summary>Closes the device, releases the camera-interface pins, stops the pixel clock and unloads the backing capture driver.</summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>Releases held resources. See <see cref="Dispose()"/>.</summary>
        /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>; <c>false</c> from a finalizer.</param>
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                this.Close();
            }

            this.Release();
            this.disposed = true;
        }


        /// <summary>
        /// Fires on the streaming worker thread for every frame captured by <see cref="VideoStreamStart"/>.
        /// Not raised by <see cref="Capture"/>.
        /// </summary>
        public event FrameReceivedEventHandler FrameReceived {
            add => this.onFrameReceivedCallbacks += value;
            remove {
                if (this.onFrameReceivedCallbacks != null) {
                    this.onFrameReceivedCallbacks -= value;
                }
            }
        }

        /// <summary>Lists every <c>WIDTHxHEIGHT</c> resolution the capture driver reports from <c>v4l2-ctl --list-formats-ext</c>, or <c>null</c> if none are found.</summary>
        public string[] GetResolution() => CameraConfiguration.GetResolution();

        /// <summary>Opens the V4L2 device for capture. Throws if the device is already open or the node does not exist.</summary>
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

        /// <summary>Stops streaming (if active) and closes the V4L2 device. Safe to call when already closed.</summary>
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


        /// <summary>
        /// Captures a single frame synchronously and returns it in the format set by <see cref="CameraConfiguration.ImageFormat"/>.
        /// Throws if <see cref="Setting"/> is unset, the device is not open, or a video stream is already running.
        /// </summary>
        /// <returns>Frame bytes, or <c>null</c> if the sensor returned no valid JPEG.</returns>
        public byte[] Capture() {
            if (this.setting == null || this.Width == 0 || this.Height == 0) {
                throw new Exception($"Invalid configuration.");
            }

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

                return ImageConverter.Convert(this.buffer, this.Width, this.Height, PixelFormat.Jpeg, this.setting.ImageFormat); ;
            }

        }

        bool videostreamstart = false;
        /// <summary>
        /// Begins continuous capture on a background task; each frame is delivered through <see cref="FrameReceived"/>.
        /// Throws if <see cref="Setting"/> is unset or the device is not open. Call <see cref="VideoStreamStop"/> to end streaming.
        /// </summary>
        public void VideoStreamStart() {
            if (this.setting == null || this.Width == 0 || this.Height == 0) {
                throw new Exception($"Invalid configuration.");
            }


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

                        var data = ImageConverter.Convert(this.buffer, this.Width, this.Height, PixelFormat.Jpeg, this.setting.ImageFormat); ;

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

        /// <summary>Signals the streaming worker to stop and blocks until it has exited.</summary>
        public void VideoStreamStop() {
            this.IsVideoStreaming = false;

            while (this.videostreamstart) Thread.Sleep(10); // make sure the streaming thread stop

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
