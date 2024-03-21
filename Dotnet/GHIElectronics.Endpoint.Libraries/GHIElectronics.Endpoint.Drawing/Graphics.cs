using System;
using System.Collections;
using GHIElectronics.Endpoint.Drawing;
using System.Runtime.CompilerServices;
using SkiaSharp;
using System.Resources;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static GHIElectronics.Endpoint.Drawing.Graphics;
using System.Xml.Linq;

namespace GHIElectronics.Endpoint.Drawing {
    internal interface IGraphics : IDisposable {
        int Width { get; }
        int Height { get; }

        void Clear();
        void Flush();
        void Flush(int x, int y, int width, int height);

        uint GetPixel(int x, int y);
        void SetPixel(int x, int y, uint color);
        byte[] GetBitmap();

        byte[] GetBitmapRgb565();
        byte[] GetBitmap(int x, int y, int width, int height);

        void DrawLine(uint color, int thickness, int x0, int y0, int x1, int y1);
        void DrawRectangle(uint colorOutline, int thicknessOutline, int x, int y, int width, int height, int xCornerRadius, int yCornerRadius, uint colorGradientStart, int xGradientStart, int yGradientStart, uint colorGradientEnd, int xGradientEnd, int yGradientEnd, ushort opacity);
        void DrawEllipse(uint colorOutline, int thicknessOutline, int x, int y, int xRadius, int yRadius, uint colorGradientStart, int xGradientStart, int yGradientStart, uint colorGradientEnd, int xGradientEnd, int yGradientEnd, ushort opacity);
        void DrawText(string text, Font font, uint color, int x, int y);
        void DrawTextInRect(string text, int x, int y, int width, int height, uint dtFlags, Color color, Font font);
        void StretchImage(int xDst, int yDst, int widthDst, int heightDst, IGraphics image, int xSrc, int ySrc, int widthSrc, int heightSrc, ushort opacity);
        void DrawImage(int xDst, int yDst, IGraphics image, int xSrc, int ySrc, int width, int height, ushort opacity);
        void SetClippingRectangle(int x, int y, int width, int height);
        bool DrawTextInRect(ref string text, ref int xRelStart, ref int yRelStart, int x, int y, int width, int height, uint dtFlags, uint color, Font font);
        void RotateImage(int angle, int xDst, int yDst, IGraphics image, int xSrc, int ySrc, int width, int height, ushort opacity);
        void MakeTransparent(uint color);
        void TileImage(int xDst, int yDst, IGraphics image, int width, int height, ushort opacity);
        void Scale9Image(int xDst, int yDst, int widthDst, int heightDst, IGraphics image, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity);
    }

    public class Graphics : MarshalByRefObject, IDisposable {
        public int Width => this.surface.Width;
        public int Height => this.surface.Height;

        internal IGraphics surface;
        private bool disposed;
        internal bool callFromImage;
        //private IntPtr hdc;

        public GraphicsUnit PageUnit { get; } = GraphicsUnit.Pixel;

        public uint GetPixel(int x, int y) => this.surface.GetPixel(x, y);
        public void SetPixel(int x, int y, Color color) => this.surface.SetPixel(x, y, (uint)color.ToArgb());
        public byte[] GetBitmap() => this.surface.GetBitmap();
        public byte[] GetBitmap(int x, int y, int width, int height) => this.surface.GetBitmap(x, y, width, height);

        //private static IGraphics CreateSurface(byte[] buffer) => CreateSurface(buffer, BitmapImageType.Bmp);
        private static IGraphics CreateSurface(byte[] buffer, int width, int height) {
            if (buffer == null)
                throw new ArgumentNullException();

            return new Internal.Bitmap(buffer, width, height);
        }

        private static IGraphics CreateSurface(byte[] buffer) {
            if (buffer == null)
                throw new ArgumentNullException();

            return new Internal.Bitmap(buffer);
        }

