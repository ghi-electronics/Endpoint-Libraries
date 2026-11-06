using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Visual style used by <see cref="Chart"/>.</summary>
public enum ChartMode {
    /// <summary>Line between successive points.</summary>
    Line = 0,
    /// <summary>Vertical bars (column) per point.</summary>
    Rectangle = 1,
    /// <summary>Filled area under the line.</summary>
    Area = 2
}

/// <summary>
/// Simple chart with a single series. Not a standard .NET widget; the API
/// mirrors TinyCLR's Chart for ease of porting.
/// </summary>
public class Chart : Widget {
    /// <summary>Create a chart inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Chart(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiChartCreate(parent.Native, out var n));
        this.Native = n;
    }

    private ChartMode _mode;
    private int _divisionAxisY;

    /// <summary>How the series is drawn. Getter returns the last value set (LVGL has no read-back).</summary>
    public ChartMode Mode {
        get => this._mode;
        set { this._mode = value; Libendpoint.Check(Libendpoint.UiChartSetMode(this.Native, (int)value)); }
    }

    /// <summary>Set the chart's drawing colors.</summary>
    /// <param name="pointColor">Line / column color as <c>0x00RRGGBB</c>.</param>
    /// <param name="areaColor">Fill color under the line as <c>0x00RRGGBB</c> (used when <see cref="Mode"/> is <see cref="ChartMode.Area"/>).</param>
    /// <param name="labelColor">Axis label color as <c>0x00RRGGBB</c>.</param>
    public void SetColors(uint pointColor, uint areaColor, uint labelColor) =>
        Libendpoint.Check(Libendpoint.UiChartSetColors(this.Native, pointColor, areaColor, labelColor));

    /// <summary>Number of horizontal division lines on the Y axis. Getter returns the last value set.</summary>
    public int DivisionAxisY {
        get => this._divisionAxisY;
        set { this._divisionAxisY = value; Libendpoint.Check(Libendpoint.UiChartSetDivision(this.Native, value)); }
    }

    /// <summary>Replace the full series with <paramref name="values"/>. The chart redraws in order left-to-right.</summary>
    /// <param name="values">Point values; may be <c>null</c> or empty to clear.</param>
    public unsafe void SetPoints(int[] values) {
        fixed (int* p = values) {
            Libendpoint.Check(Libendpoint.UiChartSetPoints(this.Native, p, values?.Length ?? 0));
        }
    }
}
