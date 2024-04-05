////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Copyright (c) GHI Electronics, LLC.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;

namespace GHIElectronics.Endpoint.UI.Controls {
    /// <summary>
    /// The DataGridItemComparer class allows comparison between DataGridItems.
    /// </summary>
    public class DataGridItemComparer : IComparer {
        /// <summary>
        /// Compare two DataGridRow objects.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(object x, object y) {
            var row1 = x as DataGridItem;
            var row2 = y as DataGridItem;
            return row1.CompareTo(row2, this.ColumnIndex);
        }

        /// <summary>
        /// Column index to compare.
        /// </summary>
        public int ColumnIndex { get; set; }
    }
}
