// UI subsystem lifecycle. Must be initialized once before any widget is
// created; typically called after DisplayController.Enable().

using System;
using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;
using GHIElectronics.Endpoint.Drawing;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// LVGL subsystem lifecycle. Call <see cref="Initialize()"/> once after the
/// display is enabled, then <see cref="Run"/> on your UI thread. LVGL is
/// built single-threaded (<c>LV_USE_OS=LV_OS_NONE</c>); use
/// <see cref="UiTimer"/> for periodic work that must touch widgets.
/// </summary>
public static unsafe class UI {
    /// <summary>
    /// Callback invoked by LVGL whenever a frame is ready to be pushed to the
    /// physical display. The bytes are in the pixel format passed to
    /// <see cref="Initialize(int, int, PixelFormat, FlushCallback)"/>. The
    /// callback runs on the UI thread. Keep it fast; return as soon as the
    /// bytes have been handed off to the transport (SPI/I2C write, file
    /// write, socket send, …).
    /// </summary>
    public delegate void FlushCallback(byte[] pixels);

    /// <summary>
    /// Initialize LVGL and bind it to the on-board parallel-RGB framebuffer
    /// (<c>/dev/fb0</c>). Call after <c>DisplayController.Enable()</c>.
    /// Safe to call multiple times (second and later calls are no-ops).
    /// </summary>
    public static void Initialize() => Libendpoint.Check(Libendpoint.UiInitialize());

    // Kept alive at the module level so the GC doesn't collect them while the
    // native display driver still holds pointers.
    private static FlushCallback? _customFlush;
    private static GCHandle _customContextHandle;

    /// <summary>
    /// Initialize LVGL for a custom display of any transport: SPI, I2C,
    /// UART, HDMI, a file, a network socket. LVGL renders each frame into an
    /// internal buffer and invokes <paramref name="flush"/> with the full
    /// rendered bytes; the caller pushes them to the physical device.
    /// Safe to call multiple times (second and later calls are no-ops).
    /// </summary>
    /// <example>
    /// <code>
    /// // 128x64 SSD1306 OLED over I2C (1 bpp, MSB-first)
    /// UI.Initialize(128, 64, PixelFormat.Monochrome, buffer => i2c.Write(buffer));
    ///
    /// // 240x320 ILI9341 SPI TFT (RGB565)
    /// UI.Initialize(240, 320, PixelFormat.Rgb565, buffer => spi.Write(buffer));
    /// </code>
    /// </example>
    public static void Initialize(int width, int height, PixelFormat format, FlushCallback flush) {
        ArgumentNullException.ThrowIfNull(flush);

        // Pin the user's delegate as the native context so the trampoline can
        // find it via GCHandle.FromIntPtr. One slot, one display (matches the
        // single-display on-board initialize).
        if (_customContextHandle.IsAllocated) _customContextHandle.Free();
        _customFlush = flush;
        _customContextHandle = GCHandle.Alloc(flush);

        Libendpoint.Check(Libendpoint.UiInitializeCustom(
            width, height, (int)format,
            &CustomFlushTrampoline,
            GCHandle.ToIntPtr(_customContextHandle)));
    }

    [UnmanagedCallersOnly]
    private static void CustomFlushTrampoline(nint context, byte* pixels, int byteCount) {
        if (GCHandle.FromIntPtr(context).Target is not FlushCallback cb) return;
        var managed = new byte[byteCount];
        Marshal.Copy((nint)pixels, managed, 0, byteCount);
        try { cb(managed); }
        catch (Exception ex) {
            Console.Error.WriteLine($"Unhandled exception in UI flush callback: {ex}");
        }
    }

    /// <summary>
    /// Run the event loop until <see cref="Stop"/> is called. Blocks the
    /// calling thread. Pumps LVGL timers and sleeps between frames.
    /// </summary>
    public static void Run() => Libendpoint.Check(Libendpoint.UiRun());

    /// <summary>Request the event loop to exit at the next tick.</summary>
    public static void Stop() => Libendpoint.Check(Libendpoint.UiStop());
}

