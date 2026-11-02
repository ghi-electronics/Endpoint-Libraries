using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Modal dialog with title, body text, and footer buttons. Add buttons
/// with <see cref="AddButton"/>; each returns a handle whose Clicked
/// event you subscribe to. Call <see cref="Hide"/> to dismiss.
/// </summary>
public class MessageBox : Widget {
    /// <summary>Create a message box inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget (typically the active <see cref="Screen"/>).</param>
    public MessageBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiMessageBoxCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Set the dialog title text.</summary>
    /// <param name="title">Title string.</param>
    public void SetTitle(string title) => Libendpoint.Check(Libendpoint.UiMessageBoxSetTitle(this.Native, title));
    /// <summary>Set the dialog body text.</summary>
    /// <param name="body">Body string.</param>
    public void SetBody(string body)   => Libendpoint.Check(Libendpoint.UiMessageBoxSetBody(this.Native, body));

    /// <summary>Add a footer button; returns a handle to subscribe to its Clicked event.</summary>
    public MessageBoxButton AddButton(string text) {
        Libendpoint.Check(Libendpoint.UiMessageBoxAddButton(this.Native, text, out var btn));
        return new MessageBoxButton(btn, this);
    }

    /// <summary>Dismiss the dialog without destroying it; call again by making the <see cref="Widget.Visible"/> parent re-show it.</summary>
    public void Hide() => Libendpoint.Check(Libendpoint.UiMessageBoxHide(this.Native));
}

/// <summary>A button inside a MessageBox. Subscribe to Clicked.</summary>
public class MessageBoxButton {
    internal nint Native;
    private readonly MessageBox _owner;
    private EventHandler? _clicked;
    private GCHandle _clickedHandle;

    internal unsafe MessageBoxButton(nint native, MessageBox owner) {
        this.Native = native;
        this._owner = owner;

        this._clickedHandle = EventTrampolines.Pin(
            () => this._clicked?.Invoke(this, EventArgs.Empty));
        Libendpoint.Check(Libendpoint.UiSubscribeClicked(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._clickedHandle)));
    }

    /// <summary>Fires when the footer button is tapped. Runs on the UI thread.</summary>
    public event EventHandler Clicked {
        add => this._clicked += value;
        remove => this._clicked -= value;
    }
}
