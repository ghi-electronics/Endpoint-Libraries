namespace GHIElectronics.Endpoint.UI.Drawing {
    /// <summary>
    /// A 32-bit color with 8 bits each of alpha, red, green, blue. The
    /// <see cref="Canvas"/> maps it to its <see cref="PixelFormat"/> at
    /// draw time: Monochrome treats non-zero as "lit", Rgb565 drops the
    /// low bits, Rgb888 drops alpha, Argb8888 keeps everything.
    /// </summary>
    public readonly struct Color {
        /// <summary>Red component, 0..255.</summary>
        public byte R { get; }
        /// <summary>Green component, 0..255.</summary>
        public byte G { get; }
        /// <summary>Blue component, 0..255.</summary>
        public byte B { get; }
        /// <summary>Alpha, 0 (transparent) .. 255 (opaque). Only Argb8888 canvases use it.</summary>
        public byte A { get; }

        /// <summary>Opaque color from R/G/B.</summary>
        public Color(byte r, byte g, byte b) : this(r, g, b, 0xFF) { }

        /// <summary>Color from R/G/B and alpha.</summary>
        public Color(byte r, byte g, byte b, byte a) {
            this.R = r; this.G = g; this.B = b; this.A = a;
        }

        /// <summary>Color from a 0xAARRGGBB value.</summary>
        public static Color FromArgb(uint argb) =>
            new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

        /// <summary>Color from a 0xRRGGBB value, opaque.</summary>
        public static Color FromRgb(uint rgb) =>
            new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        // Common colors — names match System.Drawing / CSS where they overlap.
        public static Color Transparent => new(0, 0, 0, 0);
        public static Color Black       => new(0x00, 0x00, 0x00);
        public static Color White       => new(0xFF, 0xFF, 0xFF);
        public static Color Red         => new(0xFF, 0x00, 0x00);
        public static Color Green       => new(0x00, 0xFF, 0x00);
        public static Color Blue        => new(0x00, 0x00, 0xFF);
        public static Color Yellow      => new(0xFF, 0xFF, 0x00);
        public static Color Cyan        => new(0x00, 0xFF, 0xFF);
        public static Color Magenta     => new(0xFF, 0x00, 0xFF);
        public static Color Navy        => new(0x00, 0x00, 0x80);
        public static Color Maroon      => new(0x80, 0x00, 0x00);
        public static Color Olive       => new(0x80, 0x80, 0x00);
        public static Color Gray        => new(0x80, 0x80, 0x80);
        public static Color Silver      => new(0xC0, 0xC0, 0xC0);
        public static Color Orange      => new(0xFF, 0xA5, 0x00);
    }
}
