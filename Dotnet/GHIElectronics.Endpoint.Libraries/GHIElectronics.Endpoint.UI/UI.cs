// UI subsystem lifecycle. Must be initialized once before any widget is
// created; typically called after DisplayController.Enable().

using System;
using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public static class UI {
    /// <summary>
    /// Initialize LVGL and bind it to the framebuffer. Call after the display
    /// is enabled. Safe to call multiple times (second and later calls are
    /// no-ops).
    /// </summary>
    public static void Initialize() => Libendpoint.Check(Libendpoint.UiInitialize());

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
    public static void Attach() => Libendpoint.Check(Libendpoint.UiTouchAttach());
    public static void Feed(int x, int y, bool pressed) =>
        Libendpoint.Check(Libendpoint.UiTouchFeed(x, y, pressed ? 1 : 0));
}

/// <summary>
/// Thread-safe periodic callback that runs on the UI thread. The <c>callback</c>
/// fires inside <see cref="UI.Run"/>'s LVGL pump every <c>periodMs</c>
/// milliseconds, so widget updates in it are safe (LVGL itself is single-
/// threaded). Dispose cancels the timer. Create / SetPeriod / Dispose are safe
/// from any thread; the body runs on the UI thread.
/// <code>
///     var t = new UiTimer(100, () => {
///         gauge.Value = (int)(1650 + 1450 * Math.Sin(phase));
///         phase += 0.12;
///     });
/// </code>
/// </summary>
public sealed class UiTimer : IDisposable {
    private readonly Libendpoint.UiTimerCallback _trampoline;
    private readonly Action _callback;
    private GCHandle _trampolineHandle;
    private nint _handle;
    private bool _disposed;

    public UiTimer(uint periodMs, Action callback) {
        this._callback = callback ?? throw new ArgumentNullException(nameof(callback));
        this._trampoline = this.Trampoline;
        // Pin the trampoline delegate so the native side can keep calling it.
        this._trampolineHandle = GCHandle.Alloc(this._trampoline);
        var fnPtr = Marshal.GetFunctionPointerForDelegate(this._trampoline);
        Libendpoint.Check(Libendpoint.UiTimerCreate(periodMs, fnPtr, 0, out this._handle));
    }

    public UiTimer(TimeSpan period, Action callback)
        : this((uint)Math.Max(1, (long)period.TotalMilliseconds), callback) { }

    public void SetPeriod(uint periodMs) {
        if (!this._disposed && this._handle != 0) {
            Libendpoint.Check(Libendpoint.UiTimerSetPeriod(this._handle, periodMs));
        }
    }

    public void SetPeriod(TimeSpan period) =>
        this.SetPeriod((uint)Math.Max(1, (long)period.TotalMilliseconds));

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
