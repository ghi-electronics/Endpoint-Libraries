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
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace GHIElectronics.Endpoint.Drawing {

    internal class GFX_Rect {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public int Width() => this.right - this.left + 1;
        public int Height() => this.bottom - this.top + 1;
    };
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

        public static Graphics FromData(byte[] data, int width, int height) {
            // Endpoint TODO

            return new Graphics(data, width, height); ;
        }

        //public static Graphics FromFile(string file) {
        //    // Endpoint TODO

        //    byte[] data;
        //    using (var fs = File.OpenRead(file)) {
        //       data = new byte[fs.Length];

        //        fs.Read(data, 0, data.Length);
        //    }

        //    if (data != null) {
        //        return new Graphics(data); ;
        //    }

        //    return null;

        //}

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

            this.surface.DrawLine((uint)(pen.Color.Value & 0x00FFFFFF), (int)pen.Width, x1, y1, x2, y2);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y) {
            if (brush is SolidBrush b) {
                if (b.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

                this.surface.DrawText(s, font, (uint)(b.Color.Value & 0x00FFFFFF), (int)x, (int)y);
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

            this.surface.DrawEllipse(rgb, (int)pen.Width, x, y, width, height, (uint)Color.Transparent.Value, x, y, (uint)Color.Transparent.Value, x + width * 2, y + height * 2, 0x00);
        }

        public void DrawRectangle(Pen pen, int x, int y, int width, int height) {
            if (pen.Color.A != 0xFF) throw new NotSupportedException("Alpha not supported.");

            var rgb = (uint)(pen.Color.ToArgb() & 0x00FFFFFF);

            this.surface.DrawRectangle(rgb, (int)pen.Width, x, y, width, height, 0, 0, (uint)Color.Transparent.Value, x, y, (uint)Color.Transparent.Value, x + width, y + height, 0x00);
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
                if (this.skCanvas != null) { this.skCanvas.Dispose(); this.skCanvas = null; }

                if (this.skBitmap != null) { this.skBitmap.Dispose(); this.skBitmap = null; }

            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Flush() {
                // TODO
                this.Flush(0, 0, this.Width, this.Height); ;
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
                if (bitmap == null)
                    throw new ArgumentException("bitmap is null");

                if (width > bitmap.Width || height > bitmap.Height || xSrc < 0 || ySrc < 0)
                    throw new ArgumentException("invalid argument");


                if (xSrc != 0 || ySrc != 0 || bitmap.Width != width || bitmap.Height != height) {
                    var sk_img = new SKBitmap(width, height);

                    // EP TODO optimize
                    for (var y = ySrc; y < ySrc + height; y++) {
                        for (var x = xSrc; x < xSrc + width; x++) {
                            var c = bitmap.GetPixel(x, y);

                            var a = opacity < 255 ? (byte)(opacity) : (byte)(c >> 24);
                            var r = (byte)(c >> 16);
                            var g = (byte)(c >> 8);
                            var b = (byte)(c >> 0);

                            var sk_color = new SKColor(r, g, b, a);
                            sk_img.SetPixel(x, y, sk_color);
                        }
                    }

                    this.skCanvas.DrawBitmap(sk_img, xDst, yDst);
                }
                else {

                    this.skCanvas.DrawBitmap(bitmap.SkBitmap, xDst, yDst); ;
                }
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

                var c_s_a = (byte)(colorGradientStart >> 24);
                var c_s_r = (byte)(colorGradientStart >> 16);
                var c_s_g = (byte)(colorGradientStart >> 8);
                var c_s_b = (byte)(colorGradientStart >> 0);

                var c_e_a = (byte)(colorGradientEnd >> 24);
                var c_e_r = (byte)(colorGradientEnd >> 16);
                var c_e_g = (byte)(colorGradientEnd >> 8);
                var c_e_b = (byte)(colorGradientEnd >> 0);


                var colors = new SKColor[] {
                    new SKColor(c_s_r, c_s_g, c_s_b, c_s_a),
                    new SKColor(c_e_r, c_e_g, c_e_b, c_e_a)
                };


                paint.Shader = SKShader.CreateLinearGradient(
                                 new SKPoint(rect.Left, rect.Top),
                                 new SKPoint(rect.Right, rect.Bottom),
                                 colors,
                                 new float[] { 0, 0.5f },

                                 SKShaderTileMode.Clamp);

                this.skCanvas.DrawRect(rect, paint);



            }

            public void DrawTextInRect(string text, int x, int y, int width, int height, uint dtFlags, Color color, Font font) {
                var xRelStart = 0;
                var yRelStart = 0;

                this.DrawTextInRect(ref text, ref xRelStart, ref yRelStart, x, y, width, height, dtFlags, (uint)color.Value, font);
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
                //this.skCanvas.Save();
                //var rect = new SKRect(x, y, width, height);
                //this.skCanvas.ClipRect(rect);

                //if (this.skCanvas.DeviceClipBounds.Width != 480) {
                //    while (true) {
                //        Thread.Sleep(1000);
                //    }
                //}



            }

            //[MethodImpl(MethodImplOptions.InternalCall)]


            public bool DrawTextInRect(ref string text, ref int xRelStart, ref int yRelStart, int x, int y, int width, int height, uint dtFlags, uint color, Font skFont) {


                //font.SkPaint.Color = color;
                //var textBounds = new SKRect();

                using (var skPaint = new SKPaint {
                    TextSize = skFont.SkFont.Size,
                    TextAlign = SKTextAlign.Center,
                    Color = color,

                }) {

                    var textBounds = new SKRect();

                    width += (int)skPaint.MeasureText(".", ref textBounds);

                    var s = string.Empty;
                    for (var i = 0; i < text.Length; i++) {
                        s += text[i];


                        var w = skPaint.MeasureText(s, ref textBounds);
                        if (w > width) {
                            var len = s.Length;

                            if (len > 3) {
                                s = s.Substring(0, len - 3);
                                s += "...";
                            }

                            break;
                        }

                    }

                    var textBlob = SKTextBlob.Create(s, skFont.SkFont);
                    this.skCanvas.DrawText(textBlob, x + xRelStart, y + yRelStart + skFont.Height, skPaint);
                }

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

                var new_bitmap = new Bitmap(width, height);

                for (var y = ySrc; y < ySrc + height; y++) {
                    for (var x = xSrc; x < xSrc + width; x++) {
                        var p = bitmap.GetPixel(x, y);

                        if (opacity < 255) {
                            p &= ~0xFF000000;
                            p |= (uint)((byte)(opacity) << 24);
                        }


                        new_bitmap.SetPixel(x - xSrc, y - ySrc, p);
                    }
                }


                var rotated = Rotate(bitmap.SkBitmap, angle);

                this.skCanvas.DrawBitmap(rotated, xDst, yDst);

                //TODO  int xSrc, int ySrc, int width, int height
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void MakeTransparent(uint color) {


                var pixelCount = this.skBitmap.Width * this.skBitmap.Height;
                var colors = new SKColor[pixelCount];


                var original_pixel = this.skBitmap.Pixels;

                for (var n = 0; n < pixelCount; n++) {
                    var c = original_pixel[n];
                    // Red
                    var r_trans = (byte)(color << 16);

                    var p_r = 1 - (r_trans / 255.0);

                    var r = (byte)(255 - p_r * (255 - c.Red));

                    // Green
                    var g_trans = (byte)(color << 8);

                    var p_g = 1 - (g_trans / 255.0);

                    var g = (byte)(255 - p_g * (255 - c.Green));

                    // Blue
                    var b_trans = (byte)(color << 0);

                    var p_b = 1 - (b_trans / 255.0);

                    var b = (byte)(255 - p_b * (255 - c.Blue));

                    var c_new = new SKColor(r, g, b, 255);

                    colors[n] = c_new;
                }
                this.skBitmap.Pixels = colors;


            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void StretchImage(int xDst, int yDst, Bitmap bitmap, int width, int height, ushort opacity) {

                if (width == 0 || height == 0)
                    return;

                var info = new SKImageInfo(width, height);

                var skbitmap = bitmap.SkBitmap.Resize(info, SKFilterQuality.High);

                //opacity = (ushort)(opacity & 0xFF);




                //if (opacity < 255) {

                //    var p = 1 - (opacity / 255.0);

                //    var pixelCount = width * height;
                //    var colors = new SKColor[pixelCount];


                //    var original_pixel = skbitmap.Pixels;

                //    for (var n = 0; n < pixelCount; n++) {
                //        var c = original_pixel[n];

                //        var r = (byte)(255 - p * (255 - c.Red));
                //        var g = (byte)(255 - p * (255 - c.Green));
                //        var b = (byte)(255 - p * (255 - c.Blue));

                //        var c1 = new SKColor(r, g, b, 255);

                //        colors[n] = c1;
                //    }
                //    skbitmap.Pixels = colors;
                //}

                this.skCanvas.DrawBitmap(skbitmap, xDst, yDst);
            }


            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void SetPixel(int xPos, int yPos, uint color) {
                this.skBitmap.SetPixel(xPos, yPos, color); ;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public uint GetPixel(int xPos, int yPos) {
                var c = this.skBitmap.GetPixel(xPos, yPos);

                return (uint)((c.Alpha << 24) | (c.Red << 16) | (c.Green << 8) | (c.Blue << 0)); ;

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
                if (width == 0 || height == 0)
                    return null;

                var buf = this.GetBitmap();


                //while (buf != null) {
                //    Console.WriteLine("NativeGetBitmap need to be implement");

                //    Thread.Sleep(1000);
                //}

                var ret = new byte[width * height * 4];
                var idx = 0;

                for (var y1 = y; y1 < y + height; y1++) {
                    for (var x1 = x; x1 < x + width; x1 += 4) {
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
                    //this.StretchImage(xDst, yDst, widthDst, heightDst, b, xSrc, ySrc, widthSrc, heightSrc, opacity);
                    this.StretchImage(xDst, yDst, b, widthDst, heightDst, opacity);
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void TileImage(int xDst, int yDst, Bitmap bitmap, int width, int height, ushort opacity) {



                this.StretchImage(xDst, yDst, bitmap, width, height, opacity); ;
            }

            //[MethodImpl(MethodImplOptions.InternalCall)]
            public void Scale9Image(int xDst, int yDst, int widthDst, int heightDst, Bitmap bitmap, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity) {

                //this.StretchImage(xDst, yDst, bitmap, widthDst, heightDst, opacity); ;
               

                // middle
                var color_mid = new SKColor[(bitmap.Height - (bottomBorder + topBorder)) * (bitmap.Width - (leftBorder + rightBorder))];
                var idx = 0;

                for (var y = topBorder; y < bitmap.Height - bottomBorder; y++) {
                    for (var x = leftBorder; x < bitmap.Width - rightBorder; x++) {
                        color_mid[idx++] = bitmap.GetPixel(x, y);   
                    }
                }

                var skbitmap_mid = new SKBitmap(bitmap.Width - (leftBorder + rightBorder),
                                            bitmap.Height - (bottomBorder + topBorder)) {
                    Pixels = color_mid
                };

                var info_mid = new SKImageInfo(widthDst - (leftBorder + rightBorder), heightDst - (topBorder + bottomBorder));

                var skbitmap_mid_resize = skbitmap_mid.Resize(info_mid, SKFilterQuality.High);
                this.skCanvas.DrawBitmap(skbitmap_mid_resize, xDst + leftBorder, yDst + topBorder);


                // left

                var color_left = new SKColor[leftBorder * bitmap.Height];
                idx = 0;

                for (var y = 0; y < bitmap.Height; y++) {
                    for (var x = 0; x < leftBorder; x++) {
                        color_left[idx++] = bitmap.GetPixel(x, y);
                    }
                }

                var skbitmap_left = new SKBitmap(leftBorder, bitmap.Height) {
                    Pixels = color_left
                };

                var info_left = new SKImageInfo(leftBorder, heightDst);

                var skbitmap_left_resize = skbitmap_left.Resize(info_left, SKFilterQuality.High);

                this.skCanvas.DrawBitmap(skbitmap_left_resize, xDst, yDst);

                // Right
                var color_right = new SKColor[rightBorder * bitmap.Height];
                idx = 0;

                for (var y = 0; y < bitmap.Height; y++) {
                    for (var x = bitmap.Width - rightBorder; x < bitmap.Width; x++) {
                        color_right[idx++] = bitmap.GetPixel(x, y);
                    }
                }

                var skbitmap_right = new SKBitmap(rightBorder, bitmap.Height) {
                    Pixels = color_right
                };

                var info_right = new SKImageInfo(rightBorder, heightDst);

                var skbitmap_right_resize = skbitmap_right.Resize(info_right, SKFilterQuality.High);

                this.skCanvas.DrawBitmap(skbitmap_right_resize, xDst + widthDst - rightBorder, yDst);

                // Top
   
                var color_top = new SKColor[topBorder * (bitmap.Width - (leftBorder + rightBorder))];
                idx = 0;

                for (var y = 0; y < topBorder; y++) {
                    for (var x = leftBorder; x < (bitmap.Width - (rightBorder)); x++) {
                        color_top[idx++] = bitmap.GetPixel(x, y);
                    }
                }

                var skbitmap_top = new SKBitmap(bitmap.Width - (leftBorder + rightBorder), topBorder) {
                    Pixels = color_top
                };

                var info_top = new SKImageInfo(widthDst - (leftBorder + rightBorder), topBorder);

                var skbitmap_top_resize = skbitmap_top.Resize(info_top, SKFilterQuality.High);

                this.skCanvas.DrawBitmap(skbitmap_top_resize, xDst + leftBorder, yDst);

                // bottom
                var color_bottom = new SKColor[bottomBorder * (bitmap.Width - (leftBorder + rightBorder))];
                idx = 0;

                for (var y = bitmap.Height - bottomBorder; y < bitmap.Height; y++) {
                    for (var x = leftBorder; x < (bitmap.Width - (rightBorder)); x++) {
                        color_bottom[idx++] = bitmap.GetPixel(x, y);
                    }
                }

                var skbitmap_bottom = new SKBitmap(bitmap.Width - (leftBorder + rightBorder), bottomBorder) {
                    Pixels = color_bottom
                };

                var info_bottom = new SKImageInfo(widthDst - (leftBorder + rightBorder), bottomBorder);

                var skbitmap_bottom_resize = skbitmap_bottom.Resize(info_bottom, SKFilterQuality.High);

                this.skCanvas.DrawBitmap(skbitmap_bottom_resize, xDst + leftBorder , yDst + heightDst - bottomBorder);






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
