using GHIElectronics.Endpoint.Devices.Display;

#pragma warning disable CA1416  // System.Drawing.Imaging.PixelFormat is Windows-annotated; we only use the int values.
using SysPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace GHIElectronics.Endpoint.UI.Drawing {
    /// <summary>
    /// Shortcut extensions so a Canvas can be handed to a DisplayController in
    /// one call without the caller spelling out the (Buffer, PixelFormat)
    /// tuple. The pixel format is looked up from <see cref="Canvas.PixelFormat"/>.
    /// </summary>
    public static class DisplayControllerExtensions {
        /// <summary>
        /// Push the whole Canvas to the display. Equivalent to
        /// <c>display.Flush(canvas.Buffer, mappedFormat)</c> with the format
        /// derived from <see cref="Canvas.PixelFormat"/>.
        /// </summary>
        public static void Flush(this DisplayController display, Canvas canvas) {
            if (display == null) throw new ArgumentNullException(nameof(display));
            if (canvas  == null) throw new ArgumentNullException(nameof(canvas));
            display.Flush(canvas.Buffer, ToSystemPixelFormat(canvas.PixelFormat));
        }

        private static SysPixelFormat ToSystemPixelFormat(PixelFormat f) => f switch {
            PixelFormat.Monochrome => SysPixelFormat.Format1bppIndexed,
            PixelFormat.Rgb565     => SysPixelFormat.Format16bppRgb565,
            PixelFormat.Rgb888     => SysPixelFormat.Format24bppRgb,
            PixelFormat.Argb8888   => SysPixelFormat.Format32bppArgb,
            _ => throw new NotSupportedException($"Canvas PixelFormat {f} has no DisplayController equivalent."),
        };
    }
}
