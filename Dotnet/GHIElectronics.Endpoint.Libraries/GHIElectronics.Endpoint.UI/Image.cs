namespace GHIElectronics.Endpoint.UI;

public class Image : Widget {
    public Image(Widget parent) : base(IntPtr.Zero) {
        throw new NotImplementedException();  // M4
    }

    /// <summary>
    /// Set the image from RGB565 pixel bytes. Width and height describe the
    /// source bitmap; use Widget.Width/Height to control displayed size
    /// (LVGL scales).
    /// </summary>
    public void SetSource(byte[] rgb565Pixels, int width, int height) {
        throw new NotImplementedException();  // M4
    }
}
