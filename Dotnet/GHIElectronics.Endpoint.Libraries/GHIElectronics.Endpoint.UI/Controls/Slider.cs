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
    public class Point {
        public int X { get; set; }
        public int Y { get; set; }
        public Point(int x, int y) {

            this.X = x;
            this.Y = y;
        }

    }

    /// <summary>
    /// The Slider component lets users select a value by sliding a knob along a track.
    /// </summary>
    public class Slider : ContentControl {

        public class ValueChangedEventArgs : EventArgs {
            public readonly double Value;

            public ValueChangedEventArgs(double newIndex) => this.Value = newIndex;
        }

        private UIBitmap bitmapImageButtonUp;
        private UIBitmap bitmapImageButtonDown;

        private bool dragging = false;
        private Orientation direction = Orientation.Horizontal;
        private Rectangle knob;
        private int knobSize;
        private int lineSize;
        private int tickInterval;
        private double tickSize;
        private int snapInterval;
        private double snapSize;
        private double pixelsPerValue;
        private double min;
        private double max;
        private double value;
        private int posX;
        private int posY;
        private bool disposed;
        private bool isRenderKnob;

        public delegate void ValueChangedEventHandler(object sender, ValueChangedEventArgs args);

        private event ValueChangedEventHandler TriggerValueChanged;

        private Media.Color color = Media.Color.FromRgb(0, 0, 0);
        private Media.Pen colorPen = new Media.Pen(Media.Color.FromRgb(0, 0, 0));


        /// <summary>
        /// Creates a new Slider component.
        /// </summary>     
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        public Slider(int width, int height) {
            this.Width = width;
            this.Height = height;

            // Default
            this.KnobSize = 20;
            this.SnapInterval = 10;
            this.TickInterval = 10;
            this.Minimum = 0;
            this.Maximum = 100;
            this.value = 0;

            this.bitmapImageButtonUp = UIBitmap.FromData(Resources.Button_Up);
            this.bitmapImageButtonDown = UIBitmap.FromData(Resources.Button_Down);

            this.Init();
        }

        /// <summary>
        /// Value changed event.
        /// </summary>           
        public event ValueChangedEventHandler OnValueChanged {
            add {
                this.VerifyAccess();
                this.TriggerValueChanged += value;
            }

            remove {
                this.VerifyAccess();
                this.TriggerValueChanged -= value;
            }
        }

        private void RenderKnob(Point globalPoint) {
            var localPoint = new Point(globalPoint.X, globalPoint.Y);

            this.isRenderKnob = true;

            if (this.direction == Orientation.Horizontal) {
                var half = this.knob.Width / 2;
                var maxX = this.Width - this.knob.Width;

                if (localPoint.X < half)
                    this.knob.X = 0;
                else if (localPoint.X - half > maxX)
                    this.knob.X = maxX;
                else
                    this.knob.X = localPoint.X - half;

                var interval = (int)System.Math.Round(this.knob.X / this.snapSize);

                if (this.SnapInterval > 0)
                    this.knob.X = (int)(interval * this.snapSize);

                this.Value = this.knob.X / this.pixelsPerValue;
            }
            else {
                var half = this.knob.Height / 2;
                var maxY = this.Height - this.knob.Height;

                if (localPoint.Y < half)
                    this.knob.Y = 0;
                else if (localPoint.Y - half > maxY)
                    this.knob.Y = maxY;
                else
                    this.knob.Y = localPoint.Y - half;

                var interval = (int)System.Math.Round(this.knob.Y / this.snapSize);

                if (this.SnapInterval > 0)
                    this.knob.Y = (int)(interval * this.snapSize);

                this.Value = this.max - (this.knob.Y / this.pixelsPerValue);
            }

            this.Invalidate();

            this.isRenderKnob = false;
        }

        /// <summary>
        /// Renders the Slider onto it's parent container's graphics.
        /// </summary>
        public override void OnRender(DrawingContext dc) {
            var x = 0;
            var y = 0;

            if (this.direction == Orientation.Horizontal) {
                var lineY = y + (this.Height / 2);
                var offsetX = this.knob.Width / 2;
                var knobY = this.Height - this.knob.Height;
                int tickX;
                var tickHeight = (int)System.Math.Ceiling(this.Height * 0.05);

                dc.DrawLine(this.colorPen, x + offsetX, lineY, x + offsetX + this.lineSize, lineY);

                if (this.TickInterval > 1) {
                    for (var i = 0; i < this.TickInterval + 1; i++) {
                        tickX = x + offsetX + (int)(i * this.tickSize);
                        dc.DrawLine(this.colorPen, tickX, y, tickX, y + tickHeight);
                    }
                }

                if (this.dragging)
                    dc.Scale9Image(x + this.knob.X, y + knobY, this.knob.Width, this.knob.Height, this.bitmapImageButtonDown, 5, 5, 5, 5, (ushort)this.Alpha);
                else
                    dc.Scale9Image(x + this.knob.X, y + knobY, this.knob.Width, this.knob.Height, this.bitmapImageButtonUp, 5, 5, 5, 5, (ushort)this.Alpha);
            }
            else {
                var lineX = x + (this.Width / 2);
                var offsetY = this.knob.Height / 2;
                var knobX = this.Width - this.knob.Width;
                int tickY;
                var tickWidth = (int)System.Math.Ceiling(this.Width * 0.05);

                dc.DrawLine(this.colorPen, lineX, y + offsetY, lineX, y + offsetY + this.lineSize);

                if (this.TickInterval > 1) {
                    for (var i = 0; i < this.TickInterval + 1; i++) {
                        tickY = y + offsetY + (int)(i * this.tickSize);
                        dc.DrawLine(this.colorPen, x, tickY, x + tickWidth, tickY);
                    }
                }

                if (this.dragging)
                    dc.Scale9Image(x + knobX, y + this.knob.Y, this.knob.Width, this.knob.Height, this.bitmapImageButtonDown, 5, 5, 5, 5, (ushort)this.Alpha);
                else
                    dc.Scale9Image(x + knobX, y + this.knob.Y, this.knob.Width, this.knob.Height, this.bitmapImageButtonUp, 5, 5, 5, 5, (ushort)this.Alpha);
            }
        }

        /// <summary>
        /// Handles the touch down event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchDown(TouchEventArgs e) {
            this.posX = 0;
            this.posY = 0;
            this.PointToScreen(ref this.posX, ref this.posY);
            this.dragging = true;
        }

        /// <summary>
        /// Handles the touch up event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchUp(TouchEventArgs e) =>
            this.dragging = false;

        /// <summary>
        /// Handles the touch move event.
        /// </summary>
        /// <param name="e">Touch event arguments.</param>
        /// <returns>Touch event arguments.</returns>
        protected override void OnTouchMove(TouchEventArgs e) {
            if (this.dragging) {

                var touchPoint = new Point(e.Touches[0].X, e.Touches[0].Y);
                var point = new Point(touchPoint.X - this.posX, touchPoint.Y - this.posY);

                this.RenderKnob(point);
            }
        }

        private void Init() {
            if (this.direction == Orientation.Horizontal) {
                this.knob = new Rectangle(0, 0, this.knobSize, (int)((double)this.Height / 1.2));
                this.lineSize = this.Width - this.knobSize;
            }
            else {
                this.knob = new Rectangle(0, 0, (int)((double)this.Width / 1.2), this.knobSize);
                this.lineSize = this.Height - this.knobSize;
            }

            if (this.tickInterval < 0)
                this.tickInterval = 0;
            else if (this.tickInterval > this.lineSize)
                this.tickInterval = this.lineSize;

            if (this.tickInterval > 0)
                this.tickSize = (double)this.lineSize / this.TickInterval;
            else
                this.tickSize = (double)this.lineSize / this.lineSize;

            if (this.snapInterval < 0)
                this.snapInterval = 0;
            else if (this.snapInterval > this.lineSize)
                this.snapInterval = this.lineSize;

            if (this.snapInterval > 0)
                this.snapSize = (double)this.lineSize / this.snapInterval;
            else
                this.snapSize = (double)this.lineSize / this.lineSize;

            if (this.max > 0)
                this.pixelsPerValue = this.lineSize / (this.max - this.min);
        }


        /// <summary>
        /// Direction of the slider; horizontal or vertical.
        /// </summary>
        public Orientation Direction {
            get => this.direction;
            set {
                if (value != this.direction) {
                    this.direction = value;
                    this.Init();
                }
            }
        }

        /// <summary>
        /// Size of the knob.
        /// </summary>
        public int KnobSize {
            get => this.knobSize;
            set {
                this.knobSize = value;
                this.Init();
            }
        }

        /// <summary>
        /// Maximum value.
        /// </summary>
        public double Maximum {
            get => this.max;
            set {
                this.max = value;
                this.Init();
            }
        }

        /// <summary>
        /// Minimum value.
        /// </summary>
        public double Minimum {
            get => this.min;
            set {
                this.min = value;
                this.Init();
            }
        }

        /// <summary>
        /// Increment by which the value is increased or decreased as the user slides the knob.
        /// </summary>
        public int SnapInterval {
            get => this.snapInterval;
            set {
                this.snapInterval = value;
                this.Init();
            }
        }

        /// <summary>
        /// Tick mark spacing relative to the maximum value.
        /// </summary>
        public int TickInterval {
            get => this.tickInterval;
            set {
                this.tickInterval = value;
                this.Init();
            }
        }

        /// <summary>
        /// Gets or sets the current value.
        /// </summary>
        public double Value {
            get => this.value;
            set {
                var oldValue = this.value;
                this.value = value;

                if (oldValue != this.value) {
                    var args = new ValueChangedEventArgs(this.value);

                    this.TriggerValueChanged?.Invoke(this, args);

                    // if value change by set value from user directly
                    if (this.isRenderKnob == false) {
                        if (this.direction == Orientation.Horizontal) {
                            this.knob.X = (int)(this.value * this.pixelsPerValue);
                        }
                        else {

                            if (this.pixelsPerValue != 0) {
                                this.knob.Y = (int)((this.max - this.value) / this.pixelsPerValue);
                            }
                        }
                    }
                }
            }
        }
        public ushort Alpha { get; set; } = 0xC8;

        public Media.Color Color {
            get => this.color;
            set {
                this.color = value;
                this.colorPen = new Media.Pen(this.color); ;
            }
        }
		
		public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!this.disposed) {

                this.bitmapImageButtonDown.graphics.Dispose();
                this.bitmapImageButtonUp.graphics.Dispose();                

                this.disposed = true;
            }
        }

        ~Slider() {
            this.Dispose(false);
        }
    }
}
