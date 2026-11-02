using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Horizontal alignment of text within its widget.</summary>
public enum TextAlignment {
    /// <summary>Align to the left edge.</summary>
    Left = 0,
    /// <summary>Center horizontally.</summary>
    Center = 1,
    /// <summary>Align to the right edge.</summary>
    Right = 2
}

/// <summary>Static text display. For an interactive text input, use <see cref="TextBox"/>.</summary>
public class Label : Widget {
    /// <summary>Create a label inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Label(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiLabelCreate(parent.Native, out var n));
        this.Native = n;
    }

    private string _text = string.Empty;
    private uint _color;
    private int _fontSize;
    private TextAlignment _textAlignment;

    /// <summary>Displayed text. <c>null</c> is treated as empty. Getter returns the last value set.</summary>
    public string Text {
        get => this._text;
        set { this._text = value ?? string.Empty; Libendpoint.Check(Libendpoint.UiLabelSetText(this.Native, this._text)); }
    }
    /// <summary>Text color as <c>0x00RRGGBB</c>. Getter returns the last value set.</summary>
    public uint Color {
        get => this._color;
        set { this._color = value; Libendpoint.Check(Libendpoint.UiLabelSetColor(this.Native, value)); }
    }
    /// <summary>Font size in pixels. Getter returns the last value set.</summary>
    public int FontSize {
        get => this._fontSize;
        set { this._fontSize = value; Libendpoint.Check(Libendpoint.UiLabelSetFontSize(this.Native, value)); }
    }
    /// <summary>Horizontal alignment of the text inside the label bounds. Getter returns the last value set.</summary>
    public TextAlignment TextAlignment {
        get => this._textAlignment;
        set { this._textAlignment = value; Libendpoint.Check(Libendpoint.UiLabelSetTextAlign(this.Native, (int)value)); }
    }
}