        //private static IGraphics CreateSurface(byte[] buffer, int offset, int count, BitmapImageType type)
        //{
        //    if (buffer == null)
        //        throw new ArgumentNullException();

        //    return new Internal.Bitmap(buffer, offset, count, type);
        //}

        private static IGraphics CreateSurface(int width, int height) {
            if (width <= 0 || height <= 0)
                throw new IndexOutOfRangeException();

            return new Internal.Bitmap(width, height);
        }

        //internal Graphics(byte[] buffer) : this(Graphics.CreateSurface(buffer)) { }
        //internal Graphics(byte[] buffer, BitmapImageType type) : this(Graphics.CreateSurface(buffer, type)) { }
        //internal Graphics(byte[] buffer, int offset, int count, BitmapImageType type) : this(Graphics.CreateSurface(buffer, offset, count, type)) { }
        //internal Graphics(int width, int height) : this(width, height) { }
        internal Graphics(int width, int height) : this(Graphics.CreateSurface(width, height)) { }
        //internal Graphics(byte[] buffer, int width, int height) : this(buffer, width, height) { }
        internal Graphics(byte[] buffer, int width, int height) : this(Graphics.CreateSurface(buffer, width, height)) { }

        internal Graphics(IGraphics bmp) {
            this.surface = bmp; ;

        }

        internal Graphics(byte[] buffer) : this(Graphics.CreateSurface(buffer)) {


        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (!this.disposed && !this.callFromImage) {
                this.surface?.Dispose();
                this.surface = null;

                this.disposed = true;
            }
        }

