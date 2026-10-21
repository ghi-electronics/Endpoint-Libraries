// One static unmanaged trampoline shared by every widget event. libendpoint
// calls it with the GCHandle (as nint) of a managed Action; we look up the
// Action and invoke it.
//
// [UnmanagedCallersOnly] requires a static non-generic method with a plain
// unmanaged calling convention, so one trampoline is reused for every event
// type (Clicked, ValueChanged, ...).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GHIElectronics.Endpoint.UI;

internal static class EventTrampolines {
    [UnmanagedCallersOnly]
    internal static unsafe void Invoke(nint context) {
        try {
            if (GCHandle.FromIntPtr(context).Target is Action action) action();
        }
        catch {
            // Swallow — a C callback must not propagate .NET exceptions back
            // into native code (would unwind through the LVGL stack).
        }
    }

    // Returns a GCHandle pinning the Action; caller stashes it on the widget
    // so GC doesn't reclaim the delegate while LVGL still holds the context.
    internal static GCHandle Pin(Action action) => GCHandle.Alloc(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe delegate* unmanaged<nint, void> Pointer() => &Invoke;
}
