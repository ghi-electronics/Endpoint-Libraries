////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Copyright (c) GHI Electronics, LLC.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using GHIElectronics.Endpoint.Drawing;
using System.Text;
using System.Threading;
using GHIElectronics.Endpoint.UI;
using GHIElectronics.Endpoint.UI.Controls;
using GHIElectronics.Endpoint.UI.Input;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI.Media.Imaging;
using GHIElectronics.Endpoint.UI.Properties;

namespace GHIElectronics.Endpoint.UI.Controls {
    /// <summary>
    /// The DataGrid component is a list-based component that provides a grid of rows and columns.
    /// </summary>
    public class DataGrid : ContentControl {

        /// <summary>
        /// Tap cell event arguments.
        /// </summary>
        public class TapCellEventArgs {
            /// <summary>
            /// Column index.
            /// </summary>
            public int ColumnIndex { get; }

            /// <summary>
            /// Row index.
            /// </summary>
            public int RowIndex { get; }

            /// <summary>
            /// Creates a new TapCellEventArgs.
            /// </summary>
            /// <param name="columnIndex">X coordinate</param>
            /// <param name="rowIndex">Y coordinate</param>
            public TapCellEventArgs(int columnIndex, int rowIndex) {
                this.ColumnIndex = columnIndex;
                this.RowIndex = rowIndex;
            }

            /// <summary>
            /// ToString
            /// </summary>
            /// <returns>Tap cell event properties.</returns>
            public override string ToString() => "{ ColumnIndex: " + this.ColumnIndex + ", RowIndex: " + this.RowIndex + " }";
        }

        /// <summary>
        /// Tap cell event handler.
        /// </summary>
        /// <param name="sender">Object associated with this event.</param>
        /// <param name="args">Tap cell event arguments.</param>
        public delegate void OnTapCell(object sender, TapCellEventArgs args);


        private ArrayList columns_ = new ArrayList();
        private ArrayList rows_ = new ArrayList();

        private BitmapImage headers;
        private BitmapImage items;

        //this.dropdownTextUp = BitmapImage.FromGraphics(Graphics.FromImage(Resources.GetBitmap(Resources.BitmapResources.DropdownText_Up)));
        private GHIElectronics.Endpoint.Drawing.Bitmap dataGridIcon_Asc;// = BitmapImage.FromGraphics(Graphics.FromImage(Resources.DataGridIcon_Asc));
        private GHIElectronics.Endpoint.Drawing.Bitmap dataGridIcon_Desc;//= BitmapImage.FromGraphics(Graphics.FromImage(Resources.DataGridIcon_Desc));

        private bool renderHeaders;
        private bool renderItems;
        private int scrollIndex;
        private bool pressed = false;
        private bool moving = false;
        private int selectedIndex = -1;
        private DataGridItem selectedItem;
        private int selectedDataGridColumnIndex;

        private int listY;
        private int listMaxY;
        private int lastListY;
        private int lastTouchY;
        private int ignoredTouchMoves;

        private bool showHeaders;
        private int rowCount;
        private int scrollbarHeight;
        private int scrollbarTick;
        private DataGridItemComparer comparer = new DataGridItemComparer();
        private Order order;

        private int posX;
        private int posY;
        private bool disposed;

        /// <summary>
        /// Creates a new DataGrid component.
        /// </summary>      
        /// <param name="width">width</param>
        /// <param name="rowHeight">rowHeight</param>
        /// <param name="rowCount">rowCount</param>
        /// <param name="font">font</param>
        public DataGrid(int width, int rowHeight, int rowCount, Font font) {
            this.Width = width;
            this.RowHeight = rowHeight;
            // Setting the RowCount changes the Height.
            this.RowCount = rowCount;

            // Show headers is on by default.
            // When the headers are shown the row count is decreased by one.
            this.ShowHeaders = true;

            this.headers = BitmapImage.FromGraphics(new Graphics(this.Width, this.RowHeight));
            this.Font = font;

            this.dataGridIcon_Asc = new Drawing.Bitmap(Resources.DataGridIcon_Asc, 10, 5);
            this.dataGridIcon_Desc = new Drawing.Bitmap(Resources.DataGridIcon_Desc, 10, 5);

            this.Clear();
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!this.disposed) {

                this.dataGridIcon_Asc.Dispose();
                this.dataGridIcon_Desc.Dispose();

                this.disposed = true;
            }
        }

