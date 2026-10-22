using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public enum TextAlignment { Left = 0, Center = 1, Right = 2 }

public class Label : Widget {
    public Label(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiLabelCreate(parent.Native, out var n));
        this.Native = n;
    }

    public string Text { set => Libendpoint.Check(Libendpoint.UiLabelSetText(this.Native, value)); }
    public uint Color { set => Libendpoint.Check(Libendpoint.UiLabelSetColor(this.Native, value)); }
    public int FontSize { set => Libendpoint.Check(Libendpoint.UiLabelSetFontSize(this.Native, value)); }
    public TextAlignment TextAlignment { set => Libendpoint.Check(Libendpoint.UiLabelSetTextAlign(this.Native, (int)value)); }
}
