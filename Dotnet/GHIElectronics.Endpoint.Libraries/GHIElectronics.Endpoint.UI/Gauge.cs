using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Radial dial with a needle. Not a standard .NET widget — the API
/// mirrors TinyCLR's Gauge. SweepAngle is 180/270/360 degrees.
/// </summary>
public class Gauge : Widget {
    public Gauge(Widget parent, int width, int height) : base(0) {
        Libendpoint.Check(Libendpoint.UiGaugeCreate(parent.Native, width, height, out var n));
        this.Native = n;
    }

    public void SetRange(int min, int max) =>
        Libendpoint.Check(Libendpoint.UiGaugeSetRange(this.Native, min, max));

    public int Value { set => Libendpoint.Check(Libendpoint.UiGaugeSetValue(this.Native, value)); }

    public int SweepAngle { set => Libendpoint.Check(Libendpoint.UiGaugeSetSweep(this.Native, value)); }

    /// <summary>Needle center-Y (0..1 of height) and length (0..1 of half min dimension).</summary>
    public void SetNeedleShape(double centerY, double length) =>
        Libendpoint.Check(Libendpoint.UiGaugeSetNeedleShape(this.Native,
            (int)(centerY * 1000), (int)(length * 1000)));

    public bool EnableScale {
        set => Libendpoint.Check(Libendpoint.UiGaugeShowScale(this.Native, value ? 1 : 0));
    }
}
