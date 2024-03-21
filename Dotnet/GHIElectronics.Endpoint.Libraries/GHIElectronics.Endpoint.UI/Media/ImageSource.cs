using GHIElectronics.Endpoint.Drawing;
using SkiaSharp;

namespace GHIElectronics.Endpoint.UI.Media {
    public abstract class ImageSource {
        internal readonly Graphics graphics;

        public virtual int Width => this.graphics.Width;
        public virtual int Height => this.graphics.Height;

        protected ImageSource(Graphics g) => this.graphics = g;

        protected ImageSource(byte[] data) => this.graphics = Graphics.FromData(data);
    }

    namespace UIImaging {
        public abstract class BitmapSource : ImageSource {
            protected BitmapSource(Graphics g) : base(g) {

            }

            protected BitmapSource(byte[] data) : base(data) {

            }
        }

        public class UIBitmap : BitmapSource {

            private SKBitmap skBitmap;

            private UIBitmap(Graphics g) : base(g) {
                
            }

            private UIBitmap(byte[] data) : base(data) {                
            }

            public static UIBitmap FromGraphics(Graphics g) => new UIBitmap(g);

            public static UIBitmap FromData(byte[] data) => new UIBitmap(data);
        }
    }
}
