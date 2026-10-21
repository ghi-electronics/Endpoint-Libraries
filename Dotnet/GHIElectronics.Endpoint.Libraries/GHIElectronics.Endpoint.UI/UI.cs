// UI subsystem lifecycle. Must be initialized once before any widget is
// created; typically called after DisplayController.Enable().

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
