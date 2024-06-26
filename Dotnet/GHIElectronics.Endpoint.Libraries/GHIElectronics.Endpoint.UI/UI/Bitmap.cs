using System;
using GHIElectronics.Endpoint.UI.Media;

namespace GHIElectronics.Endpoint.UI {
    internal class Bitmap : IDisposable {
        private readonly GHIElectronics.Endpoint.Drawing.Graphics g;
        private readonly GHIElectronics.Endpoint.Drawing.Internal.Bitmap surface;

        public const ushort OpacityOpaque = 0xFF;
        public const ushort OpacityTransparent = 0;

        public const uint DT_WordWrap = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.WordWrap;
        public const uint DT_TruncateAtBottom = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TruncateAtBottom;
        public const uint DT_IgnoreHeight = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.IgnoreHeight;

        public const uint DT_AlignmentLeft = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentLeft;
        public const uint DT_AlignmentCenter = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentCenter;
        public const uint DT_AlignmentRight = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.AlignmentRight;

        public const uint DT_TrimmingWordEllipsis = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TrimmingWordEllipsis;
        public const uint DT_TrimmingCharacterEllipsis = (uint)GHIElectronics.Endpoint.Drawing.Graphics.DrawTextAlignment.TrimmingCharacterEllipsis;

        public int Height => this.g.Height;
        public int Width => this.g.Width;

        public Bitmap(GHIElectronics.Endpoint.Drawing.Graphics g) {
            this.g = g;
            this.surface = Extract(g);
        }

        private static GHIElectronics.Endpoint.Drawing.Internal.Bitmap Extract(GHIElectronics.Endpoint.Drawing.Graphics g) {
            if (g.surface is GHIElectronics.Endpoint.Drawing.Internal.Bitmap b)
                return b;

            throw new NotSupportedException();
        }

        public void Dispose() => this.g.Dispose();

        public void Clear() => this.g.Clear();
        public void Flush(int x, int y, int width, int height) => this.g.Flush(x, y, width, height);

        internal void SetPixel(int x, int y, Color color) => this.surface.SetPixel(x, y, color.ToNativeColor());
        internal void SetClippingRectangle(int x, int y, int width, int height) => this.surface.SetClippingRectangle(x, y, width, height);

        internal void DrawEllipse(Color color1, ushort thickness, int v1, int v2, int xRadius, int yRadius, Color color2, int v3, int v4, Color color3, int v5, int v6, ushort v7) => this.surface.DrawEllipse(color1.ToNativeColor(), thickness, v1, v2, xRadius, yRadius, color2.ToNativeColor(), v3, v4, color3.ToNativeColor(), v5, v6, v7);
        internal void DrawRectangle(Color outlineColor, ushort outlineThickness, int x, int y, int width, int height, int v1, int v2, Color color1, int v3, int v4, Color color2, int v5, int v6, ushort opacity) => this.surface.DrawRectangle(outlineColor.ToNativeColor(), outlineThickness, x, y, width, height, v1, v2, color1.ToNativeColor(), v3, v4, color2.ToNativeColor(), v5, v6, opacity);
        internal void DrawImage(int v1, int v2, ImageSource source, int v3, int v4, int width, int height) => this.surface.DrawImage(v1, v2, Extract(source.graphics), v3, v4, width, height, OpacityOpaque);
        internal void DrawImage(int x, int y, ImageSource bitmapSource, int v1, int v2, int width, int height, ushort opacity) => this.surface.DrawImage(x, y, Extract(bitmapSource.graphics), v1, v2, width, height, opacity);
        internal void DrawLine(Color color, int v, int ix1, int y1, int ix2, int y2) => this.surface.DrawLine(color.ToNativeColor(), v, ix1, y1, ix2, y2);
        internal void DrawText(string text, GHIElectronics.Endpoint.Drawing.Font font, Color color, int v1, int v2) => this.surface.DrawText(text, font, color.ToNativeColor(), v1, v2);
        internal bool DrawTextInRect(ref string text, ref int xRelStart, ref int yRelStart, int v1, int v2, int width, int height, uint flags, Color color, GHIElectronics.Endpoint.Drawing.Font font) => this.surface.DrawTextInRect(ref text, ref xRelStart, ref yRelStart, v1, v2, width, height, flags, color.ToNativeColor(), font);

        internal void TileImage(int v1, int v2, ImageSource bitmap, int width, int height, ushort opacity) => this.surface.TileImage(v1, v2, Extract(bitmap.graphics), width, height, opacity);
        internal void RotateImage(int angle, int v1, int v2, ImageSource bitmap, int sourceX, int sourceY, int sourceWidth, int sourceHeight, ushort opacity) => this.surface.RotateImage(angle, v1, v2, Extract(bitmap.graphics), sourceX, sourceY, sourceWidth, sourceHeight, opacity);
        internal void StretchImage(int x, int y, ImageSource bitmapSource, int width, int height, ushort opacity) => this.surface.StretchImage(x, y, Extract(bitmapSource.graphics), width, height, opacity);
        internal void StretchImage(int v1, int v2, int widthDst, int heightDst, ImageSource bitmap, int xSrc, int ySrc, int widthSrc, int heightSrc, ushort opacity) => this.surface.StretchImage(v1, v2, widthDst, heightDst, bitmap.graphics.surface, xSrc, ySrc, widthSrc, heightSrc, opacity);
        internal void Scale9Image(int v1, int v2, int widthDst, int heightDst, ImageSource bitmap, int leftBorder, int topBorder, int rightBorder, int bottomBorder, ushort opacity) => this.surface.Scale9Image(v1, v2, widthDst, heightDst, Extract(bitmap.graphics), leftBorder, topBorder, rightBorder, bottomBorder, opacity);
    }
}
