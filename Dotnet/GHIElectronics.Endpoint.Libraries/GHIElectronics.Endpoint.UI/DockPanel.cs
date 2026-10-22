using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public enum Dock { Top = 0, Bottom = 1, Left = 2, Right = 3 }

/// <summary>
/// WPF-style DockPanel. LVGL has no dock layout; the caller (or codegen)
/// computes each child's absolute position from its Dock side plus the
/// remaining area, then uses the child's X/Y/Width/Height properties.
/// </summary>
public class DockPanel : Widget {
    public DockPanel(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiDockPanelCreate(parent.Native, out var n));
        this.Native = n;
    }

    public bool LastChildFill { get; set; } = true;
}
