using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Simple grid of text cells (standard WPF widget name; TinyCLR-shape API).
/// Call <see cref="SetSize"/> to allocate rows and columns, then
/// <see cref="SetCell"/> to fill each cell.
/// </summary>
public class DataGrid : Widget {
    /// <summary>Create a data grid inside <paramref name="parent"/>.</summary>
    /// <param name="parent">Containing widget.</param>
    public DataGrid(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiDataGridCreate(parent.Native, out var n));
        this.Native = n;
    }

    /// <summary>Allocate the grid dimensions. Must be called before <see cref="SetCell"/>.</summary>
    /// <param name="columns">Number of columns.</param>
    /// <param name="rows">Number of rows.</param>
    public void SetSize(int columns, int rows) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetSize(this.Native, columns, rows));

    /// <summary>Set the text in a single cell.</summary>
    /// <param name="row">0-based row index.</param>
    /// <param name="column">0-based column index.</param>
    /// <param name="text">Cell contents; <c>null</c> is treated as empty.</param>
    public void SetCell(int row, int column, string text) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetCell(this.Native, row, column, text ?? string.Empty));

    /// <summary>Set a single column's width in pixels.</summary>
    /// <param name="column">0-based column index.</param>
    /// <param name="px">Width in pixels.</param>
    public void SetColumnWidth(int column, int px) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetColumnWidth(this.Native, column, px));
}
