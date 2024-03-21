using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;
using static GHIElectronics.Endpoint.Drawing.Graphics;

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
        SKPaint skPaint;

        public SKFont SkFont => this.skFont;
        public SKPaint SkPaint => this.skPaint;

        private SKRect textBounds;


        //public float Size { get => this.skFont.Size; set => this.skFont.Size = value; }
        public Font() : this(22) {

        }

        public Font(int size) {
            this.skFont = new SKFont {
                Size = size,

            };

            this.skPaint = new SKPaint {
                TextAlign = SKTextAlign.Center,
                TextSize = this.skFont.Size,
            };

            this.textBounds = new SKRect();

            this.skPaint.MeasureText("a", ref this.textBounds);
        }

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

            this.skPaint = new SKPaint {
                TextAlign = SKTextAlign.Center,
                TextSize = this.skFont.Size,
            };

            //this.textBounds = new SKRect();

            //this.skPaint.MeasureText("B", ref this.textBounds);
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
                //var textBounds = new SKRect();

                //paint.MeasureText(text, ref textBounds);
                //return (int)16; ;// (int)Math.Ceiling(Math.Abs(this.skFont.Metrics.Top)); ;// (int)Math.Ceiling(this.skFont.Metrics.XHeight); ; ;

                return (int)Math.Ceiling(this.skFont.Metrics.CapHeight); ;

            }

        }

        internal int AverageWidth {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.AverageCharacterWidth); ;

            }

        }
        internal int MaxWidth {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.MaxCharacterWidth); ; ;

            }

        }

        internal int Ascent {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.Ascent); ; ;

            }

        }
        internal int Descent {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.Descent); ; ;

            }

        }

        internal int InternalLeading {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.Leading); ; ;

            }

        }
        internal int ExternalLeading {
            get {
                return (int)Math.Ceiling(this.skFont.Metrics.Leading); ; ;

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

        public void CountCharactersInWidth(string text, int maxChars, int width, ref int totWidth, bool fWordWrap, ref string strNext, ref int numChars) {
            //var i = 0;
            var breakPoint = string.Empty;
            var lastChar = '\0';
            var breakWidth = 0;
            var str = text;
            var num = 0;
            var breakIndex = 0;

            while (maxChars != 0) {
                if (str == null || str == string.Empty || str.Length == 0)
                    break;
                var c = str[0];
                var fNewLine = (c == '\n');
                var chrWidth = this.AverageWidth;

                if (fNewLine || chrWidth > width) {
                    if (fWordWrap) {
                        if (c == ' ') {
                            break;
                        }

                        if (breakPoint != string.Empty) {
                            if ((fNewLine && lastChar == ' ') ||
                                (!fNewLine && c != ' ')) {
                                totWidth = breakWidth;
                                str = breakPoint;
                                num = breakIndex;
                            }
                        }
                    }

                    break;
                }

                if (c == ' ') {
                    if (lastChar != c) {
                        // Break to the left of a space, since the string wouldn't
                        // be properly centered with an extra space at the end of a line
                        //
                        breakIndex = num;
                        breakPoint = str;
                        breakWidth = totWidth;
                    }
                }

                width -= chrWidth;
                totWidth += chrWidth;

                str = str.Substring(1);

                maxChars--;
                num++;

                if (c == '-') {
                    if (lastChar != ' ') // e.g., "...foo -1000"
                    {
                        // Break to the right for a hyphen so that it stays part
                        // of the current line
                        //
                        breakIndex = num;
                        breakPoint = str;
                        breakWidth = totWidth;
                    }
                }

                lastChar = c;


            }

            strNext = str;
            numChars = num;
        }

        //[MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal void ComputeTextInRect(string text, out int renderWidth, out int renderHeight, int xRelStart, int yRelStart, int availableWidth, int availableHeight, uint dtFlags) {


            //for (var i = 0;i < text.Length; i++) {
            //    var w = this.skFont.MeasureText(text, this.skPaint);

            //}
            var paint = new SKPaint {
                TextSize = this.SkFont.Size,
                TextAlign = SKTextAlign.Center,
                
            };

            var textBounds = new SKRect();

            paint.MeasureText(text, ref textBounds);


            renderWidth = (int)Math.Ceiling(textBounds.Width) + xRelStart;

            if (renderWidth > availableWidth) {
                renderWidth = availableWidth;
            }


            renderHeight = (int)Math.Ceiling(textBounds.Height) - yRelStart;

            if (renderHeight > availableHeight) {
                renderHeight = availableHeight;
            }


            //var height = availableHeight;
            //var width = availableWidth;
            //var fFirstLine = true;
            //var totWidth = 0;
            //var szTextNext = string.Empty;
            //var szEllipsis = "...";
            //var num = 0;
            //var ellipsisWidth = 0;
            ////var fDrawEllipsis = false;

            //var alignment = dtFlags & (uint)DrawTextAlignment.AlignmentMask;
            //var trimming = dtFlags & (uint)DrawTextAlignment.TrimmingMask;


            //var nHeight = this.Height;
            //var nSkip = this.ExternalLeading;

            //var dHeight = height - yRelStart;
            //var dHeightLine = nHeight + nSkip;
            //var cLineAvailable = dHeight + nSkip + (((dtFlags & (uint)DrawTextAlignment.TruncateAtBottom) != 0) ? dHeightLine - 1 : 0) / dHeightLine;

            //renderWidth = 0;
            //renderHeight = yRelStart;

            //var fWordWrap = ((uint)(dtFlags & (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap) != 0);

            //var szText = text;



            //while (((dtFlags & (uint)DrawTextAlignment.IgnoreHeight) != 0) || --cLineAvailable >= 0) {
            //    var szTextLast = szText;

            //    if (!fFirstLine) {
            //        xRelStart = 0;
            //        yRelStart += dHeightLine;
            //    }

            //    this.CountCharactersInWidth(szText, -1, width - xRelStart, ref totWidth, fWordWrap, ref szTextNext, ref num);

            //    if ((xRelStart + totWidth) > renderWidth) renderWidth = xRelStart + totWidth;
            //    renderHeight += dHeightLine;

            //    if ((trimming != (uint)DrawTextAlignment.TrimmingNone) && (cLineAvailable == 0) && szTextNext[0] != 0) {

            //        this.CountCharactersInWidth(szEllipsis, -1, 65536, ref ellipsisWidth, fWordWrap, ref szTextNext, ref num);
            //        this.CountCharactersInWidth(szText, -1, width - xRelStart - ellipsisWidth, ref totWidth, (trimming == (uint)DrawTextAlignment.TrimmingWordEllipsis), ref szTextNext, ref num);

            //        totWidth += ellipsisWidth;
            //        //fDrawEllipsis = true;
            //    }

            //    if (alignment == (uint)DrawTextAlignment.AlignmentCenter) {
            //        xRelStart = (width - totWidth + xRelStart) / 2;
            //    }
            //    else if (alignment == (uint)DrawTextAlignment.AlignmentRight) {
            //        xRelStart = width - totWidth;
            //    }


            //    szText = szTextNext;

            //    if (szText == null || szText.Length == 0)
            //        break;

            //    if (fWordWrap && szText[0] == ' ')
            //        szText = szText.Substring(1);

            //    if (szText == null || szText.Length == 0)
            //        break;

            //    if (szText[0] == '\n')
            //        szText = szText.Substring(1); // Eat just one new line.

            //    if (szText == null || szText.Length == 0 || szTextLast.CompareTo(szText) == 0 ) break; // No progress made or finished, bail out...

            //    fFirstLine = false;

            //}

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


