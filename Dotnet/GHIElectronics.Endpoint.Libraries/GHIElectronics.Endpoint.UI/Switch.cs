using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Binary on/off toggle. ValueChanged fires on each flip.
/// </summary>
public class Switch : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    /// <summary>Create a switch inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public unsafe Switch(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiSwitchCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    /// <summary>On/off state. Read-back is supported by LVGL.</summary>
    public bool IsChecked {
        get { Libendpoint.Check(Libendpoint.UiSwitchGetChecked(this.Native, out var v)); return v != 0; }
        set => Libendpoint.Check(Libendpoint.UiSwitchSetChecked(this.Native, value ? 1 : 0));
    }

    /// <summary>Fires on every flip of <see cref="IsChecked"/> from user interaction. Runs on the UI thread.</summary>
    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
