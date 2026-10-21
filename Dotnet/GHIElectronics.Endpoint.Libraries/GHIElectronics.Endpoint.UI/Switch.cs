using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Binary on/off toggle. ValueChanged fires on each flip.
/// </summary>
public class Switch : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    public unsafe Switch(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiSwitchCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    public bool IsChecked {
        get { Libendpoint.Check(Libendpoint.UiSwitchGetChecked(this.Native, out var v)); return v != 0; }
        set => Libendpoint.Check(Libendpoint.UiSwitchSetChecked(this.Native, value ? 1 : 0));
    }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
