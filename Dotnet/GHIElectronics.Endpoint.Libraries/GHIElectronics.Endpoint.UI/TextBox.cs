using System.Runtime.InteropServices;
using System.Text;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public class TextBox : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    public unsafe TextBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiTextBoxCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    public string Text {
        get {
            Span<byte> buf = stackalloc byte[256];
            unsafe {
                fixed (byte* p = buf) {
                    Libendpoint.Check(Libendpoint.UiTextBoxGetText(this.Native, p, buf.Length));
                }
            }
            var end = buf.IndexOf((byte)0);
            return Encoding.UTF8.GetString(buf[..(end < 0 ? buf.Length : end)]);
        }
        set => Libendpoint.Check(Libendpoint.UiTextBoxSetText(this.Native, value));
    }

    public string Placeholder { set => Libendpoint.Check(Libendpoint.UiTextBoxSetPlaceholder(this.Native, value)); }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
