using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public class CheckBox : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    public unsafe CheckBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiCheckBoxCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    public string Text { set => Libendpoint.Check(Libendpoint.UiCheckBoxSetText(this.Native, value)); }

    public bool IsChecked {
        get { Libendpoint.Check(Libendpoint.UiCheckBoxGetChecked(this.Native, out var v)); return v != 0; }
        set => Libendpoint.Check(Libendpoint.UiCheckBoxSetChecked(this.Native, value ? 1 : 0));
    }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
