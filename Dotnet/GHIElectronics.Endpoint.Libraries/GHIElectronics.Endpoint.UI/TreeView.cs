using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Hierarchical list. Nodes are added imperatively via <see cref="AddNode"/>
/// with a depth value — codegen walks the tree and emits one AddNode call
/// per visible node.
/// </summary>
public class TreeView : Widget {
    public TreeView(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiTreeViewCreate(parent.Native, out var n));
        this.Native = n;
    }

    public void AddNode(int depth, bool isExpanded, bool hasChildren, string header) {
        Libendpoint.Check(Libendpoint.UiTreeViewAddNode(
            this.Native, depth, isExpanded ? 1 : 0, hasChildren ? 1 : 0,
            header ?? string.Empty, out _));
    }

    public void Clear() => Libendpoint.Check(Libendpoint.UiTreeViewClear(this.Native));
}
