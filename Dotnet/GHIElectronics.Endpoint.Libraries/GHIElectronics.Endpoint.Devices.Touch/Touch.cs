#nullable enable
using System.Runtime.InteropServices;
using System.Text;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Touch {
    /// <summary>How the Points changed between frames.</summary>
    public enum TouchEventType {
        /// <summary>First finger down (points went from 0 to one or more).</summary>
        Pressed = 0,
        /// <summary>Last finger up (points went from one or more to 0).</summary>
        Released = 1,
        /// <summary>A point moved, or a finger landed/lifted while others remain.</summary>
        Moved = 2,
    }

    /// <summary>One finger on the touchscreen.</summary>
    public readonly struct TouchPoint {
        public TouchPoint(int id, int x, int y) { this.Id = id; this.X = x; this.Y = y; }
        /// <summary>Tracking ID, stable per finger across moves within one gesture.</summary>
        public int Id { get; }
        public int X { get; }
        public int Y { get; }
    }

    public sealed class TouchEventArgs : EventArgs {
        public TouchEventArgs(TouchEventType eventType, TouchPoint[] points) {
            this.EventType = eventType;
            this.Points = points;
        }
        public TouchEventType EventType { get; }
        /// <summary>Every finger currently on the screen. Empty for Released.</summary>
        public TouchPoint[] Points { get; }
    }

    public delegate void TouchChangedEventHandler(Touch sender, TouchEventArgs e);

    /// <summary>
    /// A touchscreen on the board. This class is abstract: the library ships
    /// one concrete subclass, <see cref="InputTouch"/>, for touchscreens the
    /// kernel exposes at <c>/dev/input/eventN</c> (USB HID touchscreens). All
    /// other touch controllers (SPI chips like XPT2046, I2C chips like FT5x06
    /// or GT911, custom boards) are the customer's own subclass code using the
    /// Core Spi/I2c/Gpio APIs. Interrupt-based drivers are first class:
    /// <see cref="System.Device.Gpio.GpioPin.ValueChanged"/> wakes the touch
    /// thread the moment the chip pulls the IRQ line.
    ///
    /// Subclasses raise <see cref="OnChangedEvent"/> by calling
    /// <see cref="RaiseChanged"/> from their reader loop or interrupt handler.
    /// </summary>
    public abstract class Touch : IDisposable {
        /// <summary>Visible width of the touchscreen area, in pixels.</summary>
        public abstract int Width { get; }

        /// <summary>Visible height of the touchscreen area, in pixels.</summary>
        public abstract int Height { get; }

        /// <summary>True while the touchscreen is physically present and responding.</summary>
        public abstract bool Connected { get; }

        /// <summary>Fired on every change in finger state (press / move / release).</summary>
        public event TouchChangedEventHandler? OnChangedEvent;

        /// <summary>Subclasses call this to emit a touch event to the user.</summary>
        protected void RaiseChanged(TouchEventArgs e) {
            try {
                this.OnChangedEvent?.Invoke(this, e);
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in a Touch event: {exception}");
            }
        }

        public abstract void Dispose();

        /// <summary>
        /// The first kernel-exposed touchscreen the library can find at
        /// <c>/dev/input/eventN</c>, or null if none is present. Used for
        /// USB touchscreens; the on-board panel's chip is the customer's
        /// own subclass and does not come through here.
        /// </summary>
        public static Touch? GetDefault() => InputTouch.FindDefault();
    }

    /// <summary>
    /// Touchscreen backed by a kernel input device at <c>/dev/input/eventN</c>.
    /// USB HID touchscreens show up this way automatically.
    /// </summary>
    public sealed unsafe class InputTouch : Touch {
        private readonly GCHandle self;
        private nint handle;
        private readonly int width, height;
        private bool connected;

        public override int Width => this.width;
        public override int Height => this.height;
        public override bool Connected => this.connected;

        public string DeviceName { get; }
        public int VendorId { get; }
        public int ProductId { get; }

        /// <summary>Opens a specific /dev/input/eventN path.</summary>
        public InputTouch(string deviceName) {
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.TouchOpen(deviceName, &Changed,
                                                       GCHandle.ToIntPtr(this.self), out this.handle));
                Libendpoint.Check(Libendpoint.TouchInfo(this.handle, out var vendor, out var product,
                                                       out var isConnected, out var w, out var h));
                this.VendorId = vendor;
                this.ProductId = product;
                this.connected = isConnected != 0;
                this.width = w;
                this.height = h;
                this.DeviceName = deviceName;
            }
            catch {
                this.self.Free();
                throw;
            }
        }

        internal static InputTouch? FindDefault() {
            var buffer = new byte[64];
            fixed (byte* p = buffer) {
                Libendpoint.Check(Libendpoint.TouchFindDefault(p, buffer.Length));
            }
            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = buffer.Length;
            if (length == 0) return null;
            string path = Encoding.ASCII.GetString(buffer, 0, length);
            return new InputTouch(path);
        }

        public override void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.TouchClose(old);
                this.self.Free();
            }
        }

        [UnmanagedCallersOnly]
        private static void Changed(nint context, int eventType, int pointCount, int* ids, int* xs, int* ys) {
            if (GCHandle.FromIntPtr(context).Target is not InputTouch touch) return;
            var points = new TouchPoint[pointCount];
            for (int i = 0; i < pointCount; ++i) {
                points[i] = new TouchPoint(ids[i], xs[i], ys[i]);
            }
            touch.RaiseChanged(new TouchEventArgs((TouchEventType)eventType, points));
        }
    }
}
