using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Clickable push button with text, color, and font-size properties.</summary>
public class Button : Widget {
    private EventHandler? _clicked;
    private GCHandle _clickedHandle;

    /// <summary>Create a button inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public unsafe Button(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiButtonCreate(parent.Native, out var n));
        this.Native = n;

        // Register the native callback once at construction; subscribers to
        // the C# event are added/removed in managed code only.
        this._clickedHandle = EventTrampolines.Pin(() => this._clicked?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._clickedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeClicked(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._clickedHandle)));
    }

    private string _text = string.Empty;
    private uint _backgroundColor;
    private uint _textColor;
    private int _fontSize;

    /// <summary>Button caption. <c>null</c> is treated as empty. Getter returns the last value set.</summary>
    public string Text {
        get => this._text;
        set { this._text = value ?? string.Empty; Libendpoint.Check(Libendpoint.UiButtonSetText(this.Native, this._text)); }
    }
    /// <summary>Button background color as <c>0x00RRGGBB</c>. Getter returns the last value set.</summary>
    public override uint BackgroundColor {
        get => this._backgroundColor;
        set { this._backgroundColor = value; Libendpoint.Check(Libendpoint.UiButtonSetBgColor(this.Native, value)); }
    }
    /// <summary>Caption color as <c>0x00RRGGBB</c>. Getter returns the last value set.</summary>
    public uint TextColor {
        get => this._textColor;
        set { this._textColor = value; Libendpoint.Check(Libendpoint.UiButtonSetTextColor(this.Native, value)); }
    }
    /// <summary>Caption font size in pixels. Getter returns the last value set.</summary>
    public int FontSize {
        get => this._fontSize;
        set { this._fontSize = value; Libendpoint.Check(Libendpoint.UiButtonSetFontSize(this.Native, value)); }
    }

    /// <summary>Fires when the button is clicked (press-and-release inside its bounds). Runs on the UI thread.</summary>
    public event EventHandler Clicked {
        add => this._clicked += value;
        remove => this._clicked -= value;
    }
}
