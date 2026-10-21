using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Virtual on-screen keyboard. Attach to a <see cref="TextBox"/> via
/// <see cref="SetTarget"/> so taps on keys type into it. Positions itself
/// at the bottom half of the display by default; override via inherited
/// X / Y / Width / Height.
/// </summary>
public class Keyboard : Widget {
    public Keyboard(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiKeyboardCreate(parent.Native, out var n));
        this.Native = n;
    }

    public void SetTarget(TextBox textbox) =>
        Libendpoint.Check(Libendpoint.UiKeyboardSetTextBox(this.Native, textbox.Native));

    public void ClearTarget() =>
        Libendpoint.Check(Libendpoint.UiKeyboardSetTextBox(this.Native, 0));
}
