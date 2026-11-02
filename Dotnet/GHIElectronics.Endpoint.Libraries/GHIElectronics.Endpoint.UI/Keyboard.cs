using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Virtual on-screen keyboard. Attach to a <see cref="TextBox"/> via
/// <see cref="SetTarget"/> so taps on keys type into it. Positions itself
/// at the bottom half of the display by default; override via inherited
/// X / Y / Width / Height.
/// </summary>
public class Keyboard : Widget {
    /// <summary>Create a virtual keyboard inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Keyboard(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiKeyboardCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Route key presses into <paramref name="textbox"/>.</summary>
    /// <param name="textbox">Target text box; its text updates on each key.</param>
    public void SetTarget(TextBox textbox) =>
        Libendpoint.Check(Libendpoint.UiKeyboardSetTextBox(this.Native, textbox.Native));

    /// <summary>Detach any current target so key presses go nowhere.</summary>
    public void ClearTarget() =>
        Libendpoint.Check(Libendpoint.UiKeyboardSetTextBox(this.Native, 0));
}
