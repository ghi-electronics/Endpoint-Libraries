using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public class Button : Widget {
    private EventHandler? _clicked;
    private GCHandle _clickedHandle;

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

    public string Text { set => Libendpoint.Check(Libendpoint.UiButtonSetText(this.Native, value)); }
    public uint BackgroundColor { set => Libendpoint.Check(Libendpoint.UiButtonSetBgColor(this.Native, value)); }
    public uint TextColor { set => Libendpoint.Check(Libendpoint.UiButtonSetTextColor(this.Native, value)); }
    public int FontSize { set => Libendpoint.Check(Libendpoint.UiButtonSetFontSize(this.Native, value)); }

    public event EventHandler Clicked {
        add => this._clicked += value;
        remove => this._clicked -= value;
    }
}
