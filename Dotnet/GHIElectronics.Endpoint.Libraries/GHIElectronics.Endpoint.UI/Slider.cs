namespace GHIElectronics.Endpoint.UI;

public class Slider : Widget {
    public Slider(Widget parent) : base(IntPtr.Zero) {
        throw new NotImplementedException();  // M4
    }

    public int Minimum {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int Maximum {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int Value {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public event EventHandler? ValueChanged;

    protected virtual void OnValueChanged() => this.ValueChanged?.Invoke(this, EventArgs.Empty);
}
