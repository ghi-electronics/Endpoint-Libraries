namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// A top-level container with no parent. Multiple screens can exist at once;
/// only one is active at a time. Call <see cref="Load"/> to make this the
/// currently-displayed screen.
/// </summary>
public class Screen : Widget {
    /// <summary>Create a new standalone screen (invisible until Load()).</summary>
    public Screen() : base(IntPtr.Zero) {
        throw new NotImplementedException();  // M4
    }

    /// <summary>Make this the currently-displayed screen.</summary>
    public void Load() {
        throw new NotImplementedException();  // M4
    }

    /// <summary>The screen currently displayed on the panel.</summary>
    public static Screen Active {
        get => throw new NotImplementedException();  // M4
    }
}
