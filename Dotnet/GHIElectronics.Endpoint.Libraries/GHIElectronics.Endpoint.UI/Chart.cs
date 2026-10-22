using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

public enum ChartMode { Line = 0, Rectangle = 1, Area = 2 }

/// <summary>
/// Simple chart with a single series. Not a standard .NET widget — the API
/// mirrors TinyCLR's Chart for ease of porting.
/// </summary>
public class Chart : Widget {
    public Chart(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiChartCreate(parent.Native, out var n));
        this.Native = n;
    }

    public ChartMode Mode { set => Libendpoint.Check(Libendpoint.UiChartSetMode(this.Native, (int)value)); }

    public void SetColors(uint pointColor, uint areaColor, uint labelColor) =>
        Libendpoint.Check(Libendpoint.UiChartSetColors(this.Native, pointColor, areaColor, labelColor));

    public int DivisionAxisY { set => Libendpoint.Check(Libendpoint.UiChartSetDivision(this.Native, value)); }

    public unsafe void SetPoints(int[] values) {
        fixed (int* p = values) {
            Libendpoint.Check(Libendpoint.UiChartSetPoints(this.Native, p, values?.Length ?? 0));
        }
    }
}