        ~DataGrid() {
            this.Dispose(false);
        }

        // Events

        /// <summary>
        /// Tap grid event.
        /// </summary>
        public event OnTapCell TapCellEvent;

        /// <summary>
        /// Triggers a tap cell event.
        /// </summary>
        /// <param name="sender">Object associated with this event.</param>
        /// <param name="args">Tap cell event arguments.</param>
        public void TriggerTapCellEvent(object sender, TapCellEventArgs args) => TapCellEvent?.Invoke(sender, args);

        private void RenderHeaders() {
            var x = 0;

            using (var headersBackColorBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.HeadersBackColor.R, this.HeadersBackColor.G, this.HeadersBackColor.B))) {

                this.headers.graphics.FillRectangle(headersBackColorBrush, x, 0, this.Width, this.headers.Height);
            }

            DataGridColumn dataGridColumn;
            for (var j = 0; j < this.columns_.Count; j++) {
                dataGridColumn = (DataGridColumn)this.columns_[j];

                // Draw text
                this.headers.graphics.DrawTextInRect(dataGridColumn.label, x + 5, (this.headers.Height - this.Font.Height) / 2, dataGridColumn.width, this.headers.Height, 0, GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.HeadersFontColor.R, this.HeadersFontColor.G, this.HeadersFontColor.B), this.Font);

                // If we're on the selected column draw the icon.
                if (j == this.selectedDataGridColumnIndex) {
                    if (dataGridColumn.order == Order.ASC)
                        this.headers.graphics.DrawImage(this.dataGridIcon_Asc, this.columns_.Count / 2 * dataGridColumn.width, 5, this.dataGridIcon_Asc.Width, this.dataGridIcon_Asc.Height);
                    else
                        this.headers.graphics.DrawImage(this.dataGridIcon_Desc, this.columns_.Count / 2 * dataGridColumn.width, 5, this.dataGridIcon_Desc.Width, this.dataGridIcon_Desc.Height);
                }

                x += dataGridColumn.width;
            }
        }

        private void UpdateItem(int index, bool clear) {
            if (clear)
                this.RenderItemClear(index);

            if (index == this.selectedIndex)
                this.RenderItem(this.selectedIndex, ((DataGridItem)this.rows_[index]).data, this.SelectedItemBackColor, this.SelectedItemFontColor);
            else {
                if (index % 2 == 0) {

                    this.RenderItem(index, ((DataGridItem)this.rows_[index]).data, this.ItemsBackColor, this.ItemsFontColor);
                }
                else {

                    this.RenderItem(index, ((DataGridItem)this.rows_[index]).data, this.ItemsAltBackColor, this.ItemsFontColor);
                }
            }
        }

        private void RenderItem(int index, object[] data, Media.Color backColor, Media.Color fontColor) {
            var x = 0;
            var y = index * this.RowHeight;

            using (var gridColorPen = new GHIElectronics.Endpoint.Drawing.Pen(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.GridColor.R, this.GridColor.G, this.GridColor.B))) {

                using (var itemsBackColorBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(backColor.R, backColor.G, backColor.B))) {

                    this.items.graphics.FillRectangle(itemsBackColorBrush, 0, index * this.RowHeight, this.items.Width, this.RowHeight);
                }

                DataGridColumn dataGridColumn;

                for (var i = 0; i < this.columns_.Count; i++) {
                    dataGridColumn = (DataGridColumn)this.columns_[i];

                    this.items.graphics.DrawTextInRect(data[i].ToString(), x + 5, y + (this.RowHeight - this.Font.Height) / 2, dataGridColumn.width, this.RowHeight, 0, GHIElectronics.Endpoint.Drawing.Color.FromArgb(fontColor.R, fontColor.G, fontColor.B), this.Font);
                    this.items.graphics.DrawLine(gridColorPen, x, y, x, y + this.RowHeight);

                    x += dataGridColumn.width;
                }
            }
        }

        private void RenderItemClear(int index) {
            using (var blackColorColorBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(0, 0, 0))) {
                this.headers.graphics.FillRectangle(blackColorColorBrush, 0, index * this.RowHeight, this.Width, this.RowHeight);
            }
        }

        private void RenderEmpty() {

            using (var itemsBackColoBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.ItemsBackColor.R, this.ItemsBackColor.G, this.ItemsBackColor.B))) {

                this.items.graphics.FillRectangle(itemsBackColoBrush, 0, 0, this.items.Width, this.items.Height);
            }

            using (var gridColorPen = new GHIElectronics.Endpoint.Drawing.Pen(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.GridColor.R, this.GridColor.G, this.GridColor.B))) {

                var x = 0;
                for (var i = 0; i < this.columns_.Count; i++) {
                    this.items.graphics.DrawLine(gridColorPen, x, 0, x, this.items.Height);
                    x += ((DataGridColumn)this.columns_[i]).width;
                }
            }
        }

        /// <summary>
        /// Renders the DataGrid onto it's parent container's graphics.
        /// </summary>
        public override void OnRender(DrawingContext dc) {
            if (this.ShowHeaders && this.renderHeaders) {
                //this.renderHeaders = false; TODO
                this.RenderHeaders();
            }


            if (this.renderItems) {
                this.renderItems = false;

                // Only recreate the items Bitmap if necessary
                if (this.items == null || this.items.Height != this.rows_.Count * this.RowHeight) {
                    if (this.items != null)
                        this.items.graphics.Dispose();

                    if (this.rows_.Count < this.rowCount) {

                        this.items = BitmapImage.FromGraphics(new Graphics(this.Width, this.rowCount * this.RowHeight));

                        //TODO
                        using (var itemsBackColorBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.ItemsBackColor.R, this.ItemsBackColor.G, this.ItemsBackColor.B))) {

                            this.items.graphics.FillRectangle(itemsBackColorBrush, 0, 0, this.items.Width, this.items.Height);
                        }

                        this.RenderEmpty();
                    }
                    else {
                        //this.items = new Bitmap(this.Width, this.rows_.Count * this.RowHeight);
                        this.items = BitmapImage.FromGraphics(new Graphics(this.Width, this.rows_.Count * this.RowHeight));

                        //TODO
                        using (var itemsBackColorBrush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(this.ItemsBackColor.R, this.ItemsBackColor.G, this.ItemsBackColor.B))) {

                            this.items.graphics.FillRectangle(itemsBackColorBrush, 0, 0, this.items.Width, this.items.Height);
                        }
                    }



                }
                else {
                    using (var blackColorBush = new GHIElectronics.Endpoint.Drawing.SolidBrush(GHIElectronics.Endpoint.Drawing.Color.FromArgb(0, 0, 0))) {
                        this.items.graphics.FillRectangle(blackColorBush, 0, 0, this.items.Width, this.items.Height);
                    }

                }



                if (this.rows_.Count > 0) {
                    for (var i = 0; i < this.rows_.Count; i++)
                        this.UpdateItem(i, false);
                }
            }

            var x = 0;
            var y = 0;

            if (this.showHeaders) {
                dc.DrawImage(this.headers, x, y, 0, 0, this.Width, this.RowHeight);
                y += this.RowHeight;
            }

            dc.DrawImage(this.items, x, y, 0, this.listY, this.Width, this.rowCount * this.RowHeight);

            if (this.ShowScrollbar) {
                this.scrollbarHeight = this.RowHeight;
                var travel = this.Height - this.scrollbarHeight;

                if (this.rows_.Count > 0)
                    this.scrollbarTick = (int)System.Math.Round((double)(travel / this.rows_.Count));
                else
                    this.scrollbarTick = 0;

                this.scrollbarHeight = this.Height - ((this.rows_.Count - this.rowCount) * this.scrollbarTick);

                x += this.Width - this.ScrollbarWidth;
                y = 0 + this.posY;

                var scrollbarBackColorPen = new Media.Pen(this.ScrollbarBackColor);
                var scrollbarBackColorBrush = new Media.SolidColorBrush(this.ScrollbarBackColor);

                dc.DrawRectangle(scrollbarBackColorBrush, scrollbarBackColorPen, x, y, this.ScrollbarWidth, this.Height);

                // Only show the scrollbar scrubber if it's smaller than the Height
                if (this.scrollbarHeight < this.Height) {

                    var scrollbarScrubberColorBrush = new Media.SolidColorBrush(this.ScrollbarScrubberColor);
                    var scrollbarScrubberColorPen = new Media.Pen(this.ScrollbarScrubberColor);

                    dc.DrawRectangle(scrollbarScrubberColorBrush, scrollbarScrubberColorPen, x, y + (this.scrollIndex * this.scrollbarTick), this.ScrollbarWidth, this.scrollbarHeight);
                }
            }
        }

        /// <summary>
        /// Handles the touch down event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchDown(TouchEventArgs e) {


            if (this.rows_.Count > 0) {
                var touchPoint = new Point(e.Touches[0].X, e.Touches[0].Y);

                this.pressed = true;
                this.lastTouchY = touchPoint.Y;
                this.lastListY = this.listY;

                this.posX = 0;
                this.posY = 0;

                this.PointToScreen(ref this.posX, ref this.posY);
            }

            //return e;
        }

        /// <summary>
        /// Handles the touch up event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchUp(TouchEventArgs e) {
            if (!this.pressed)
                return;

            var touchPoint = new Point(e.Touches[0].X, e.Touches[0].Y);
            var isContained = false;

            if (!this.moving) {
                var x = this.posX;
                var y = this.posY;

                var index = ((this.listY + touchPoint.Y) - y) / this.RowHeight;
                var rowIndex = index;
                // If headers are present the rowIndex needs to be offset
                if (this.ShowHeaders)
                    rowIndex--;

                var columnIndex = 0;
                DataGridColumn dataGridColumn;

                for (var i = 0; i < this.columns_.Count; i++) {
                    dataGridColumn = (DataGridColumn)this.columns_[i];

                    if (IsContains(touchPoint, x, y, dataGridColumn.width, this.Height)) {
                        columnIndex = i;

                        isContained = true;
                        break;
                    }

                    x += dataGridColumn.width;
                }

                if (index == 0 && this.ShowHeaders && this.SortableHeaders) {
                    this.Sort(columnIndex);
                    this.Invalidate();
                }
                else {
                    if (isContained && this.TappableCells) {
                        this.SelectedIndex = rowIndex;
                        this.TriggerTapCellEvent(this, new TapCellEventArgs(columnIndex, rowIndex));
                    }
                }
            }

            this.pressed = false;
            this.moving = false;
            this.ignoredTouchMoves = 0;
        }

        /// <summary>
        /// Handles the touch move event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchMove(TouchEventArgs e) {
            if (!this.Draggable || !this.pressed || this.rows_.Count <= this.RowCount)
                return;

            if (!this.moving) {
                if (this.ignoredTouchMoves < this.MaxIgnoredTouchMoves)
                    this.ignoredTouchMoves++;
                else {
                    this.ignoredTouchMoves = 0;
                    this.moving = true;
                }
            }
            else {
                var touchPoint = new Point(e.Touches[0].X, e.Touches[0].Y);

                this.listY = this.lastListY - (touchPoint.Y - this.lastTouchY);
                this.listY = MinMax(this.listY, 0, this.listMaxY);

                this.scrollIndex = (int)System.Math.Ceiling((double)this.listY / this.RowHeight);

                this.Invalidate();
            }

        }

        private bool RowIndexIsValid(int index) => (this.rows_.Count > 0 && index > -1 && index < this.rows_.Count);

        private bool ColumnIndexIsValid(int index) => (this.columns_.Count > 0 && index > -1 && index < this.columns_.Count);

        /// <summary>
        /// Adds a column.
        /// </summary>
        /// <param name="dataGridColumn">dataGridColumn</param>
        public void AddColumn(DataGridColumn dataGridColumn) => this.AddColumnAt(-1, dataGridColumn);

        /// <summary>
        /// Adds a column at a specified index.
        /// </summary>
        /// <param name="index">index</param>
        /// <param name="dataGridColumn">dataGridColumn</param>
        public void AddColumnAt(int index, DataGridColumn dataGridColumn) {
            if (this.columns_.Count == 0 || index == -1 || index > this.columns_.Count - 1)
                this.columns_.Add(dataGridColumn);
            else
                this.columns_.Insert(index, dataGridColumn);

            // A new column was added so we must re-render the headers.
            this.renderHeaders = true;
        }

        /// <summary>
        /// Removes a column.
        /// </summary>
        /// <param name="dataGridColumn">dataGridColumn</param>
        public void RemoveColumn(DataGridColumn dataGridColumn) {
            var index = this.columns_.IndexOf(dataGridColumn);
            if (index > -1)
                this.RemoveColumnAt(index);
        }

        /// <summary>
        /// Removes a column at a specified index.
        /// </summary>
        /// <param name="index">index</param>
        public void RemoveColumnAt(int index) {
            if (this.ColumnIndexIsValid(index)) {
                this.columns_.RemoveAt(index);
                this.renderHeaders = true;
            }
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="dataGridItem">dataGridItem</param>
        public void AddItem(DataGridItem dataGridItem) => this.AddItemAt(-1, dataGridItem);

        /// <summary>
        /// Adds an item at a specified index.
        /// </summary>
        /// <param name="index">index</param>
        /// <param name="dataGridItem">dataGridItem</param>
        public void AddItemAt(int index, DataGridItem dataGridItem) {
            if (dataGridItem.data.Length != this.columns_.Count)
                throw new ArgumentException("The DataGridRow data length does not match the DataGrid's column count.", "dataGridRow");

            if (this.rows_.Count == 0 || index == -1 || index > this.rows_.Count - 1)
                this.rows_.Add(dataGridItem);
            else
                this.rows_.Insert(index, dataGridItem);

            // A new row was added so we must re-render the items.
            this.renderItems = true;

            // Calculate the max list Y position
            this.listMaxY = this.rows_.Count * this.RowHeight;
            if (this.ShowHeaders) this.listMaxY += this.RowHeight;
            this.listMaxY -= this.Height;
        }

        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="dataGridItem">dataGridItem</param>
        public void RemoveItem(DataGridItem dataGridItem) {
            var index = this.rows_.IndexOf(dataGridItem);
            if (index > -1)
                this.RemoveItemAt(index);
        }

        /// <summary>
        /// Removes an item a specified index.
        /// </summary>
        /// <param name="index">index</param>
        public void RemoveItemAt(int index) {
            if (this.RowIndexIsValid(index)) {
                this.rows_.RemoveAt(index);

                // A row was removed so we must re-render the items.
                this.renderItems = true;

                // If the rows fall below the selected index
                // default to no selection
                if (this.rows_.Count - 1 < this.SelectedIndex)
                    this.SelectedIndex = -1;

                // If the index removed was the selected index
                // revert to no selection
                if (this.SelectedIndex == index)
                    this.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// Scroll the rows up by a specified amount.
        /// </summary>
        /// <param name="amount">amount</param>
        public void ScrollUp(int amount) {
            if (this.rows_.Count == 0)
                return;

            if (this.scrollIndex - amount >= 0)
                this.scrollIndex -= amount;
            else
                this.scrollIndex = 0;

            this.listY = this.scrollIndex * this.RowHeight;
        }

        /// <summary>
        /// Scroll the rows down by a specified amount.
        /// </summary>
        /// <param name="amount">amount</param>
        public void ScrollDown(int amount) {
            if (this.rows_.Count == 0)
                return;

            if (this.scrollIndex + amount < this.rows_.Count - this.rowCount)
                this.scrollIndex += amount;
            else
                this.scrollIndex = this.rows_.Count - this.rowCount;

            this.listY = this.scrollIndex * this.RowHeight;
        }

        /// <summary>
        /// Scroll the rows to a specified index.
        /// </summary>
        /// <param name="index">index</param>
        public void ScrollTo(int index) {
            if (this.RowIndexIsValid(index)) {
                this.scrollIndex = index;
                this.listY = this.scrollIndex * this.RowHeight;
            }
        }

        /// <summary>
        /// Sets new row data.
        /// </summary>
        /// <param name="index">index</param>
        /// <param name="data">Data object array.</param>
        public void SetRowData(int index, object[] data) {
            if (this.RowIndexIsValid(index)) {
                if (data.Length != this.columns_.Count)
                    throw new ArgumentException("The data length does not match the DataGrid's column count.", "data");

                ((DataGridItem)this.rows_[index]).data = data;
                this.UpdateItem(index, true);
            }
        }

        /// <summary>
        /// Gets row data.
        /// </summary>
        /// <param name="index">index</param>
        /// <returns>Data object array.</returns>
        public object[] GetRowData(int index) {
            if (this.RowIndexIsValid(index))
                return ((DataGridItem)this.rows_[index]).data;
            else
                return null;
        }

        /// <summary>
        /// Sets a cell's data.
        /// </summary>
        /// <param name="columnIndex">columnIndex</param>
        /// <param name="rowIndex">rowIndex</param>
        /// <param name="data">data</param>
        public void SetCellData(int columnIndex, int rowIndex, object data) {
            if (this.ColumnIndexIsValid(columnIndex) && this.RowIndexIsValid(rowIndex)) {
                ((DataGridItem)this.rows_[rowIndex]).data[columnIndex] = data;
                this.UpdateItem(rowIndex, true);
            }
        }

        /// <summary>
        /// Get a cell's data.
        /// </summary>
        /// <param name="columnIndex">columnIndex</param>
        /// <param name="rowIndex">rowIndex</param>
        public object GetCellData(int columnIndex, int rowIndex) {
            if (this.ColumnIndexIsValid(columnIndex) && this.RowIndexIsValid(rowIndex))
                return ((DataGridItem)this.rows_[rowIndex]).data[columnIndex];
            else
                return null;
        }

        private void PerformSort() {
            var order = (this.order == Order.DESC) ? -1 : 1;
            object item;
            int i, j;

            for (i = 0; i < this.rows_.Count; i++) {
                item = this.rows_[i];
                j = i;

                while ((j > 0) && ((this.comparer.Compare(this.rows_[j - 1], item) * order) > 0)) {
                    this.rows_[j] = this.rows_[j - 1];
                    j--;
                }

                this.rows_[j] = item;
            }

            this.renderHeaders = true;
            this.renderItems = true;
        }

        /// <summary>
        /// Sorts the items on a specified column index.
        /// </summary>
        /// <param name="columnIndex"></param>
        public void Sort(int columnIndex) {
            this.selectedDataGridColumnIndex = columnIndex;

            var dataGridColumn = (DataGridColumn)this.columns_[columnIndex];
            dataGridColumn.ToggleOrder();

            this.order = dataGridColumn.order;
            this.comparer.ColumnIndex = columnIndex;

            this.PerformSort();

            for (var i = 0; i < this.rows_.Count; i++) {
                if ((DataGridItem)this.rows_[i] == this.selectedItem) {
                    this.RenderItemClear(this.selectedIndex);
                    this.selectedIndex = i;
                }
            }
        }

        /// <summary>
        /// Clears all items including their data and resets the data grid.
        /// </summary>
        public void Clear() {
            this.rows_.Clear();

            this.renderHeaders = true;
            this.renderItems = true;
            this.scrollIndex = 0;
            this.pressed = false;
            this.moving = false;

            this.listY = 0;
            this.listMaxY = 0;
            this.ignoredTouchMoves = 0;
            this.MaxIgnoredTouchMoves = 10;

            this.SelectedIndex = -1;

            this.selectedDataGridColumnIndex = -1;
        }

        /// <summary>
        /// Number of rows displayed.
        /// </summary>
        public int RowCount {
            get => this.rowCount;
            set {
                if (value != this.rowCount) {
                    this.rowCount = value;
                    this.Height = this.rowCount * this.RowHeight;
                }
            }
        }

        /// <summary>
        /// Font used by the text.
        /// </summary>
        //public Font Font { get; set; }//= Resources.GetFont(Resources.FontResources.droid_reg12);

        /// <summary>
        /// Row height.
        /// </summary>
        public int RowHeight { get; set; } = 20;

        /// <summary>
        /// The currently selected index.
        /// </summary>
        public int SelectedIndex {
            get => this.selectedIndex;
            set {
                if (value != this.selectedIndex && (value >= -1 && value < this.rows_.Count)) {
                    // If a previous selection exists -- clear it
                    if (this.selectedIndex > -1 && this.selectedIndex < this.rows_.Count) {
                        var oldSelectedIndex = this.selectedIndex;
                        this.selectedIndex = -1;
                        this.UpdateItem(oldSelectedIndex, true);
                    }

                    this.selectedIndex = value;

                    if (this.selectedIndex > -1) {
                        this.selectedItem = (DataGridItem)this.rows_[this.selectedIndex];
                        this.RenderItem(this.selectedIndex, ((DataGridItem)this.rows_[this.selectedIndex]).data, this.SelectedItemBackColor, this.SelectedItemFontColor);
                    }
                    else
                        this.selectedItem = null;

                    // Scroll as selected index gets out of view.
                    var rowCount = this.RowCount - 1;
                    if (this.selectedIndex <= this.scrollIndex)
                        this.ScrollTo(this.selectedIndex);
                    else if (this.selectedIndex > this.scrollIndex + rowCount)
                        this.ScrollTo(this.selectedIndex - rowCount);

                    this.Invalidate();
                }
            }
        }

        /// <summary>
        /// Number of items in the DataGrid.
        /// </summary>
        public int NumItems => this.rows_.Count;

        /// <summary>
        /// Indicates whether items trigger cell tap events or not.
        /// </summary>
        public bool TappableCells { get; set; } = true;

        /// <summary>
        /// Indicates whether or not the item list can be dragged up and down.
        /// </summary>
        public bool Draggable { get; set; } = true;

        /// <summary>
        /// Indicates whether the headers are shown.
        /// </summary>
        public bool ShowHeaders {
            get => this.showHeaders;
            set {
                if (value != this.showHeaders) {
                    this.showHeaders = value;
                    if (this.showHeaders)
                        this.rowCount--;
                    else
                        this.rowCount++;

                    this.renderHeaders = true;
                    this.renderItems = true;
                }
            }
        }

        /// <summary>
        /// Indicates whether the headers are sortable.
        /// </summary>
        public bool SortableHeaders { get; set; } = true;

        /// <summary>
        /// Headers background color.
        /// </summary>        
        public Media.Color HeadersBackColor { get; set; } = Colors.Black;

        /// <summary>
        /// Headers font color.
        /// </summary>
        public Media.Color HeadersFontColor { get; set; } = Colors.White;

        /// <summary>
        /// Items background color.
        /// </summary>
        public Media.Color ItemsBackColor { get; set; } = Colors.White;

        /// <summary>
        /// Items alternate background color.
        /// </summary>
        public Media.Color ItemsAltBackColor { get; set; } = Media.Color.FromRgb(0xF0, 0xF0, 0xF0);

        /// <summary>
        /// Items font color.
        /// </summary>
        public Media.Color ItemsFontColor { get; set; } = Colors.Black;

        /// <summary>
        /// Selected item background color.
        /// </summary>
        public Media.Color SelectedItemBackColor { get; set; } = Media.Color.FromRgb(0xF0, 0xF2, 0x99);

        /// <summary>
        /// Selected item font color.
        /// </summary>
        public Media.Color SelectedItemFontColor { get; set; } = Colors.Black;

        /// <summary>
        /// Grid color.
        /// </summary>
        public Media.Color GridColor { get; set; } = Colors.Gray;

        /// <summary>
        /// Indicates whether the scrollbar is shown.
        /// </summary>
        public bool ShowScrollbar { get; set; } = true;

        /// <summary>
        /// Scrollbar width.
        /// </summary>
        public int ScrollbarWidth { get; set; } = 4;

        /// <summary>
        /// Scrollbar background color.
        /// </summary>
        public Media.Color ScrollbarBackColor { get; set; } = Media.Color.FromRgb(0xC0, 0xC0, 0xC0);

        /// <summary>
        /// Scrollbar scrubber color.
        /// </summary>
        public Media.Color ScrollbarScrubberColor { get; set; } = Colors.Black;

        /// <summary>
        /// Touch senstitive.
        /// </summary>
        public int MaxIgnoredTouchMoves { get; set; } = 1;

        /// <summary>
        /// The order in which rows are sorted.
        /// </summary>
        /// <remarks>ASC stands for ascending ex: 1 to 10 or A to Z. DESC stands for descending ex: 10 to 1 or Z to A.</remarks>
        public enum Order {
            /// <summary>
            /// Ascending
            /// </summary>
            ASC,

            /// <summary>
            /// Descending
            /// </summary>
            DESC
        }

        /// <summary>
        /// Checks a value against minimum and maximum limits.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <returns>The value limited by min and max.</returns>
        private static int MinMax(int value, int min, int max) => Math.Max(min, System.Math.Min(max, value));
        private static bool IsContains(Point point, int x, int y, int width, int height) {

            if (point.X >= x && point.X < (x + width) && point.Y >= y && point.Y < (y + height))
                return true;

            return false;

        }
    }
}
