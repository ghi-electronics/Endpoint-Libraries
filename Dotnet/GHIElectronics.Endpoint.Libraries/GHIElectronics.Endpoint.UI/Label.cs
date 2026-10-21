using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public class Label : Widget {
    public Label(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiLabelCreate(parent.Native, out var n));
        this.Native = n;
    }

    public string Text { set => Libendpoint.Check(Libendpoint.UiLabelSetText(this.Native, value)); }
    public uint Color { set => Libendpoint.Check(Libendpoint.UiLabelSetColor(this.Native, value)); }
    public int FontSize { set => Libendpoint.Check(Libendpoint.UiLabelSetFontSize(this.Native, value)); }
}
