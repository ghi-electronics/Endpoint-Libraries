using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Two-state check box with a label. <see cref="ValueChanged"/> fires on each toggle.</summary>
public class CheckBox : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    /// <summary>Create a check box inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public unsafe CheckBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiCheckBoxCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    private string _text = string.Empty;
    /// <summary>Label text shown next to the check box. <c>null</c> is treated as empty. Getter returns the last value set.</summary>
    public string Text {
        get => this._text;
        set { this._text = value ?? string.Empty; Libendpoint.Check(Libendpoint.UiCheckBoxSetText(this.Native, this._text)); }
    }

    /// <summary>Checked state. Read-back is supported by LVGL.</summary>
    public bool IsChecked {
        get { Libendpoint.Check(Libendpoint.UiCheckBoxGetChecked(this.Native, out var v)); return v != 0; }
        set => Libendpoint.Check(Libendpoint.UiCheckBoxSetChecked(this.Native, value ? 1 : 0));
    }

    /// <summary>Fires when <see cref="IsChecked"/> toggles via user interaction. Runs on the UI thread.</summary>
    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
