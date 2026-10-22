using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Container with WPF-style <see cref="Thickness"/>, <see cref="BorderBrush"/>,
/// <see cref="CornerRadius"/>, and <see cref="Padding"/>. Children are
/// positioned relative to this border, same as <see cref="Container"/>.
/// Background is set via the inherited <see cref="Widget.BackgroundColor"/>.
/// </summary>
public class Border : Widget {
    public Border(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiBorderCreate(parent.Native, out var n));
        this.Native = n;
    }

    public int Thickness    { set => Libendpoint.Check(Libendpoint.UiBorderSetThickness(this.Native, value)); }
    public uint BorderBrush { set => Libendpoint.Check(Libendpoint.UiBorderSetBrush(this.Native, value)); }
    public int CornerRadius { set => Libendpoint.Check(Libendpoint.UiBorderSetCornerRadius(this.Native, value)); }
    public int Padding      { set => Libendpoint.Check(Libendpoint.UiBorderSetPadding(this.Native, value)); }
}
