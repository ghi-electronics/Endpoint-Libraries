using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// WPF-style Grid with ColumnDefinitions + RowDefinitions. Call
/// <see cref="SetDefinitions"/> once with the column + row sizes, then
/// call <see cref="SetCell(Widget, int, int, int, int)"/> on each child.
/// Sizes: positive = fixed pixels; 0 or negative = star (-1 = "*",
/// -2 = "2*", ...).
/// </summary>
public class Grid : Widget {
    /// <summary>Create a grid inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public Grid(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiGridCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>
    /// Define column and row sizes. Positive values are fixed pixel widths;
    /// a negative value <c>-n</c> is a star share of weight <c>n</c>
    /// (<c>-1</c> = <c>*</c>, <c>-2</c> = <c>2*</c>, ...). Zero is treated as star.
    /// </summary>
    /// <param name="columns">Column size definitions, left to right.</param>
    /// <param name="rows">Row size definitions, top to bottom.</param>
    public unsafe void SetDefinitions(int[] columns, int[] rows) {
        fixed (int* c = columns)
        fixed (int* r = rows) {
            Libendpoint.Check(Libendpoint.UiGridSetDefinitions(
                this.Native, c, columns?.Length ?? 0, r, rows?.Length ?? 0));
        }
    }

    /// <summary>Attach <paramref name="child"/> to a cell in the grid.</summary>
    public static void SetCell(Widget child, int column, int row, int columnSpan = 1, int rowSpan = 1) {
        Libendpoint.Check(Libendpoint.UiGridSetCell(child.Native, column, columnSpan, row, rowSpan));
    }
}
