using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public class Slider : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;
    private int _min = 0;
    private int _max = 100;

    public unsafe Slider(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiSliderCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    // LVGL stores min and max as a pair, so we track them on the managed side
    // and push both whenever either changes.
    public int Minimum {
        get => this._min;
        set { this._min = value; Libendpoint.Check(Libendpoint.UiSliderSetRange(this.Native, this._min, this._max)); }
    }

    public int Maximum {
        get => this._max;
        set { this._max = value; Libendpoint.Check(Libendpoint.UiSliderSetRange(this.Native, this._min, this._max)); }
    }

    public int Value {
        get { Libendpoint.Check(Libendpoint.UiSliderGetValue(this.Native, out var v)); return v; }
        set => Libendpoint.Check(Libendpoint.UiSliderSetValue(this.Native, value));
    }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
