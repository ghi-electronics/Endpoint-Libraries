using System.Runtime.InteropServices;
using System.Text;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
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
        internal TouchEventArgs(TouchEventType eventType, TouchPoint[] points) {
            this.EventType = eventType;
            this.Points = points;
        }
        public TouchEventType EventType { get; }
        /// <summary>Every finger currently on the screen. Empty for Released.</summary>
        public TouchPoint[] Points { get; }
    }

    public delegate void TouchChangedEventHandler(Touch sender, TouchEventArgs e);

    /// <summary>
    /// A touchscreen. Covers both the built-in display's touch (configured in
    /// display.json; use <see cref="GetDefault"/>) and USB touchscreens
    /// (hot-plugged, discovered via <see cref="UsbHostController"/> as
    /// <see cref="DeviceType.Touch"/>; construct from <c>e.DeviceName</c>).
    /// Multi-touch is first class; single-touch devices simply report one point.
    /// <code>
    /// var touch = Touch.GetDefault();   // null if no built-in touch
    /// if (touch != null) {
    ///     touch.OnChangedEvent += (s, e) =&gt; {
    ///         foreach (var p in e.Points) Console.WriteLine($"{p.Id}: ({p.X},{p.Y})");
    ///     };
    /// }
    /// </code>
    /// </summary>
    public sealed unsafe class Touch : IDisposable {
        private readonly GCHandle self;
        private nint handle;

        /// <summary>Opens a touchscreen at a specific /dev/input/eventN path.</summary>
        public Touch(string deviceName) {
            this.self = GCHandle.Alloc(this);
            try {
                Libendpoint.Check(Libendpoint.TouchOpen(deviceName, &Changed, GCHandle.ToIntPtr(this.self), out this.handle));
                Libendpoint.Check(Libendpoint.TouchInfo(this.handle, out var vendor, out var product,
                                                       out var connected, out var width, out var height));
                this.VendorId = vendor;
                this.ProductId = product;
                this.Connected = connected != 0;
                this.Width = width;
                this.Height = height;
                this.DeviceName = deviceName;
            }
            catch {
                this.self.Free();
                throw;
            }
        }

        /// <summary>
        /// The built-in touchscreen configured in /config/display.json, or null
        /// if no built-in touch is wired up. USB touchscreens come through
        /// <see cref="UsbHostController"/>, not this method.
        /// </summary>
        public static Touch GetDefault() {
            var buffer = new byte[64];
            fixed (byte* p = buffer) {
                Libendpoint.Check(Libendpoint.TouchFindDefault(p, buffer.Length));
            }
            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = buffer.Length;
            if (length == 0) return null;
            string path = Encoding.ASCII.GetString(buffer, 0, length);
            return new Touch(path);
        }

        public string DeviceName { get; }
        public int VendorId { get; }
        public int ProductId { get; }
        public bool Connected { get; private set; }
        public int Width { get; }
        public int Height { get; }

        public event TouchChangedEventHandler OnChangedEvent;

        public void Dispose() {
            var old = Interlocked.Exchange(ref this.handle, 0);
            if (old != 0) {
                Libendpoint.TouchClose(old);
                this.self.Free();
            }
        }

        // From the device's thread. A handler's exception must not stop the
        // events (it cannot go back into the library): it is printed.
        [UnmanagedCallersOnly]
        private static void Changed(nint context, int eventType, int pointCount, int* ids, int* xs, int* ys) {
            try {
                if (GCHandle.FromIntPtr(context).Target is not Touch touch) {
                    return;
                }
                var points = new TouchPoint[pointCount];
                for (int i = 0; i < pointCount; ++i) {
                    points[i] = new TouchPoint(ids[i], xs[i], ys[i]);
                }
                touch.OnChangedEvent?.Invoke(touch, new TouchEventArgs((TouchEventType)eventType, points));
            }
            catch (Exception exception) {
                Console.Error.WriteLine($"Unhandled exception in a Touch event: {exception}");
            }
        }
    }
}
