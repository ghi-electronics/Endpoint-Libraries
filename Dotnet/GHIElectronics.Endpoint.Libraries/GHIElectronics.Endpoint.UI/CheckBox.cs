namespace GHIElectronics.Endpoint.UI;

public class CheckBox : Widget {
    public CheckBox(Widget parent) : base(IntPtr.Zero) {
        throw new NotImplementedException();  // M4
    }

    public string Text {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public bool IsChecked {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public event EventHandler? ValueChanged;

    protected virtual void OnValueChanged() => this.ValueChanged?.Invoke(this, EventArgs.Empty);
}
