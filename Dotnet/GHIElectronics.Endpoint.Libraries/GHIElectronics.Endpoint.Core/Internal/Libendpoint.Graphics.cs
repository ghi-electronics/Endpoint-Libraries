using System.Runtime.InteropServices;

namespace GHIElectronics.Endpoint.Core.Internal {
    // P/Invoke surface for the native image decoder (stb_image) and font
    // rasterizer (stb_truetype) exposed from libendpoint.so. Consumed by
    // Canvas.DrawImage, Canvas.DrawText and Font. See native/src/Graphics.cpp.
    internal static unsafe partial class Libendpoint {
        [LibraryImport(Library, EntryPoint = "endpoint_image_decode")]
        public static partial int ImageDecode(byte* data, int length,
                                              out byte* pixels, out int width, out int height);

        [LibraryImport(Library, EntryPoint = "endpoint_image_free")]
        public static partial void ImageFree(byte* pixels);

        [LibraryImport(Library, EntryPoint = "endpoint_font_init")]
        public static partial int FontInit(byte* ttfData, int length, out nint handle);

        [LibraryImport(Library, EntryPoint = "endpoint_font_free")]
        public static partial void FontFree(nint handle);

        [LibraryImport(Library, EntryPoint = "endpoint_font_metrics")]
        public static partial int FontMetrics(nint handle, int sizePx,
                                              out int ascent, out int descent, out int lineGap);

        [LibraryImport(Library, EntryPoint = "endpoint_font_rasterize")]
        public static partial int FontRasterize(nint handle, int codepoint, int sizePx,
                                                out byte* bitmap, out int width, out int height,
                                                out int xoff, out int yoff, out int advance);

        // LVGL-backed TTF font for Widget text (lv_tiny_ttf). Separate handle
        // namespace from the Canvas-side Font (stb_truetype).
        [LibraryImport(Library, EntryPoint = "endpoint_ui_font_load_ttf")]
        public static partial int UiFontLoadTtf(byte* data, int length, int sizePx, out nint handle);

        [LibraryImport(Library, EntryPoint = "endpoint_ui_font_free")]
        public static partial void UiFontFree(nint handle);

        [LibraryImport(Library, EntryPoint = "endpoint_ui_widget_set_font")]
        public static partial int UiWidgetSetFont(nint widget, nint fontHandle);
    }
}
