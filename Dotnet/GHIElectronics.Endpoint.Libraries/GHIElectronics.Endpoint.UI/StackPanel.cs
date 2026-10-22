using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public enum Orientation { Vertical = 0, Horizontal = 1 }

/// <summary>
/// WPF-style StackPanel: stacks children vertically (default) or
/// horizontally via LVGL flex layout. <see cref="Spacing"/> is the gap
/// between children; each child's <see cref="Widget.SetMargin"/> stacks
/// on top of that.
/// </summary>
public class StackPanel : Widget {
    public StackPanel(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiStackPanelCreate(parent.Native, out var n));
        this.Native = n;
    }

    public Orientation Orientation {
        set => Libendpoint.Check(Libendpoint.UiStackPanelSetOrientation(this.Native, (int)value));
    }

    public int Spacing {
        set => Libendpoint.Check(Libendpoint.UiStackPanelSetSpacing(this.Native, value));
    }
}