/// <summary>
/// Touch input bridge. Call <see cref="Attach"/> once after
/// <see cref="UI.Initialize"/>, then call <see cref="Feed"/> from your
/// touch chip's event handler. LVGL will dispatch Clicked / ValueChanged
/// accordingly on its event loop.
/// </summary>
public static class TouchBridge {
    /// <summary>Register the touch input device with LVGL. Call once after <see cref="UI.Initialize"/>.</summary>
    public static void Attach() => Libendpoint.Check(Libendpoint.UiTouchAttach());

    /// <summary>Submit one touch sample. Call from the touch driver's event handler.</summary>
    /// <param name="x">Touch X coordinate in pixels.</param>
    /// <param name="y">Touch Y coordinate in pixels.</param>
    /// <param name="pressed"><c>true</c> while the finger is down; <c>false</c> on release.</param>
    public static void Feed(int x, int y, bool pressed) =>
        Libendpoint.Check(Libendpoint.UiTouchFeed(x, y, pressed ? 1 : 0));
}

/// <summary>
/// Thread-safe periodic callback that runs on the UI thread. The <c>callback</c>
/// fires inside <see cref="UI.Run"/>'s LVGL pump every <c>periodMs</c>
/// milliseconds, so widget updates in it are safe (LVGL itself is single-
/// threaded). Dispose cancels the timer. Create / SetPeriod / Dispose are safe
/// from any thread; the body runs on the UI thread.
/// </summary>
/// <example>
/// <code>
/// var t = new UiTimer(100, () => {
///     gauge.Value = (int)(1650 + 1450 * Math.Sin(phase));
///     phase += 0.12;
/// });
/// </code>
/// </example>
public sealed class UiTimer : IDisposable {
    private readonly Libendpoint.UiTimerCallback _trampoline;
    private readonly Action _callback;
    private GCHandle _trampolineHandle;
    private nint _handle;
    private bool _disposed;

    /// <summary>Create a periodic timer that fires <paramref name="callback"/> on the UI thread.</summary>
    /// <param name="periodMs">Period in milliseconds between callbacks.</param>
    /// <param name="callback">Delegate to invoke on each tick; runs on LVGL's event thread so widget updates are safe.</param>
    public UiTimer(uint periodMs, Action callback) {
        this._callback = callback ?? throw new ArgumentNullException(nameof(callback));
        this._trampoline = this.Trampoline;
        // Pin the trampoline delegate so the native side can keep calling it.
        this._trampolineHandle = GCHandle.Alloc(this._trampoline);
        var fnPtr = Marshal.GetFunctionPointerForDelegate(this._trampoline);
        Libendpoint.Check(Libendpoint.UiTimerCreate(periodMs, fnPtr, 0, out this._handle));
    }

    /// <summary>Create a periodic timer with a <see cref="TimeSpan"/> period (clamped to at least 1 ms).</summary>
    /// <param name="period">Period between callbacks.</param>
    /// <param name="callback">Delegate to invoke on each tick; runs on the UI thread.</param>
    public UiTimer(TimeSpan period, Action callback)
        : this((uint)Math.Max(1, (long)period.TotalMilliseconds), callback) { }

    /// <summary>Change the timer period in milliseconds. Safe to call from any thread.</summary>
    /// <param name="periodMs">New period in milliseconds.</param>
    public void SetPeriod(uint periodMs) {
        if (!this._disposed && this._handle != 0) {
            Libendpoint.Check(Libendpoint.UiTimerSetPeriod(this._handle, periodMs));
        }
    }

    /// <summary>Change the timer period (clamped to at least 1 ms). Safe to call from any thread.</summary>
    /// <param name="period">New period.</param>
    public void SetPeriod(TimeSpan period) =>
        this.SetPeriod((uint)Math.Max(1, (long)period.TotalMilliseconds));

    /// <summary>Cancel the timer and release its pinned callback. Idempotent.</summary>
    public void Dispose() {
        if (this._disposed) return;
        this._disposed = true;
        if (this._handle != 0) {
            Libendpoint.UiTimerDelete(this._handle);
            this._handle = 0;
        }
        if (this._trampolineHandle.IsAllocated) this._trampolineHandle.Free();
    }

    private void Trampoline(nint _context) {
        if (!this._disposed) this._callback();
    }
}
