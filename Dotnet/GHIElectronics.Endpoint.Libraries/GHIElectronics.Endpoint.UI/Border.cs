using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Container with WPF-style <see cref="Thickness"/>, <see cref="BorderBrush"/>,
/// <see cref="CornerRadius"/>, and <see cref="Padding"/>. Children are
/// positioned relative to this border, same as <see cref="Container"/>.
/// Background is set via the inherited <see cref="Widget.BackgroundColor"/>.
/// </summary>
public class Border : Widget {
    /// <summary>Create a border inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Border(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiBorderCreate(parent.Native, out var n));
        this.Native = n;
    }

    private int _thickness;
    private uint _borderBrush;
    private int _cornerRadius;
    private int _padding;

    /// <summary>Border line thickness in pixels. Getter returns the last value set (LVGL has no read-back).</summary>
    public int Thickness {
        get => this._thickness;
        set { this._thickness = value; Libendpoint.Check(Libendpoint.UiBorderSetThickness(this.Native, value)); }
    }
    /// <summary>Border line color as <c>0x00RRGGBB</c>. Getter returns the last value set (LVGL has no read-back).</summary>
    public uint BorderBrush {
        get => this._borderBrush;
        set { this._borderBrush = value; Libendpoint.Check(Libendpoint.UiBorderSetBrush(this.Native, value)); }
    }
    /// <summary>Corner radius in pixels (0 = sharp corners). Getter returns the last value set (LVGL has no read-back).</summary>
    public int CornerRadius {
        get => this._cornerRadius;
        set { this._cornerRadius = value; Libendpoint.Check(Libendpoint.UiBorderSetCornerRadius(this.Native, value)); }
    }
    /// <summary>Inner padding in pixels between the border and child widgets. Getter returns the last value set (LVGL has no read-back).</summary>
    public int Padding {
        get => this._padding;
        set { this._padding = value; Libendpoint.Check(Libendpoint.UiBorderSetPadding(this.Native, value)); }
    }
}
