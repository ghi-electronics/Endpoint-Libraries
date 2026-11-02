using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Vertical scrollable list. <see cref="AddItem"/> returns a handle whose
/// Clicked event you subscribe to.
/// </summary>
public class ListBox : Widget {
    /// <summary>Create a list box inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public ListBox(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiListBoxCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Append an item.</summary>
    /// <param name="text">Row text.</param>
    /// <returns>Handle for subscribing to the row's <see cref="ListBoxItem.Clicked"/> event.</returns>
    public ListBoxItem AddItem(string text) {
        Libendpoint.Check(Libendpoint.UiListBoxAddItem(this.Native, text, out var item));
        return new ListBoxItem(item);
    }

    /// <summary>Remove every item.</summary>
    public void Clear() => Libendpoint.Check(Libendpoint.UiListBoxClear(this.Native));
}

/// <summary>An item inside a ListBox. Subscribe to Clicked.</summary>
public class ListBoxItem {
    internal nint Native;
    private EventHandler? _clicked;
    private GCHandle _clickedHandle;

    internal unsafe ListBoxItem(nint native) {
        this.Native = native;

        this._clickedHandle = EventTrampolines.Pin(
            () => this._clicked?.Invoke(this, EventArgs.Empty));
        Libendpoint.Check(Libendpoint.UiSubscribeClicked(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._clickedHandle)));
    }

    /// <summary>Fires when this list item is tapped. Runs on the UI thread.</summary>
    public event EventHandler Clicked {
        add => this._clicked += value;
        remove => this._clicked -= value;
    }
}
