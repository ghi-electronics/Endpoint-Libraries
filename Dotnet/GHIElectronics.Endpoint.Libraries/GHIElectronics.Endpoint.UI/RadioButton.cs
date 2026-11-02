using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Circular-indicator CheckBox. Grouping (mutual exclusion) is up to the
/// caller: subscribe to ValueChanged on each radio and clear the others
/// when one gets selected.
/// </summary>
public class RadioButton : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    /// <summary>Create a radio button inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public unsafe RadioButton(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiRadioButtonCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    private string _text = string.Empty;
    /// <summary>Label text shown next to the radio button. <c>null</c> is treated as empty. Getter returns the last value set.</summary>
    public string Text {
        get => this._text;
        set { this._text = value ?? string.Empty; Libendpoint.Check(Libendpoint.UiRadioButtonSetText(this.Native, this._text)); }
    }

    /// <summary>Selected state. Read-back is supported by LVGL.</summary>
    public bool IsChecked {
        get { Libendpoint.Check(Libendpoint.UiRadioButtonGetChecked(this.Native, out var v)); return v != 0; }
        set => Libendpoint.Check(Libendpoint.UiRadioButtonSetChecked(this.Native, value ? 1 : 0));
    }

    /// <summary>Fires when the radio button is toggled via user interaction. Runs on the UI thread.</summary>
    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
