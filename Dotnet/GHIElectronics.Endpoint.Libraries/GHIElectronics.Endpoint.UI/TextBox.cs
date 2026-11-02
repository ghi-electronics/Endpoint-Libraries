using System.Runtime.InteropServices;
using System.Text;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Single-line editable text input. Pair with a <see cref="Keyboard"/> for on-screen typing.</summary>
public class TextBox : Widget {
    private EventHandler? _valueChanged;
    private GCHandle _valueChangedHandle;

    /// <summary>Create a text box inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public unsafe TextBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiTextBoxCreate(parent.Native, out var n));
        this.Native = n;

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        this.TrackEventHandle(this._valueChangedHandle);
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    /// <summary>Current text content. Reads back live from LVGL (up to 255 UTF-8 bytes).</summary>
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

    private string _placeholder = string.Empty;
    /// <summary>Hint text shown when <see cref="Text"/> is empty. <c>null</c> is treated as empty. Getter returns the last value set.</summary>
    public string Placeholder {
        get => this._placeholder;
        set { this._placeholder = value ?? string.Empty; Libendpoint.Check(Libendpoint.UiTextBoxSetPlaceholder(this.Native, this._placeholder)); }
    }

    /// <summary>Fires on every character insertion or deletion. Runs on the UI thread.</summary>
    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }
}
