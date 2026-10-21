using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Select one item from a list. ValueChanged fires when the selection
/// changes.
/// </summary>
public class Dropdown : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    public unsafe Dropdown(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiDropdownCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    /// <summary>Set the items. Internally joined with newline before passing to libendpoint.</summary>
    public void SetOptions(IEnumerable<string> options) =>
        Libendpoint.Check(Libendpoint.UiDropdownSetOptions(this.Native, string.Join('\n', options)));

    public int Selected {
        get { Libendpoint.Check(Libendpoint.UiDropdownGetSelected(this.Native, out var v)); return v; }
        set => Libendpoint.Check(Libendpoint.UiDropdownSetSelected(this.Native, value));
    }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
