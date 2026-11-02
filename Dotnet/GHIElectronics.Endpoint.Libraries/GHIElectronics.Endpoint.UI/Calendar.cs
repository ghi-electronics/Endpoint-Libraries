using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>Month calendar.</summary>
public class Calendar : Widget {
    /// <summary>Create a calendar inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Calendar(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiCalendarCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Set the month shown by the calendar.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month, 1 (January) to 12 (December).</param>
    public void SetShowedDate(int year, int month) =>
        Libendpoint.Check(Libendpoint.UiCalendarSetShowedDate(this.Native, year, month));

    /// <summary>Set the day that is highlighted as "today".</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month, 1-12.</param>
    /// <param name="day">Day of month, 1-31.</param>
    public void SetToday(int year, int month, int day) =>
        Libendpoint.Check(Libendpoint.UiCalendarSetToday(this.Native, year, month, day));
}
