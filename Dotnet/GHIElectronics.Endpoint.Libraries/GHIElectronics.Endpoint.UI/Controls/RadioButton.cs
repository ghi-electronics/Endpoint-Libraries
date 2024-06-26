using System;
using System.Collections;
using GHIElectronics.Endpoint.Drawing;
using System.Text;
using System.Threading;
using GHIElectronics.Endpoint.UI.Input;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI.Media.UIImaging;
using GHIElectronics.Endpoint.UI.Properties;

namespace GHIElectronics.Endpoint.UI.Controls {
    public static class RadioButtonManager {
        private static Hashtable groups = new Hashtable();

        // Adds a radio button to it's group.
        internal static void AddButton(RadioButton radioButton) {
            var name = radioButton.GroupName;

            if (!groups.Contains(name))
                groups.Add(name, new ArrayList());

            ((ArrayList)groups[name]).Add(radioButton);
        }

        public static string GetValue(string groupName) {
            if (!groups.Contains(groupName))
                throw new ArgumentOutOfRangeException("groupName", "No such radio button group exists.");

            // Find the selected button
            var group = (ArrayList)groups[groupName];
            RadioButton button;
            for (var i = 0; i < group.Count; i++) {
                button = (RadioButton)group[i];
                if (button.Checked)
                    return button.Value;
            }

            return string.Empty;
        }

        public static int GetCount(string groupName) {
            if (!groups.Contains(groupName))
                throw new ArgumentOutOfRangeException("groupName", "No such radio button group exists.");

            return ((ArrayList)groups[groupName]).Count;
        }

        internal static void TriggerTapEvent(object sender) {
            var tappedButton = (RadioButton)sender;

            if (!groups.Contains(tappedButton.GroupName))
                return;

            var group = (ArrayList)groups[tappedButton.GroupName];

            // Find the button that was previously selected and unselect it
            RadioButton button;
            for (var i = 0; i < group.Count; i++) {
                button = (RadioButton)group[i];
                if (button.Name == tappedButton.Name) continue;
                if (button.Checked) {
                    button.Checked = false;
                    break;
                }
            }
        }
    }

    public class RadioButton : ContentControl, IDisposable {
        public event RoutedEventHandler Click;
        private UIBitmap bitmapImageRadioButton;

        private bool isChecked = false;
        private string value = string.Empty;

        public string Name { get; set; } = string.Empty;
        public ushort Alpha { get; set; } = 0xC8;
        public int RadiusBorder { get; set; } = 5;

        private void InitResource() => this.bitmapImageRadioButton = UIBitmap.FromData(Resources.RadioButton);

        public RadioButton() : this(string.Empty) {

        }
        public RadioButton(string groupName) : base() {
            this.InitResource();

            this.Width = this.bitmapImageRadioButton.Width;
            this.Height = this.bitmapImageRadioButton.Height;

            this.GroupName = groupName;

            RadioButtonManager.AddButton(this);
        }

        public override void OnRender(DrawingContext dc) {
            var x = 0;
            var y = 0;

            var alpha = (this.IsEnabled) ? this.Alpha : (ushort)(this.Alpha / 2);

            dc.Scale9Image(x, y, this.Width, this.Height, this.bitmapImageRadioButton, this.RadiusBorder, this.RadiusBorder, this.RadiusBorder, this.RadiusBorder, alpha);

            x += (this.Width / 2) - 1;
            y += (this.Height / 2) - 1;

            var xRadius = this.Width / 3;
            var yRadius = this.Height / 3;

            var penSelected = new GHIElectronics.Endpoint.UI.Media.Pen(this.SelectedColor, 1);
            var penSelectedOutline = new GHIElectronics.Endpoint.UI.Media.Pen(this.SelectedOutlineColor, 1);
            var penUnselectOutline = new GHIElectronics.Endpoint.UI.Media.Pen(this.OutlineUnselectColor, 1);

            var brush = new SolidColorBrush(this.SelectedColor);

            if (this.isChecked) {
                dc.DrawEllipse(null, penSelectedOutline, x, y, xRadius, yRadius);

                dc.DrawEllipse(brush, penSelected, x, y, this.Width / 4, this.Height / 4);

            }
            else {
                dc.DrawEllipse(null, penUnselectOutline, x, y, xRadius, yRadius);
            }
        }

        protected override void OnTouchUp(TouchEventArgs e) {
            if (!this.IsEnabled) {
                return;
            }

            var evt = new RoutedEvent("TouchUpEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler));
            var args = new RoutedEventArgs(evt, this);

            try {
                this.Click?.Invoke(this, args);
            }
            catch { }

            e.Handled = args.Handled;

            this.Toggle();

            if (this.Parent != null)
                this.Invalidate();
        }

        protected override void OnTouchDown(TouchEventArgs e) {
            if (!this.IsEnabled) {
                return;
            }

            var evt = new RoutedEvent("TouchDownEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler));
            var args = new RoutedEventArgs(evt, this);

            try {
                this.Click?.Invoke(this, args);
            }
            catch { }

            e.Handled = args.Handled;

            if (this.Parent != null)
                this.Invalidate();
        }

        public string Value {
            get {
                if (this.isChecked)
                    return this.value;
                else
                    return string.Empty;
            }
            set => this.value = value;
        }


        public bool Checked {
            get => this.isChecked;
            set {
                if (this.isChecked != value) {
                    this.isChecked = value;

                    if (this.Parent != null)
                        this.Invalidate();
                }
            }
        }

        public string GroupName { get; set; } = string.Empty;

        public void Toggle() {
            var groupCount = RadioButtonManager.GetCount(this.GroupName);

            // Only allow change if this button is alone or in a group and not selected
            if (groupCount <= 1 || (groupCount > 1 && !this.Checked)) {
                this.Checked = !this.Checked;
                RadioButtonManager.TriggerTapEvent(this);
            }
        }

        public GHIElectronics.Endpoint.UI.Media.Color OutlineUnselectColor { get; set; } = GHIElectronics.Endpoint.UI.Media.Color.FromRgb(0xb8, 0xb8, 0xb8);
        public GHIElectronics.Endpoint.UI.Media.Color SelectedOutlineColor { get; set; } = GHIElectronics.Endpoint.UI.Media.Color.FromRgb(0x00, 0x2d, 0xff);
        public GHIElectronics.Endpoint.UI.Media.Color SelectedColor { get; set; } = GHIElectronics.Endpoint.UI.Media.Color.FromRgb(0x35, 0x8b, 0xf6);

        private bool disposed;

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!this.disposed) {

                this.bitmapImageRadioButton.graphics.Dispose();

                this.disposed = true;
            }
        }

        ~RadioButton() {
            this.Dispose(false);
        }
    }
}
