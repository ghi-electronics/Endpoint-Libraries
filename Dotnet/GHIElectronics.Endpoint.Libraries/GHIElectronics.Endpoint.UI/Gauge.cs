using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Radial dial with a needle. Not a standard .NET widget; the API
/// mirrors TinyCLR's Gauge. SweepAngle is 180/270/360 degrees.
/// </summary>
public class Gauge : Widget {
    /// <summary>Create a gauge inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public Gauge(Widget parent, int width, int height) : base(0) {
        Libendpoint.Check(Libendpoint.UiGaugeCreate(parent.Native, width, height, out var n));
        this.Native = n;
    }

    /// <summary>Set the value range mapped onto the dial.</summary>
    /// <param name="min">Value shown at the start of the sweep.</param>
    /// <param name="max">Value shown at the end of the sweep.</param>
    public void SetRange(int min, int max) =>
        Libendpoint.Check(Libendpoint.UiGaugeSetRange(this.Native, min, max));

    private int _value;
    private int _sweepAngle;
    private bool _enableScale;

    /// <summary>Current needle value. Getter returns the last value set (LVGL has no read-back).</summary>
    public int Value {
        get => this._value;
        set { this._value = value; Libendpoint.Check(Libendpoint.UiGaugeSetValue(this.Native, value)); }
    }

    /// <summary>Total sweep in degrees (typically 180, 270, or 360). Getter returns the last value set.</summary>
    public int SweepAngle {
        get => this._sweepAngle;
        set { this._sweepAngle = value; Libendpoint.Check(Libendpoint.UiGaugeSetSweep(this.Native, value)); }
    }

    /// <summary>Needle center-Y (0..1 of height) and length (0..1 of half min dimension).</summary>
    public void SetNeedleShape(double centerY, double length) =>
        Libendpoint.Check(Libendpoint.UiGaugeSetNeedleShape(this.Native,
            (int)(centerY * 1000), (int)(length * 1000)));

    /// <summary>Whether tick marks and labels are drawn around the dial. Getter returns the last value set.</summary>
    public bool EnableScale {
        get => this._enableScale;
        set { this._enableScale = value; Libendpoint.Check(Libendpoint.UiGaugeShowScale(this.Native, value ? 1 : 0)); }
    }
}
