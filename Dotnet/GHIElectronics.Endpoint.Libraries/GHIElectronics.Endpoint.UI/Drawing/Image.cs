using System;
using System.Runtime.InteropServices;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.UI.Drawing {
    /// <summary>
    /// A decoded image, ready to blit into a <see cref="Canvas"/>. Decoding
    /// (JPEG / PNG / BMP / GIF / TGA, picked from the file's magic bytes)
    /// happens once at <see cref="Load"/>; <see cref="Canvas.DrawImage(Image, int, int)"/>
    /// then just blits + converts to whatever <see cref="PixelFormat"/> the
    /// target canvas uses. Same <c>Image</c> instance is reusable across
    /// multiple canvases and multiple frames.
    /// </summary>
    /// <example>
    /// <code>
    /// // Standard .NET: &lt;EmbeddedResource Include="Resources\background.jpg" /&gt;
    /// var bytes = ... // load the resource stream
    /// var background = Image.Load(bytes);  // decode once
    ///
    /// while (running) {
    ///     canvas.DrawImage(background, 0, 0);  // many frames, no re-decode
    ///     display.Flush(canvas.Buffer);
    /// }
    /// </code>
    /// </example>
    public sealed class Image {
        /// <summary>Width in pixels.</summary>
        public int Width { get; }

        /// <summary>Height in pixels.</summary>
        public int Height { get; }

        /// <summary>
        /// Internal storage format of the decoded pixels. Currently always
        /// <see cref="PixelFormat.Rgb888"/>, the decoder's native output.
        /// This does NOT dictate the canvas format; <see cref="Canvas.DrawImage(Image, int, int)"/>
        /// converts to the target canvas's <see cref="Canvas.PixelFormat"/> on draw.
        /// </summary>
        public PixelFormat PixelFormat { get; }

        internal byte[] Pixels { get; }

        private Image(int width, int height, PixelFormat format, byte[] pixels) {
            this.Width = width;
            this.Height = height;
            this.PixelFormat = format;
            this.Pixels = pixels;
        }

        /// <summary>
        /// Decodes an image file (JPEG / PNG / BMP / GIF / TGA, detected
        /// from the file's magic bytes) into an in-memory bitmap. Decode
        /// runs inside <c>libendpoint</c> (stb_image); callers never see a
        /// pixel-format parameter or a stride.
        /// </summary>
        public static unsafe Image Load(byte[] fileBytes) {
            ArgumentNullException.ThrowIfNull(fileBytes);
            if (fileBytes.Length == 0) {
                throw new ArgumentException("Image file bytes are empty.", nameof(fileBytes));
            }

            byte* pixels;
            int w, h;
            fixed (byte* src = fileBytes) {
                Libendpoint.Check(Libendpoint.ImageDecode(src, fileBytes.Length, out pixels, out w, out h));
            }
            try {
                var managed = new byte[w * h * 3];
                Marshal.Copy((nint)pixels, managed, 0, managed.Length);
                return new Image(w, h, PixelFormat.Rgb888, managed);
            }
            finally {
                Libendpoint.ImageFree(pixels);
            }
        }
    }
}
