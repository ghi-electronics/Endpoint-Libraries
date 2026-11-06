using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Hierarchical list. Nodes are added imperatively via <see cref="AddNode"/>
/// with a depth value; codegen walks the tree and emits one AddNode call
/// per visible node.
/// </summary>
public class TreeView : Widget {
    /// <summary>Create a tree view inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public TreeView(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiTreeViewCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Append a visible node at the given indent level.</summary>
    /// <param name="depth">Indent depth, 0-based (0 = root level).</param>
    /// <param name="isExpanded">Whether this node currently shows its children (visual only; the caller feeds the children themselves).</param>
    /// <param name="hasChildren">Whether to draw an expand/collapse chevron.</param>
    /// <param name="header">Row text; <c>null</c> is treated as empty.</param>
    public void AddNode(int depth, bool isExpanded, bool hasChildren, string header) {
        Libendpoint.Check(Libendpoint.UiTreeViewAddNode(
            this.Native, depth, isExpanded ? 1 : 0, hasChildren ? 1 : 0,
            header ?? string.Empty, out _));
    }

    /// <summary>Remove every node.</summary>
    public void Clear() => Libendpoint.Check(Libendpoint.UiTreeViewClear(this.Native));
}
