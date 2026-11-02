using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Edge a child docks to within a <see cref="DockPanel"/>.</summary>
public enum Dock {
    /// <summary>Dock to the top edge.</summary>
    Top = 0,
    /// <summary>Dock to the bottom edge.</summary>
    Bottom = 1,
    /// <summary>Dock to the left edge.</summary>
    Left = 2,
    /// <summary>Dock to the right edge.</summary>
    Right = 3
}

/// <summary>
/// WPF-style DockPanel. LVGL has no dock layout; the caller (or codegen)
/// computes each child's absolute position from its Dock side plus the
/// remaining area, then uses the child's X/Y/Width/Height properties.
/// </summary>
public class DockPanel : Widget {
    /// <summary>Create a dock panel inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public DockPanel(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiDockPanelCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>When <c>true</c>, the last added child fills the remaining area. Consumed by codegen, not LVGL.</summary>
    public bool LastChildFill { get; set; } = true;
}
