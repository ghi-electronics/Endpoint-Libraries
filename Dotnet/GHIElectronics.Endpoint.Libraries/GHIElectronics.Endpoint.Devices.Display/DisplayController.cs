namespace GHIElectronics.Endpoint.Devices.Display
{
    public class DisplayConfiguration {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    
    public class DisplayController
    {
        private IDisplayProvider iDisplay;
        public DisplayController(IDisplayProvider display) => this.iDisplay = display;

        public void Flush(byte[] data) => this.Flush(data, 0, data.Length);
        public void Flush(byte[] data, int offset, int length) => this.iDisplay.Flush(data, offset, length);

        public void Flush(byte[] data, int offset, int length, int width, int height) => this.iDisplay.Flush(data, offset, length, width, height);


    }
}
