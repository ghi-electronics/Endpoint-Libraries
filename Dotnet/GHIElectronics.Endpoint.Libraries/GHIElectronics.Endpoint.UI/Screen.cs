using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// A top-level container with no parent. Multiple screens can exist at once;
/// only one is active at a time. Call <see cref="Load"/> to make this the
/// currently-displayed screen.
/// </summary>
public class Screen : Widget {
    /// <summary>Create a new standalone screen (invisible until Load()).</summary>
    public Screen() : base(0) {
        Libendpoint.Check(Libendpoint.UiScreenCreate(out var n));
        this.Native = n;
    }

    // Internal ctor used by Active (which doesn't own the handle).
    private Screen(nint existing) : base(existing) { }

    /// <summary>Make this the currently-displayed screen.</summary>
    public void Load() => Libendpoint.Check(Libendpoint.UiScreenLoad(this.Native));

    /// <summary>
    /// The screen currently displayed on the panel. The returned instance
    /// does not own its handle and should not be disposed.
    /// </summary>
    public static Screen Active {
        get {
            Libendpoint.Check(Libendpoint.UiScreenActive(out var n));
            return new Screen(n);
        }
    }
}
