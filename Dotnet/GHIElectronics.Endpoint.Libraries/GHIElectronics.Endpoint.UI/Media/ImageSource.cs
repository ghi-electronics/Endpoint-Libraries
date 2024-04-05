using GHIElectronics.Endpoint.Drawing;
using SkiaSharp;

namespace GHIElectronics.Endpoint.UI.Media {
    public abstract class ImageSource {
        internal readonly Graphics graphics;

        public virtual int Width => this.graphics.Width;
        public virtual int Height => this.graphics.Height;

        protected ImageSource(Graphics g) => this.graphics = g;

        protected ImageSource(byte[] data) => this.graphics = Graphics.FromData(data);

        protected ImageSource(byte[] data, int width, int height) => this.graphics = Graphics.FromData(data, width, height);
    }

    namespace UIImaging {
        public abstract class BitmapSource : ImageSource {
            protected BitmapSource(Graphics g) : base(g) {

            }

            protected BitmapSource(byte[] data) : base(data) {

            }

            protected BitmapSource(byte[] data, int width, int height) : base(data, width, height) {

            }
        }

        public class UIBitmap : BitmapSource {

            private SKBitmap skBitmap;

            private UIBitmap(Graphics g) : base(g) {
                
            }

            private UIBitmap(byte[] data) : base(data) {                
            }

            private UIBitmap(byte[] data, int width, int height) : base(data, width, height) {
            }

            public static UIBitmap FromGraphics(Graphics g) => new UIBitmap(g);

            public static UIBitmap FromData(byte[] data) => new UIBitmap(data);

            public static UIBitmap FromData(byte[] data, int width, int height) => new UIBitmap(data, width, height);
        }
    }
}
