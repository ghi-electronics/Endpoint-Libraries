using System.Runtime.CompilerServices;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.UI")]

namespace GHIElectronics.Endpoint.Drawing {
    public enum GraphicsUnit {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6
    }

    //The name and namespace of this must match the definition in c_TypeIndexLookup in TypeSystem.cpp
    public sealed class Font : MarshalByRefObject, IDisposable {
#pragma warning disable CS0169 // The field is never used
        IntPtr implPtr;
        IntPtr dataPtr;
#pragma warning restore CS0169 // The field is never used

        // Must keep in sync with CLR_GFX_Font::c_DefaultKerning
        private const int DefaultKerning = 1024;

        SKFont skFont;

        public SKFont SkFont => this.skFont;


        public float Size { get => this.skFont.Size; set => this.skFont.Size = value; }
        private Font() { }

        public Font(byte[] data) => new Font(data, 0, data.Length);

        public Font(byte[] data, int offset, int count) {
            //if (data == null) throw new ArgumentNullException(nameof(data));
            //if (offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(data));

            //this.CreateInstantFromBuffer(data, offset, count);

            Stream stream = new MemoryStream(data, offset, count);
            var tf = SKTypeface.FromStream(stream);

            this.skFont = new SKFont {
                Typeface = tf
            };
        }

        //public Font(string familyName, float emSize) {
        //    var sz = (int)emSize;

        //    this.IsGHIMono8x5 = familyName == "GHIMono8x5" && (sz % 8) == 0 ? true : throw new NotSupportedException();
        //    this.Size = sz;
        //}

        ~Font() => this.Dispose();

        //internal int Size { get; }
        //internal bool IsGHIMono8x5 { get; }

        //public object Clone() => throw new NotImplementedException();

        //[MethodImpl(MethodImplOptions.InternalCall)]
        //private extern int CharWidth(char c);

        public GraphicsUnit Unit => GraphicsUnit.Pixel;

        public int Height {
            get {
                return (int)this.skFont.Metrics.XHeight; ; ;

            }

        }

        //internal extern int AverageWidth { [MethodImpl(MethodImplOptions.InternalCall)] get; }
        internal int MaxWidth {
            get {
                return (int)this.skFont.Metrics.MaxCharacterWidth; ; ;

            }

        }

        internal int Ascent {
            get {
                return (int)this.skFont.Metrics.Ascent; ; ;

            }

        }
        internal int Descent {
            get {
                return (int)this.skFont.Metrics.Descent; ; ;

            }

        }

        internal int InternalLeading {
            get {
                return (int)this.skFont.Metrics.Leading; ; ;

            }

        }
        internal int ExternalLeading {
            get {
                return (int)this.skFont.Metrics.Leading; ; ;

            }

        }

        //[MethodImpl(MethodImplOptions.InternalCall)]
        private void ComputeExtent(string text, out int width, out int height, int kerning) {
            var nTotWidth = 0;

            for (var i = 0; i < text.Length; i++) {

                using (var p = new SKPaint {
                    Typeface = this.skFont.Typeface
                }) {

                    var ch = text[i].ToString();

                    var nOffset = nTotWidth + p.MeasureText(text[i].ToString());

                    nTotWidth = (int)(nOffset + p.StrokeWidth);
                }



            }

            width = nTotWidth;
            height = this.Height;

        }

        //[MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal void ComputeTextInRect(string text, out int renderWidth, out int renderHeight, int xRelStart, int yRelStart, int availableWidth, int availableHeight, uint dtFlags) {
            using (var p = new SKPaint {
                Typeface = this.skFont.Typeface
            }) {

                renderWidth = (int)p.MeasureText(text.ToString()) + xRelStart;

                renderHeight = this.Height - yRelStart;
            }
        }

        public void ComputeExtent(string text, out int width, out int height) => this.ComputeExtent(text, out width, out height, DefaultKerning);
        public void ComputeTextInRect(string text, out int renderWidth, out int renderHeight) => this.ComputeTextInRect(text, out renderWidth, out renderHeight, 0, 0, 65536, 0, (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.IgnoreHeight | (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap);
        public void ComputeTextInRect(string text, out int renderWidth, out int renderHeight, int availableWidth) => this.ComputeTextInRect(text, out renderWidth, out renderHeight, 0, 0, availableWidth, 0, (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.IgnoreHeight | (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap);

        //[MethodImplAttribute(MethodImplOptions.InternalCall)]
        //private extern void CreateInstantFromResources(uint buffer, uint size, uint assembly);

        //[MethodImplAttribute(MethodImplOptions.InternalCall)]
        //private extern void CreateInstantFromBuffer(byte[] data, int offset, int size);

        //[MethodImpl(MethodImplOptions.InternalCall)]
        public void Dispose() {

        }
    }
}


