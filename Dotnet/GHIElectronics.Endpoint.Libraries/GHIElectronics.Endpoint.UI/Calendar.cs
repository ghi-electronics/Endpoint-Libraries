using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Month calendar.</summary>
public class Calendar : Widget {
    public Calendar(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiCalendarCreate(parent.Native, out var n));
        this.Native = n;
    }

    public void SetShowedDate(int year, int month) =>
        Libendpoint.Check(Libendpoint.UiCalendarSetShowedDate(this.Native, year, month));

    public void SetToday(int year, int month, int day) =>
        Libendpoint.Check(Libendpoint.UiCalendarSetToday(this.Native, year, month, day));
}
