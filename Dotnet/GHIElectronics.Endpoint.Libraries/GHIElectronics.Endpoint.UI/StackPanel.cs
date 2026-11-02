using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Direction in which <see cref="StackPanel"/> arranges its children.</summary>
public enum Orientation {
    /// <summary>Stack children top to bottom.</summary>
    Vertical = 0,
    /// <summary>Stack children left to right.</summary>
    Horizontal = 1
}

/// <summary>
/// WPF-style StackPanel: stacks children vertically (default) or
/// horizontally via LVGL flex layout. <see cref="Spacing"/> is the gap
/// between children; each child's <see cref="Widget.SetMargin"/> stacks
/// on top of that.
/// </summary>
public class StackPanel : Widget {
    /// <summary>Create a stack panel inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public StackPanel(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiStackPanelCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Direction in which children are arranged. Set-only; LVGL has no read-back.</summary>
    public Orientation Orientation {
        set => Libendpoint.Check(Libendpoint.UiStackPanelSetOrientation(this.Native, (int)value));
    }

    /// <summary>Gap in pixels between successive children. Set-only; LVGL has no read-back.</summary>
    public int Spacing {
        set => Libendpoint.Check(Libendpoint.UiStackPanelSetSpacing(this.Native, value));
    }
}
