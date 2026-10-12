using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Watchdog {
    /// <summary>
    /// Endpoint's watchdog, with the same API as TinyCLR. After
    /// <see cref="Enable(TimeSpan)"/>, <see cref="Reset"/> must be called more
    /// often than the timeout, or the board restarts:
    /// <code>
    /// var watchdog = WatchdogController.FromName(EPM815.Watchdog.Id);
    /// watchdog.Enable(TimeSpan.FromSeconds(10));
    /// while (true) { DoWork(); watchdog.Reset(); }
    /// </code>
    /// The chip's watchdog cannot be stopped once it runs. <see cref="Disable"/>
    /// hands it to Linux, which then keeps it fed. If the program ends or
    /// crashes without Disable, the board restarts after the timeout, also
    /// when debugging stops.
    /// </summary>
    public class WatchdogController : IDisposable {
        // Linux's watchdog calls (linux/watchdog.h)
        private const nuint KeepAlive = 0x80045705;   // WDIOC_KEEPALIVE
        private const nuint SetTimeout = 0xC0045706;  // WDIOC_SETTIMEOUT, in seconds

        private readonly object gate = new();
        private int fd = -1;
        private bool disposed;

        private WatchdogController() {
        }

        /// <summary>The board's watchdog</summary>
        public static WatchdogController GetDefault() => new();

        /// <summary>The watchdog named <paramref name="name"/>: EPM815.Watchdog.Id</summary>
        public static WatchdogController FromName(string name) =>
            name == EPM815.Watchdog.Id
                ? new WatchdogController()
                : throw new ArgumentException($"There is no watchdog named \"{name}\": use EPM815.Watchdog.Id", nameof(name));

        /// <summary>The longest timeout: 131 seconds (4096 × 1024 ticks of the chip's 32 kHz clock)</summary>
        public TimeSpan MaxTimeout => TimeSpan.FromSeconds(131);

        /// <summary>Whether this controller has enabled the watchdog (and not disabled it)</summary>
        public bool IsEnabled {
            get {
                lock (this.gate) {
                    return this.fd >= 0;
                }
            }
        }

        /// <summary>
        /// Starts the watchdog, or changes its timeout. Linux counts whole
        /// seconds, so the timeout is rounded up to the next second.
        /// </summary>
        /// <param name="timeout">More than 0 and at most <see cref="MaxTimeout"/></param>
        public unsafe void Enable(TimeSpan timeout) {
            if (timeout <= TimeSpan.Zero || timeout > this.MaxTimeout) {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                    $"The timeout must be more than 0 and at most {this.MaxTimeout.TotalSeconds} seconds");
            }
            var seconds = (int)Math.Ceiling(timeout.TotalSeconds);

            lock (this.gate) {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                if (this.fd < 0) {
                    var path = Device();
                    // Opening the device starts the watchdog
                    var opened = Libc.Open(path, Libc.O_WRONLY | Libc.O_CLOEXEC);
                    if (opened < 0) {
                        if (Marshal.GetLastPInvokeError() == Libc.EBUSY) {
                            throw new InvalidOperationException("The watchdog is already enabled by another program or WatchdogController");
                        }
                        throw Libc.Error($"Cannot open {path}");
                    }
                    this.fd = opened;
                }
                if (Libc.Ioctl(this.fd, SetTimeout, (nint)(&seconds)) < 0) {
                    throw Libc.Error("Cannot set the watchdog's timeout");
                }
                this.Feed();
            }
        }

        /// <summary>Starts the timeout again. Call it more often than the timeout.</summary>
        public void Reset() {
            lock (this.gate) {
                if (this.fd < 0) {
                    throw new InvalidOperationException("The watchdog is not enabled: call Enable first");
                }
                this.Feed();
            }
        }

        /// <summary>
        /// Stops watching this program: Linux keeps the watchdog fed from now
        /// on (the chip's watchdog itself cannot stop)
        /// </summary>
        public unsafe void Disable() {
            lock (this.gate) {
                if (this.fd < 0) {
                    return;
                }
                // "V" before closing tells Linux that the stop is on purpose
                var magic = (byte)'V';
                Libc.Write(this.fd, (nint)(&magic), 1);
                Libc.Close(this.fd);
                this.fd = -1;
            }
        }

        /// <summary>
        /// Releases the controller. An enabled watchdog stays enabled: with
        /// nobody to reset it, the board restarts after the timeout. Call
        /// <see cref="Disable"/> first to avoid that.
        /// </summary>
        public void Dispose() {
            lock (this.gate) {
                if (this.fd >= 0) {
                    Libc.Close(this.fd);
                    this.fd = -1;
                }
                this.disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        private unsafe void Feed() {
            var unused = 0;
            if (Libc.Ioctl(this.fd, KeepAlive, (nint)(&unused)) < 0) {
                throw Libc.Error("Cannot reset the watchdog");
            }
        }

        // /dev/watchdogN of the chip's IWDG2 (N depends on the order the drivers started in)
        private static string Device() {
            var number = LinuxDevices.Find("watchdog", "watchdog", EPM815.Watchdog.Address);
            return number >= 0
                ? $"/dev/watchdog{number}"
                : throw new InvalidOperationException("The watchdog is not enabled in this firmware: update the board's firmware");
        }
    }
}
