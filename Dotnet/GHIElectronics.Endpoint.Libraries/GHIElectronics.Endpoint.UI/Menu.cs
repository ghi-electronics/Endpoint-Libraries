using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Horizontal menu bar. <see cref="AddItem"/> returns a leaf-item handle;
/// <see cref="AddDropdown"/> returns a dropdown handle whose entries are
/// listed on click.
/// </summary>
public class Menu : Widget {
    public Menu(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiMenuCreate(parent.Native, out var n));
        this.Native = n;
    }

    public uint Background { set => Libendpoint.Check(Libendpoint.UiMenuSetBackground(this.Native, value)); }

    public MenuItem AddItem(string header, int itemWidth = 0) {
        Libendpoint.Check(Libendpoint.UiMenuItemCreate(this.Native, header, itemWidth, out var h));
        return new MenuItem(h);
    }

    public MenuItem AddDropdown(string header, string[] subItems, int itemWidth = 0) {
        var joined = subItems is null ? string.Empty : string.Join("\n", subItems);
        Libendpoint.Check(Libendpoint.UiMenuItemCreateDropdown(this.Native, header, joined, itemWidth, out var h));
        return new MenuItem(h);
    }
}

/// <summary>
/// Handle to a menu item. For a leaf item (<see cref="Menu.AddItem"/>)
/// subscribe to <see cref="Clicked"/>. For a dropdown parent
/// (<see cref="Menu.AddDropdown"/>) subscribe to <see cref="ValueChanged"/>
/// and read <see cref="Selected"/> to find out which sub-item the user picked.
/// </summary>
public class MenuItem {
    internal nint Native;
    private EventHandler? _clicked;
    private EventHandler? _valueChanged;
    private GCHandle _clickedHandle;
    private GCHandle _valueChangedHandle;

    internal unsafe MenuItem(nint native) {
        this.Native = native;
        this._clickedHandle = EventTrampolines.Pin(
            () => this._clicked?.Invoke(this, EventArgs.Empty));
        Libendpoint.Check(Libendpoint.UiSubscribeClicked(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._clickedHandle)));

        this._valueChangedHandle = EventTrampolines.Pin(
            () => this._valueChanged?.Invoke(this, EventArgs.Empty));
        Libendpoint.Check(Libendpoint.UiSubscribeValueChanged(
            this.Native, EventTrampolines.Pointer(), GCHandle.ToIntPtr(this._valueChangedHandle)));
    }

    public event EventHandler Clicked {
        add => this._clicked += value;
        remove => this._clicked -= value;
    }

    public event EventHandler ValueChanged {
        add => this._valueChanged += value;
        remove => this._valueChanged -= value;
    }

    /// <summary>
    /// For dropdown items: 0-based index of the sub-item the user picked.
    /// Returns -1 if this is a leaf item (no sub-items).
    /// </summary>
    public int Selected {
        get {
            Libendpoint.Check(Libendpoint.UiDropdownGetSelected(this.Native, out var v));
            return v;
        }
    }
}
