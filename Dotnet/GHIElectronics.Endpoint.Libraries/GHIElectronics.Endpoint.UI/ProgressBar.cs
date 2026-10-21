using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Horizontal progress bar. No events; the owner drives it via
/// <see cref="Value"/>.
/// </summary>
public class ProgressBar : Widget {
    private int _minimum = 0;
    private int _maximum = 100;

    public ProgressBar(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiProgressBarCreate(parent.Native, out var n));
        this.Native = n;
    }

    // LVGL stores min + max as a pair; track them managed-side.
    public int Minimum {
        get => this._minimum;
        set { this._minimum = value; Libendpoint.Check(Libendpoint.UiProgressBarSetRange(this.Native, this._minimum, this._maximum)); }
    }

    public int Maximum {
        get => this._maximum;
        set { this._maximum = value; Libendpoint.Check(Libendpoint.UiProgressBarSetRange(this.Native, this._minimum, this._maximum)); }
    }

    public int Value {
        get { Libendpoint.Check(Libendpoint.UiProgressBarGetValue(this.Native, out var v)); return v; }
        set => Libendpoint.Check(Libendpoint.UiProgressBarSetValue(this.Native, value));
    }
}
