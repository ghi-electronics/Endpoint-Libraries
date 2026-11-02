using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Horizontal progress bar. No events; the owner drives it via
/// <see cref="Value"/>.
/// </summary>
public class ProgressBar : Widget {
    private int _minimum = 0;
    private int _maximum = 100;

    /// <summary>Create a progress bar inside <paramref name="parent"/>. Default range is 0-100.</summary>
    /// <param name="parent">Containing widget.</param>
    public ProgressBar(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiProgressBarCreate(parent.Native, out var n));
        this.Native = n;
    }

    // LVGL stores min + max as a pair; track them managed-side.
    /// <summary>Lower bound of <see cref="Value"/>. Setting it pushes both bounds to LVGL. Getter returns the last value set.</summary>
    public int Minimum {
        get => this._minimum;
        set { this._minimum = value; Libendpoint.Check(Libendpoint.UiProgressBarSetRange(this.Native, this._minimum, this._maximum)); }
    }

    /// <summary>Upper bound of <see cref="Value"/>. Setting it pushes both bounds to LVGL. Getter returns the last value set.</summary>
    public int Maximum {
        get => this._maximum;
        set { this._maximum = value; Libendpoint.Check(Libendpoint.UiProgressBarSetRange(this.Native, this._minimum, this._maximum)); }
    }

    /// <summary>Current fill between <see cref="Minimum"/> and <see cref="Maximum"/>. Read-back is supported by LVGL.</summary>
    public int Value {
        get { Libendpoint.Check(Libendpoint.UiProgressBarGetValue(this.Native, out var v)); return v; }
        set => Libendpoint.Check(Libendpoint.UiProgressBarSetValue(this.Native, value));
    }
}
