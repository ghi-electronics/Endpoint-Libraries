namespace GHIElectronics.Endpoint.UI;

public class Button : Widget {
    public Button(Widget parent) : base(IntPtr.Zero) {
        throw new NotImplementedException();  // M4
    }

    public string Text {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public uint BackgroundColor {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public uint TextColor {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int FontSize {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public event EventHandler? Clicked;

    // Called by the native event trampoline (M4). Protected so subclasses may
    // override to intercept.
    protected virtual void OnClicked() => this.Clicked?.Invoke(this, EventArgs.Empty);
}
