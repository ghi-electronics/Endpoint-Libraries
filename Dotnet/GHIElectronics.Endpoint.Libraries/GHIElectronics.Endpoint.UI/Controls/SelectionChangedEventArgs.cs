using System;

namespace GHIElectronics.Endpoint.UI.Controls {
    public class SelectionChangedEventArgs : EventArgs {
        public readonly int PreviousSelectedIndex;
        public readonly int SelectedIndex;

        public SelectionChangedEventArgs(int previousIndex, int newIndex) {
            this.PreviousSelectedIndex = previousIndex;
            this.SelectedIndex = newIndex;
        }
    }
}