        private uint ToFlags(StringFormat format, float height, bool ignoreHeight, bool truncateAtBottom) {
            var flags = 0U;

            if (ignoreHeight || height == 0.0) flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.IgnoreHeight;
            if (truncateAtBottom) flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TruncateAtBottom;

            if (format.FormatFlags != 0) throw new NotSupportedException();

            switch (format.Alignment) {
                case StringAlignment.Center: flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentCenter; break;
                case StringAlignment.Far: flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentRight; break;
                case StringAlignment.Near: flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentLeft; break;
                default: throw new ArgumentException();
            }

            switch (format.Trimming) {
                case StringTrimming.EllipsisCharacter: flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TrimmingCharacterEllipsis; break;
                case StringTrimming.EllipsisWord: flags |= (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap | (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TrimmingWordEllipsis; break;
                case StringTrimming.None:
                    break;

                case StringTrimming.EllipsisPath:
                case StringTrimming.Character:
                case StringTrimming.Word:
                    throw new NotSupportedException();

                default:
                    throw new ArgumentException();
            }

            return flags;
        }

        ~Graphics() => this.Dispose(false);

        public SizeF MeasureString(string text, Font font) {
            font.ComputeExtent(text, out var width, out var height);

            return new SizeF(width, height);
        }

        public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat) {
            font.ComputeTextInRect(text, out var width, out var height, 0, 0, (int)layoutArea.Width, (int)layoutArea.Height, this.ToFlags(stringFormat, layoutArea.Height, false, false));

            return new SizeF(width, height);
        }

        public void Clear() => this.surface.Clear();

        //public static Graphics FromHdc(IntPtr hdc)
        //{
        //    if (hdc == IntPtr.Zero) throw new ArgumentNullException(nameof(hdc));

        //    var res = Internal.Bitmap.GetSizeForLcdFromHdc(hdc, out var width, out var height);

        //    if (!res || width == 0 || height == 0) throw new InvalidOperationException("No screen configured.");

        //    return new Graphics(width, height, hdc);
        //}

        public static Graphics FromImage(Image image) {
            image.imgGfx.callFromImage = true;

            return image.imgGfx;
        }

        public static Graphics FromData(byte[] data) {
            // Endpoint TODO

            return new Graphics(data); ;
        }

        public delegate void OnFlushHandler(Graphics sender, byte[] data, int x, int y, int width, int height, int originalWidth);

        static public event OnFlushHandler OnFlushEvent;

        //public void Flush() {
        //    //if (this.hdc != IntPtr.Zero)
        //    //{
        //    //    this.surface.Flush(this.hdc, 0, 0, this.surface.Width, this.surface.Height);
        //    //}

        //    OnFlushEvent?.Invoke(this, this.surface.GetBitmapRgb565(), 0, 0, this.surface.Width, this.surface.Height, this.surface.Width); ;
        //}

        //Draws a portion of an image at a specified location.
        public void DrawImage(Image image, int x, int y, Rectangle srcRect, GraphicsUnit srcUnit) => this.surface.StretchImage(x, y, srcRect.Width, srcRect.Height, image.imgGfx.surface, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, 0xFF);

        //Draws the specified Image at the specified location and with the specified size.
        public void DrawImage(Image image, int x, int y, int width, int height) => this.surface.StretchImage(x, y, width, height, image.imgGfx.surface, 0, 0, image.Width, image.Height, 0xFF);

        //Draws the specified image, using its original physical size, at the location specified by a coordinate pair.
        public void DrawImage(Image image, int x, int y) => this.surface.StretchImage(x, y, image.Width, image.Height, image.imgGfx.surface, 0, 0, image.Width, image.Height, 0xFF);

        //Draws the specified portion of the specified Image at the specified location and with the specified size.
        public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit) => this.surface.StretchImage(destRect.X, destRect.Y, destRect.Width, destRect.Height, image.imgGfx.surface, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, 0xFF);

        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) {
            if (pen.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

            this.surface.DrawLine((uint)(pen.Color.value & 0x00FFFFFF), (int)pen.Width, x1, y1, x2, y2);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y) {
            if (brush is SolidBrush b) {
                if (b.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

                this.surface.DrawText(s, font, (uint)(b.Color.value & 0x00FFFFFF), (int)x, (int)y);
            }
            else {
                throw new NotSupportedException();
            }
        }

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle) => this.DrawString(s, font, brush, layoutRectangle, new StringFormat {
            Trimming = StringTrimming.EllipsisWord,
            Alignment = StringAlignment.Near
        });

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat format) {
            if (brush is SolidBrush b) {
                if (b.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

                this.surface.DrawTextInRect(s, (int)layoutRectangle.X, (int)layoutRectangle.Y, (int)layoutRectangle.Width, (int)layoutRectangle.Height, this.ToFlags(format, layoutRectangle.Height, false, false), b.Color, font);
            }
            else {
                throw new NotSupportedException();
            }
        }

        public void DrawEllipse(Pen pen, int x, int y, int width, int height) {
            if (pen.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

            var rgb = (uint)(pen.Color.ToArgb() & 0x00FFFFFF);

            width = (width - 1) / 2;
            height = (height - 1) / 2;

            x += width;
            y += height;

            this.surface.DrawEllipse(rgb, (int)pen.Width, x, y, width, height, (uint)Color.Transparent.value, x, y, (uint)Color.Transparent.value, x + width * 2, y + height * 2, 0x00);
        }

        public void DrawRectangle(Pen pen, int x, int y, int width, int height) {
            if (pen.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

            var rgb = (uint)(pen.Color.ToArgb() & 0x00FFFFFF);

            this.surface.DrawRectangle(rgb, (int)pen.Width, x, y, width, height, 0, 0, (uint)Color.Transparent.value, x, y, (uint)Color.Transparent.value, x + width, y + height, 0x00);
        }

        public void FillEllipse(Brush brush, int x, int y, int width, int height) {
            if (brush is SolidBrush b) {
                var rgb = (uint)(b.Color.ToArgb() & 0x00FFFFFF);

                width = (width - 1) / 2;
                height = (height - 1) / 2;

                x += width;
                y += height;

                this.surface.DrawEllipse(rgb, 0, x, y, width, height, rgb, x, y, rgb, x + width * 2, y + height * 2, b.Color.A);
            }
            else {
                throw new NotSupportedException();
            }
        }

        public void FillRectangle(Brush brush, int x, int y, int width, int height) {
            if (brush is SolidBrush b) {
                var rgb = (uint)(b.Color.ToArgb() & 0x00FFFFFF);

                this.surface.DrawRectangle(rgb, 0, x, y, width, height, 0, 0, rgb, x, y, rgb, x + width, y + height, b.Color.A);
            }
            else {
                throw new NotSupportedException();
            }
        }

        public void DrawImage(int xDst, int yDst, Image image, int xSrc, int ySrc, int width, int height, ushort opacity) => this.surface.DrawImage(xDst, yDst, image.imgGfx.surface, xSrc, ySrc, width, height, opacity);

        public void Flush(int x, int y, int width, int height) {
            //if (this.hdc != IntPtr.Zero)
            //{
            //    this.surface.Flush(this.hdc, x, y, width, height);
            //}

            // Note:
            // Proper way is this.surface.GetBitmap(x,y, width, height) but it will create a new buffer.
            // Keep same buffer for now.
            OnFlushEvent?.Invoke(this, this.surface.GetBitmapRgb565(), x, y, width, height, this.surface.Width); ;
        }

        public void SetClippingRectangle(int x, int y, int width, int height) => this.surface.SetClippingRectangle(x, y, width, height);
        public void DrawTextInRect(string text, int x, int y, int width, int height, DrawTextAlignment dtFlags, Color color, Font font) => this.surface.DrawTextInRect(text, x, y, width, height, (uint)dtFlags, color, font);
        public bool DrawTextInRect(ref string text, ref int xRelStart, ref int yRelStart, int x, int y, int width, int height, DrawTextAlignment dtFlags, Color color, Font font) => this.surface.DrawTextInRect(ref text, ref xRelStart, ref yRelStart, x, y, width, height, (uint)dtFlags, (uint)color.ToArgb(), font);
        public void RotateImage(int angle, int xDst, int yDst, Image image, int xSrc, int ySrc, int width, int height, ushort opacity) {
            if (image == null) throw new ArgumentNullException("image null.");

            if ((xSrc + width > image.Width) || (ySrc + height > image.Height))
                throw new ArgumentOutOfRangeException();

            this.surface.RotateImage(angle, xDst, yDst, image.imgGfx.surface, xSrc, ySrc, width, height, opacity);
        }
        public void MakeTransparent(Color color) => this.surface.MakeTransparent((uint)color.ToArgb());
        public void StretchImage(int xDst, int yDst, int widthDst, int heightDst, Image image, int xSrc, int ySrc, int widthSrc, int heightSrc, ushort opacity) => this.surface.StretchImage(xDst, yDst, widthDst, heightDst, image.imgGfx.surface, xSrc, ySrc, widthSrc, heightSrc, opacity);
        public void TileImage(int xDst, int yDst, Image image, int width, int height, ushort opacity) => this.surface.TileImage(xDst, yDst, image.imgGfx.surface, width, height, opacity);
        public void Scale9Image(int xDst, int yDst, int widthDst, int heightDst, Image image, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity) => this.surface.Scale9Image(xDst, yDst, widthDst, heightDst, image.imgGfx.surface, leftBorder, topBorder, rightBorder, bottomBorder, opacity);

        //
        // These have to be kept in sync with the CLR_GFX_Bitmap::c_DrawText_ flags.
        //
        public enum DrawTextAlignment : uint {
            None = 0x00000000,
            WordWrap = 0x00000001,
            TruncateAtBottom = 0x00000004,
            Ellipsis = 0x00000008,
            IgnoreHeight = 0x00000010,
            AlignmentLeft = 0x00000000,
            AlignmentCenter = 0x00000002,
            AlignmentRight = 0x00000020,
            AlignmentMask = 0x00000022,
            TrimmingNone = 0x00000000,
            TrimmingWordEllipsis = 0x00000008,
            TrimmingCharacterEllipsis = 0x00000040,
            TrimmingMask = 0x00000048,
        }

    }

    namespace Internal {
        //The name and namespace of this must match the definition in c_TypeIndexLookup in TypeSystem.cpp and ResourceManager.GetObject
        internal class Bitmap : MarshalByRefObject, IDisposable, IGraphics {
#pragma warning disable CS0169 // The field is never used
            IntPtr implPtr;
#pragma warning restore CS0169 // The field is never used

            SKBitmap skBitmap;
            SKImageInfo skInfo;
            SKCanvas skCanvas;

            public SKBitmap SkBitmap => this.skBitmap;
            public void Dispose() {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~Bitmap() => this.Dispose(false);

            public int Width { get; private set; }// { [MethodImpl(MethodImplOptions.InternalCall)] get; }
            public int Height { get; private set; }// { [MethodImpl(MethodImplOptions.InternalCall)] get; }

            public byte[] GetBitmap(int x, int y, int width, int height) {
                if ((x < 0) || (y < 0) || (x + width > this.Width) || (y + height > this.Height))
                    throw new ArgumentOutOfRangeException();

                return this.NativeGetBitmap(x, y, width, height, this.Width);
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            //public static extern bool GetSizeForLcdFromHdc(IntPtr hdc, out int width, out int height);

            //[MethodImplAttribute(MethodImplOptions.InternalCall)]
            //private extern void CreateInstantFromResources(uint buffer, uint size, uint assembly);

            //[MethodImpl(MethodImplOptions.InternalCall)]
            //public extern Bitmap(byte[] imageData, BitmapImageType type);

            public Bitmap(byte[] data) {
                this.skBitmap = SKBitmap.Decode(data);
                this.skCanvas = new SKCanvas(this.skBitmap);

                this.Width = this.skBitmap.Width;
                this.Height = this.skBitmap.Height;

            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            //public extern Bitmap(byte[] imageData, int offset, int count, BitmapImageType type);

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public Bitmap(int width, int height) {

                this.skBitmap = new SKBitmap(width, height); ;// SKImageInfo.PlatformColorType, SKAlphaType.Premul); ;

                this.skCanvas = new SKCanvas(this.skBitmap);

                this.Width = this.skBitmap.Width;
                this.Height = this.skBitmap.Height;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public Bitmap(byte[] data, int width, int height) {

                this.skInfo = new SKImageInfo(width, height); // width and height of rect

                this.skBitmap = SKBitmap.Decode(data, this.skInfo);

                this.skCanvas = new SKCanvas(this.skBitmap);

                this.Width = this.skBitmap.Width;
                this.Height = this.skBitmap.Height;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Clear() {
                this.skCanvas.Clear(SKColors.Black); ;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Dispose(bool disposing) {
                // TODO
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Flush() {
                // TODO
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void DrawText(string text, Font font, uint color, int x, int y) {
                using (var p = new SKPaint()) {
                    p.Color = color;
                    p.Style = SKPaintStyle.Stroke;

                    this.skCanvas.DrawText(text, x, y, p);
                }
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void DrawImage(int xDst, int yDst, Bitmap bitmap, int xSrc, int ySrc, int width, int height, ushort opacity) {

                var img = bitmap.GetBitmap(xSrc, ySrc, width, height);

                var info = new SKImageInfo(width, height); // width and height of rect

                var sk_img = SKBitmap.Decode(img, info);

                this.skCanvas.DrawBitmap(sk_img, xDst, yDst);
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void DrawEllipse(uint colorOutline, int thicknessOutline, int x, int y, int xRadius, int yRadius, uint colorGradientStart, int xGradientStart, int yGradientStart, uint colorGradientEnd, int xGradientEnd, int yGradientEnd, ushort opacity) {

                using (var p = new SKPaint()) {
                    p.Color = colorOutline;
                    p.Style = SKPaintStyle.Stroke;
                    p.StrokeWidth = thicknessOutline;

                    this.skCanvas.DrawOval(x, y, xRadius, yRadius, p);
                }


            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void DrawLine(uint color, int thickness, int x0, int y0, int x1, int y1) {
                using (var p = new SKPaint()) {
                    p.Color = color;
                    p.Style = SKPaintStyle.Stroke;
                    p.StrokeWidth = thickness;

                    this.skCanvas.DrawLine(x0, y0, x1, y1, p);
                }
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void DrawRectangle(uint colorOutline, int thicknessOutline, int x, int y, int width, int height, int xCornerRadius, int yCornerRadius, uint colorGradientStart, int xGradientStart, int yGradientStart, uint colorGradientEnd, int xGradientEnd, int yGradientEnd, ushort opacity) {

                var rect = SKRect.Create(x, y, width, height);

                var paint = new SKPaint();




                if (colorGradientStart == colorGradientEnd) {
                    //paint.Style = SKPaintStyle.Fill;
                    //paint.Color = colorGradientStart;

                    paint.Shader = SKShader.CreateLinearGradient(
                                new SKPoint(0, 0),
                                new SKPoint(width, height),
                                new SKColor[] { new SKColor(colorGradientStart),
                                                new SKColor(colorGradientEnd) },
                                null,
                                SKShaderTileMode.Clamp);
                }

                this.skCanvas.DrawRect(rect, paint);



            }

            public void DrawTextInRect(string text, int x, int y, int width, int height, uint dtFlags, Color color, Font font) {
                var xRelStart = 0;
                var yRelStart = 0;

                this.DrawTextInRect(ref text, ref xRelStart, ref yRelStart, x, y, width, height, dtFlags, (uint)(color.value & 0x00FFFFFF), font);
            }

            //public void DrawEllipse(Color colorOutline, int x, int y, int xRadius, int yRadius) => DrawEllipse(colorOutline, 1, x, y, xRadius, yRadius, Color.Black, 0, 0, Color.Black, 0, 0, OpacityOpaque);
            //
            //public void DrawImage(int xDst, int yDst, Graphics bitmap, int xSrc, int ySrc, int width, int height) => DrawImage(xDst, yDst, bitmap, xSrc, ySrc, width, height, OpacityOpaque);

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Flush(int x, int y, int width, int height) {
                // EP TODO
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void SetClippingRectangle(int x, int y, int width, int height) {
                // EP TODO
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]

            private void CountCharactersInWidth(string text, int maxChars, int width, ref int totWidth, bool fWordWrap, ref string strNext, ref int numChars, Font font) {
                //var i = 0;
                var breakPoint = string.Empty;
                var lastChar = '\0';
                var breakWidth = 0;
                var str = text;
                var num = 0;
                var breakIndex = 0;

                while (maxChars != 0) {

                    var c = str[0];
                    var fNewLine = (c == '\n');
                    var chrWidth = font.AverageWidth;

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
            public bool DrawTextInRect(ref string text, ref int xRelStart, ref int yRelStart, int x, int y, int width, int height, uint dtFlags, uint color, Font font) {

                var textBlob = SKTextBlob.Create(text, font.SkFont);
                font.SkPaint.Color = color;

                //xRelStart = (int)textBlob.Bounds.Left;
                //yRelStart = (int)textBlob.Bounds.Top;


                //font.SkFont.GetFontMetrics(out var skFontMetric);
                //var alignment = (DrawTextAlignment)(dtFlags & (uint)DrawTextAlignment.AlignmentMask);

                //switch (alignment) {
                //    case DrawTextAlignment.AlignmentLeft:
                //        x -= width / 2;
                //        break;

                //    case DrawTextAlignment.AlignmentRight:
                //        x += width / 2;
                //        break;

                //    case DrawTextAlignment.AlignmentCenter:
                //        break;

                //}

               
                this.skCanvas.DrawText(textBlob, x, y + font.Height, font.SkPaint);

                //var fFirstLine = true;
                //var totWidth = 0;
                //var szTextNext = string.Empty;
                //var szEllipsis = "...";
                //var num = 0;
                //var ellipsisWidth = 0;
                //var fDrawEllipsis = false;

                //var alignment = dtFlags & (uint)DrawTextAlignment.AlignmentMask;
                //var trimming = dtFlags & (uint)DrawTextAlignment.TrimmingMask;


                //var nHeight = font.Height;
                //var nSkip = font.ExternalLeading;

                //var dHeight = height - yRelStart;
                //var dHeightLine = nHeight + nSkip;
                //var cLineAvailable = dHeight + nSkip + (((dtFlags & (uint)DrawTextAlignment.TruncateAtBottom) != 0) ? dHeightLine - 1 : 0) / dHeightLine;

                //var renderWidth = 0;
                //var renderHeight = yRelStart;

                //var fWordWrap = ((uint)(dtFlags & (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap) != 0);

                //var szText = text;



                //while (((dtFlags & (uint)DrawTextAlignment.IgnoreHeight) != 0) || --cLineAvailable >= 0) {
                //    var szTextLast = szText;

                //    if (!fFirstLine) {
                //        xRelStart = 0;
                //        yRelStart += dHeightLine;
                //    }

                //    font.CountCharactersInWidth(szText, -1, width - xRelStart, ref totWidth, fWordWrap, ref szTextNext, ref num);

                //    if ((xRelStart + totWidth) > renderWidth) renderWidth = xRelStart + totWidth;
                //    renderHeight += dHeightLine;

                //    if ((trimming != (uint)DrawTextAlignment.TrimmingNone) && (cLineAvailable == 0) && szTextNext[0] != 0) {

                //        font.CountCharactersInWidth(szEllipsis, -1, 65536, ref ellipsisWidth, fWordWrap, ref szTextNext, ref num);
                //        font.CountCharactersInWidth(szText, -1, width - xRelStart - ellipsisWidth, ref totWidth, (trimming == (uint)DrawTextAlignment.TrimmingWordEllipsis), ref szTextNext, ref num);

                //        totWidth += ellipsisWidth;
                //        fDrawEllipsis = true;
                //    }

                //    if (alignment == (uint)DrawTextAlignment.AlignmentCenter) {
                //        xRelStart = (width - totWidth + xRelStart) / 2;
                //    }
                //    else if (alignment == (uint)DrawTextAlignment.AlignmentRight) {
                //        xRelStart = width - totWidth;
                //    }

                //    if (true) {
                //        this.skCanvas.DrawText(textBlob, x + xRelStart, y + yRelStart, font.SkPaint);
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

                //    if (szTextLast.CompareTo(szText) == 0 || szText == null || szText.Length == 0) break; // No progress made or finished, bail out...

                //    fFirstLine = false;

                //}

                return true;
            }


            public static SKBitmap Rotate(SKBitmap bitmap, int angle) {

                var rotated = new SKBitmap(bitmap.Height, bitmap.Width);

                using (var surface = new SKCanvas(rotated)) {
                    surface.Translate(rotated.Width, 0);
                    surface.RotateDegrees(angle);
                    surface.DrawBitmap(bitmap, 0, 0);
                }

                return rotated;

            }
            ///[MethodImpl(MethodImplOptions.InternalCall)]
            public void RotateImage(int angle, int xDst, int yDst, Bitmap bitmap, int xSrc, int ySrc, int width, int height, ushort opacity) {
                var rotated = Rotate(bitmap.SkBitmap, angle);

                this.skCanvas.DrawBitmap(rotated, xDst, yDst);

                //TODO  int xSrc, int ySrc, int width, int height
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void MakeTransparent(uint color) {
                this.skCanvas.Clear(color); ;

                //TODO
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void StretchImage(int xDst, int yDst, Bitmap bitmap, int width, int height, ushort opacity) {

                var info = new SKImageInfo(width, height);
                this.skCanvas.DrawBitmap(bitmap.SkBitmap.Resize(info, SKFilterQuality.Low), xDst, yDst);
            }


            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void SetPixel(int xPos, int yPos, uint color) {
                this.skBitmap.SetPixel(xPos, yPos, color); ;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public uint GetPixel(int xPos, int yPos) {
                var c = this.skBitmap.GetPixel(xPos, yPos);

                return (uint)((c.Red << 24) | (c.Green << 16) | (c.Blue << 8) | (c.Alpha << 0)); ;

            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public byte[] GetBitmap() {
                return this.skBitmap.Copy(SKColorType.Rgba8888).Bytes; ;
            }

            public byte[] GetBitmapRgb565() {
                return this.skBitmap.Copy(SKColorType.Rgb565).Bytes; ;
            }
            //[MethodImpl(MethodImplOptions.InternalCall)]
            private byte[] NativeGetBitmap(int x, int y, int width, int height, int originalWidth) {
                var buf = this.GetBitmap();

                var ret = new byte[width * height * 4];
                var idx = 0;

                for (var y1 = y; y1 < y + height; y1++) {
                    for (var x1 = y; x1 < x + width; x1 += 4) {
                        ret[idx + 0] = buf[(y1 * originalWidth + x1) * 4 + 0];
                        ret[idx + 1] = buf[(y1 * originalWidth + x1) * 4 + 1];
                        ret[idx + 2] = buf[(y1 * originalWidth + x1) * 4 + 2];
                        ret[idx + 3] = buf[(y1 * originalWidth + x1) * 4 + 3];

                        idx += 4;
                    }
                }

                return ret;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            //public extern void StretchImage(int xDst, int yDst, int widthDst, int heightDst, Bitmap bitmap, int xSrc, int ySrc, int widthSrc, int heightSrc, ushort opacity);

            public void StretchImage(int xDst, int yDst, int widthDst, int heightDst, IGraphics bitmap, int xSrc, int ySrc, int widthSrc, int heightSrc, ushort opacity) {
                if (bitmap is Bitmap b)
                    this.StretchImage(xDst, yDst, widthDst, heightDst, b, xSrc, ySrc, widthSrc, heightSrc, opacity);
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void TileImage(int xDst, int yDst, Bitmap bitmap, int width, int height, ushort opacity) {



                this.StretchImage(xDst, yDst, bitmap, width, height, opacity); ;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Scale9Image(int xDst, int yDst, int widthDst, int heightDst, Bitmap bitmap, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity) {

                this.StretchImage(xDst, yDst, bitmap, widthDst, heightDst, opacity); ;
            }

            public void DrawImage(int xDst, int yDst, IGraphics bitmap, int xSrc, int ySrc, int width, int height, ushort opacity) {
                if (bitmap is Bitmap b)
                    this.DrawImage(xDst, yDst, b, xSrc, ySrc, width, height, opacity);
            }

            public void RotateImage(int angle, int xDst, int yDst, IGraphics bitmap, int xSrc, int ySrc, int width, int height, ushort opacity) {
                if (bitmap is Bitmap b)
                    this.RotateImage(angle, xDst, yDst, b, xSrc, ySrc, width, height, opacity);
            }

            public void TileImage(int xDst, int yDst, IGraphics bitmap, int width, int height, ushort opacity) {
                if (bitmap is Bitmap b)
                    this.TileImage(xDst, yDst, b, width, height, opacity);
            }

            public void Scale9Image(int xDst, int yDst, int widthDst, int heightDst, IGraphics bitmap, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity) {
                if (bitmap is Bitmap b)
                    this.Scale9Image(xDst, yDst, widthDst, heightDst, b, leftBorder, topBorder, rightBorder, bottomBorder, opacity);
            }
        }
    }
}
