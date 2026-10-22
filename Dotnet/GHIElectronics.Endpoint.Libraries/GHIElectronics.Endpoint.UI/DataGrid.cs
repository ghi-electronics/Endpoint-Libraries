using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI;

/// <summary>
/// Simple grid of text cells (standard WPF widget name; TinyCLR-shape API).
/// Call <see cref="SetSize"/> to allocate rows and columns, then
/// <see cref="SetCell"/> to fill each cell.
/// </summary>
public class DataGrid : Widget {
    public DataGrid(Widget parent) : base(0) {
        Libendpoint.Check(Libendpoint.UiDataGridCreate(parent.Native, out var n));
        this.Native = n;
    }

    public void SetSize(int columns, int rows) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetSize(this.Native, columns, rows));

    public void SetCell(int row, int column, string text) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetCell(this.Native, row, column, text ?? string.Empty));

    public void SetColumnWidth(int column, int px) =>
        Libendpoint.Check(Libendpoint.UiDataGridSetColumnWidth(this.Native, column, px));
}
