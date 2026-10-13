using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Media {
    /// <summary>What a camera sends (pictures always come out as JPEG).</summary>
    public enum CameraPixelFormat {
        /// <summary>Motion JPEG: small and fast, the way most webcams send large pictures.</summary>
        Mjpeg = 0,

        /// <summary>Raw YUYV pictures: every webcam has them; the library makes the JPEG.</summary>
        Yuyv = 1,
    }

    /// <summary>A picture size a camera has, what it sends for it, and its highest frame rate.</summary>
    public sealed class CameraFormat {
        internal CameraFormat(int width, int height, CameraPixelFormat pixelFormat, int maxFrameRate) {
            this.Width = width;
            this.Height = height;
            this.PixelFormat = pixelFormat;
            this.MaxFrameRate = maxFrameRate;
        }

        public int Width { get; }
        public int Height { get; }
        public CameraPixelFormat PixelFormat { get; }

        /// <summary>Frames per second</summary>
        public int MaxFrameRate { get; }

        public override string ToString() => $"{this.Width}x{this.Height} {this.PixelFormat} {this.MaxFrameRate} fps";
    }

    /// <summary>A camera's picture controls (each camera has some of them).</summary>
    public enum CameraControl {
        Brightness,
        Contrast,
        Saturation,
        Hue,
        Gamma,
        Gain,
        Sharpness,
        BacklightCompensation,

        /// <summary>1: on, 0: off (then WhiteBalanceTemperature counts)</summary>
        AutoWhiteBalance,
        WhiteBalanceTemperature,

        /// <summary>1: on, 0: off (then Exposure counts)</summary>
        AutoExposure,
        Exposure,

        /// <summary>0: off, 1: 50 Hz, 2: 60 Hz (against flicker from lights)</summary>
        PowerLineFrequency,

        /// <summary>1: on, 0: off (then Focus counts)</summary>
        AutoFocus,
        Focus,
        Zoom,
        Pan,
        Tilt,
    }

    /// <summary>A camera control's range and value, in the camera's own units.</summary>
    public sealed class CameraControlValue {
        internal CameraControlValue(CameraControl control, int minimum, int maximum, int step, int defaultValue, int value) {
            this.Control = control;
            this.Minimum = minimum;
            this.Maximum = maximum;
            this.Step = step;
            this.DefaultValue = defaultValue;
            this.Value = value;
        }

        public CameraControl Control { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public int Step { get; }
        public int DefaultValue { get; }
        public int Value { get; }

        public override string ToString() => $"{this.Control} {this.Value} ({this.Minimum} to {this.Maximum}, default {this.DefaultValue})";
    }

    /// <summary>A camera's frame while streaming: a JPEG.</summary>
    public sealed class FrameReceivedEventArgs : EventArgs {
        internal FrameReceivedEventArgs(byte[] jpeg, int width, int height) {
            this.Jpeg = jpeg;
            this.Width = width;
            this.Height = height;
        }

        public byte[] Jpeg { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>
    /// A webcam: any USB Video Class camera. Pictures come as JPEG files, whether the camera
    /// sends MJPEG or raw YUYV pictures.
    /// </summary>
    /// <remarks>
    /// Open it with the DeviceName of a Webcam from UsbHostController ("/dev/video0").
    /// The first picture waits until the camera's automatic exposure has settled (about
    /// a second); while pictures are taken the camera keeps running, so the next ones
    /// come at once. At first the size is 640x480.
    /// <code>
    /// using var camera = new Camera(e.DeviceName);
    /// camera.SetResolution(1280, 720);
    /// camera.Capture("/media/usb0/photo.jpg");
    ///
    /// camera.FrameReceived += (sender, frame) => SendToBrowser(frame.Jpeg);
    /// camera.StartStreaming();
    /// </code>
    /// The events come on the camera's own thread. A camera stays alive until it is disposed.
    /// </remarks>
    public sealed unsafe class Camera : IDisposable {
        private readonly GCHandle self;
        private nint handle;

        /// <summary>The handler of FrameReceived</summary>
        public delegate void FrameReceivedEventHandler(Camera sender, FrameReceivedEventArgs e);

        /// <summary>The handler of Disconnected</summary>
        public delegate void DisconnectedEventHandler(Camera sender, EventArgs e);

        /// <summary>A frame while streaming (StartStreaming)</summary>
        public event FrameReceivedEventHandler? FrameReceived;

        /// <summary>The camera was unplugged</summary>
        public event DisconnectedEventHandler? Disconnected;

        /// <summary>Opens the camera that UsbHostController reported ("/dev/video0").</summary>
        public Camera(string deviceName) {
            ArgumentNullException.ThrowIfNull(deviceName);
            this.DeviceName = deviceName;
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.CameraOpen(deviceName, &Frame, &Unplugged, GCHandle.ToIntPtr(this.self), out this.handle));
                var name = stackalloc byte[256];
                Libendpoint.Check(Libendpoint.CameraInfo(this.handle, name, 256, out _, out var count));
                this.Name = Marshal.PtrToStringUTF8((nint)name) ?? "";
                var formats = new List<CameraFormat>();
                for (var i = 0; i < count; i++) {
                    Libendpoint.Check(Libendpoint.CameraFormat(this.handle, i, out var width, out var height, out var pixelFormat, out var rate));
                    formats.Add(new CameraFormat(width, height, (CameraPixelFormat)pixelFormat, rate));
                }
                this.SupportedFormats = formats;
            }
            catch {
                if (this.handle != 0) {
                    Libendpoint.CameraClose(this.handle);
                    this.handle = 0;
                }
                this.self.Free();
                throw;
            }
        }

        /// <summary>What the camera was opened with: "/dev/video0"</summary>
        public string DeviceName { get; }

        /// <summary>The camera's name ("USB 2.0 Camera: USB Camera")</summary>
        public string Name { get; }

        /// <summary>The sizes the camera has, with what it sends for each</summary>
        public IReadOnlyList<CameraFormat> SupportedFormats { get; }

        public bool Connected {
            get {
                var camera = this.handle;
                if (camera == 0) {
                    return false;
                }
                Libendpoint.Check(Libendpoint.CameraInfo(camera, null, 0, out var connected, out _));
                return connected != 0;
            }
        }

        /// <summary>The picture width in use</summary>
        public int Width => this.Resolution().Width;

        /// <summary>The picture height in use</summary>
        public int Height => this.Resolution().Height;

        /// <summary>What the camera sends for the size in use</summary>
        public CameraPixelFormat PixelFormat => this.Resolution().PixelFormat;

        /// <summary>The frame rate in use (frames per second)</summary>
        public int FrameRate => this.Resolution().FrameRate;

        /// <summary>The JPEG quality (1 to 100) of pictures the library makes from raw YUYV pictures. Defaults to 85.</summary>
        public int JpegQuality {
            get {
                Libendpoint.Check(Libendpoint.CameraGetQuality(this.Handle(), out var quality));
                return quality;
            }
            set => Libendpoint.Check(Libendpoint.CameraSetQuality(this.Handle(), value));
        }

        /// <summary>Whether StartStreaming is on</summary>
        public bool IsStreaming { get; private set; }

        /// <summary>Uses a size the camera has (MJPEG if the camera has it at that size, else YUYV).</summary>
        /// <param name="width">The width, one of SupportedFormats'.</param>
        /// <param name="height">The height.</param>
        /// <param name="frameRate">Frames per second; 0: the camera's highest for that size.</param>
        public void SetResolution(int width, int height, int frameRate = 0) =>
            Libendpoint.Check(Libendpoint.CameraSetResolution(this.Handle(), width, height, frameRate, -1));

        /// <summary>Uses one of SupportedFormats, as it is (YUYV too).</summary>
        public void SetResolution(CameraFormat format, int frameRate = 0) {
            ArgumentNullException.ThrowIfNull(format);
            Libendpoint.Check(Libendpoint.CameraSetResolution(this.Handle(), format.Width, format.Height, frameRate, (int)format.PixelFormat));
        }

        /// <summary>Takes a picture: a JPEG.</summary>
        /// <param name="timeout">How long to wait for the camera, in milliseconds (the first picture also waits for the exposure).</param>
        /// <exception cref="TimeoutException">The camera sent no picture in time.</exception>
        public byte[] Capture(int timeout = 5000) {
            Libendpoint.Check(Libendpoint.CameraCapture(this.Handle(), timeout, out var jpeg, out var length));
            try {
                return new ReadOnlySpan<byte>(jpeg, length).ToArray();
            }
            finally {
                Libendpoint.CameraFree(jpeg);
            }
        }

        /// <summary>Takes a picture and saves it as a JPEG file.</summary>
        public void Capture(string path) {
            ArgumentNullException.ThrowIfNull(path);
            File.WriteAllBytes(path, this.Capture());
        }

        /// <summary>Starts streaming: every frame comes as a JPEG in FrameReceived.</summary>
        public void StartStreaming() {
            Libendpoint.Check(Libendpoint.CameraStartStreaming(this.Handle()));
            this.IsStreaming = true;
        }

        public void StopStreaming() {
            Libendpoint.Check(Libendpoint.CameraStopStreaming(this.Handle()));
            this.IsStreaming = false;
        }

        /// <summary>The controls this camera has</summary>
        public IReadOnlyList<CameraControl> SupportedControls =>
            Enum.GetValues<CameraControl>().Where(control => this.Get(control) != null).ToList();

        /// <summary>A control's range and value.</summary>
        /// <exception cref="NotSupportedException">The camera does not have this control.</exception>
        public CameraControlValue GetControl(CameraControl control) =>
            this.Get(control) ?? throw new NotSupportedException($"The camera has no {control} control");

        /// <summary>Sets a control, in the camera's own units (GetControl gives its range).</summary>
        public void SetControl(CameraControl control, int value) =>
            Libendpoint.Check(Libendpoint.CameraSetControl(this.Handle(), (int)control, value));

        /// <summary>Stops the camera (a frame being delivered finishes first) and closes it</summary>
        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.CameraClose(old);
                this.self.Free();
                this.IsStreaming = false;
            }
        }

        private CameraControlValue? Get(CameraControl control) {
            Libendpoint.Check(Libendpoint.CameraGetControl(this.Handle(), (int)control, out var supported, out var minimum, out var maximum,
                                                           out var step, out var defaultValue, out var value));
            return supported != 0 ? new CameraControlValue(control, minimum, maximum, step, defaultValue, value) : null;
        }

        private (int Width, int Height, CameraPixelFormat PixelFormat, int FrameRate) Resolution() {
            Libendpoint.Check(Libendpoint.CameraGetResolution(this.Handle(), out var width, out var height, out var pixelFormat, out var rate));
            return (width, height, (CameraPixelFormat)pixelFormat, rate);
        }

        private nint Handle() => this.handle != 0 ? this.handle : throw new ObjectDisposedException(nameof(Camera));

        // From the camera's thread. A handler's exception must not stop the
        // frames (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void Frame(nint context, byte* jpeg, int length, int width, int height) {
            try {
                if (GCHandle.FromIntPtr(context).Target is Camera camera && camera.FrameReceived is { } handler) {
                    handler(camera, new FrameReceivedEventArgs(new ReadOnlySpan<byte>(jpeg, length).ToArray(), width, height));
                }
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in FrameReceived: {exception}");
            }
        }

        [UnmanagedCallersOnly]
        private static void Unplugged(nint context) {
            try {
                if (GCHandle.FromIntPtr(context).Target is Camera camera) {
                    camera.Disconnected?.Invoke(camera, EventArgs.Empty);
                }
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in Disconnected: {exception}");
            }
        }
    }
}
