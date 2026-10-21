using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Groups child widgets. Children are positioned relative to the container.</summary>
public class Container : Widget {
    public Container(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiContainerCreate(parent.Native, out var n));
        this.Native = n;
    }
}
