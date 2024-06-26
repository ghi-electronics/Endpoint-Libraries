using System;
using System.Collections;
using GHIElectronics.Endpoint.Drawing;
using System.Text;
using System.Threading;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI.Media.UIImaging;
using GHIElectronics.Endpoint.UI.Properties;

namespace GHIElectronics.Endpoint.UI.Controls {
    public enum Direction {
        Left,
        Right,
        Up,
        Down
    }

    public class ProgressBar : Image, IDisposable {
        private UIBitmap bitmapImageProgressBar;
        private UIBitmap bitmapImageProgressBarFill;

        public Direction Direction { get; set; } = Direction.Right;
        public int MaxValue { get; set; } = 100;
        public int Value { get; set; } = 0;
        public ushort Alpha { get; set; } = 0xC8;
        public int Border { get; set; } = 5;


        private void InitResource() {
            this.bitmapImageProgressBar = UIBitmap.FromData(Resources.ProgressBar);
            this.bitmapImageProgressBarFill = UIBitmap.FromData(Resources.ProgressBar_Fill);
        }

        public ProgressBar() : base() => this.InitResource();

        public override void OnRender(DrawingContext dc) {
            var x = 0;
            var y = 0;

            dc.Scale9Image(0, 0, this.Width, this.Height, this.bitmapImageProgressBar, this.Border, this.Border, this.Border, this.Border, this.Alpha);

            x += 1;
            y += 1;

            var width = this.Width;
            var height = this.Height;
            var size = (float)this.Value / (float)this.MaxValue;

            if (this.Direction == Direction.Right || this.Direction == Direction.Left) {
                width = (int)((this.Width - 2) * size);
                height = this.Height - 2;
            }
            else if (this.Direction == Direction.Up || this.Direction == Direction.Down) {
                width = this.Width - 2;
                height = (int)((this.Height - 2) * size);
            }

            if (this.Direction == Direction.Left) {
                x += (this.Width - 2) - width;
            }
            else if (this.Direction == Direction.Up) {
                y += (this.Height - 2) - height;
            }

            dc.Scale9Image(x, y, width, height, this.bitmapImageProgressBarFill, this.Border, this.Border, this.Border, this.Border, this.Alpha);
        }

        private bool disposed;

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!this.disposed) {

                this.bitmapImageProgressBar.graphics.Dispose();
                this.bitmapImageProgressBarFill.graphics.Dispose();

                this.disposed = true;
            }
        }

        ~ProgressBar() {
            this.Dispose(false);
        }

    }
}
