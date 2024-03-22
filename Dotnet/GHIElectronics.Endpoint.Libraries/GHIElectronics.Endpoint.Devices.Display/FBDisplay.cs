using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core;



namespace GHIElectronics.Endpoint.Devices.Display {
    public class FBDisplay : IDisplayProvider {

        static int initializeCount = 0;
        static int fbHandle = -1;
        static int strideHandle = -1;


        static int fbWidth = 0;
        static int fbHeight = 0;

        static int fbSize = 0;

        static IntPtr fbPtr = IntPtr.Zero;

        const string CMD_LOCATION = "./";

        const string FB_PATH = "/dev/fb0";

        const string FB_STRIDE = "/sys/class/graphics/fb0/stride";

        static int stride = -1;

        public int Width { get; }
        public int Height { get; }
        DisplayConfiguration IDisplayProvider.Configuration => this.configuration;

        private Configuration configuration;
        public FBDisplay(DisplayConfiguration configuration) {

            if (configuration == null) {
                throw new Exception("The configuration can not be null");
            }

            this.Width = configuration.Width;
            this.Height = configuration.Height;
            this.configuration = (Configuration)configuration;

            this.Acquire();
        }

        public void Flush(byte[] data, int offset, int length, int x, int y, int width, int height, int orginalWidth) {
            unsafe {
                var mptr = (byte*)fbPtr;

                var widthDest2 = fbWidth << 1;
                var widthSrc2 = width << 1;
                var originalWidth2 = orginalWidth << 1;

                var h = Math.Min(height, this.Height);

                var w = Math.Min(this.Width, Math.Min(orginalWidth, Math.Min(fbWidth, width))); // which one is smallest

                w <<= 1;
                x <<= 1;

                h += y;
                w += x;

                for (var idx_y = y; idx_y < h; idx_y++) {
                    for (var id_x = x; id_x < w; id_x++) {

                        var posDest = (idx_y * widthDest2) + id_x;
                        var posSrc = (idx_y * (originalWidth2)) + id_x;

                        mptr[posDest] = data[posSrc + offset];
                    }
                }
            }

        }
        public void Flush(byte[] data, int offset, int length) => this.Flush(data, offset, length, 0, 0, this.Width, this.Height, this.Width);

        private void Acquire() {
            if (initializeCount == 0) {
                this.LoadResources();
            }

            initializeCount++;
        }

        private void Release() {
            initializeCount--;

            if (initializeCount == 0) {
                this.UnLoadResources();
            }
        }

        private unsafe void LoadResources() {

            var setting = $"{this.configuration.Clock},";

            setting += $"{this.configuration.Width},{this.configuration.Hsync_start},{this.configuration.Hsync_end},{this.configuration.Htotal},";
            setting += $"{this.configuration.Height},{this.configuration.Vsync_start},{this.configuration.Vsync_end},{this.configuration.Vtotal},";
            setting += $"{this.configuration.Num_modes},{this.configuration.Dpi_width},{this.configuration.Dpi_height},{this.configuration.Bus_flags},{this.configuration.Bus_format},{this.configuration.Connector_type},{this.configuration.Bpc}";


            var script_insmod = new Script("modprobe", CMD_LOCATION, $"panel-simple ltdc_generic_setting={setting}");
            script_insmod.Start();


            while (!File.Exists(FB_PATH)) {
                Thread.Sleep(10);
            }


            if (fbHandle < 0) {
                fbHandle = Interop.Open(FB_PATH, Interop.FileOpenFlags.O_RDWR);
            }

            if (stride < 0) {
                while (!File.Exists(FB_STRIDE))
                    Thread.Sleep(10);

                strideHandle = Interop.Open(FB_STRIDE, Interop.FileOpenFlags.O_RDONLY);

                var buf_read = new byte[16];

                fixed (byte* readBufferPointer = buf_read) {
                    Interop.Read(strideHandle, new IntPtr(readBufferPointer), 16);
                }

                Interop.Close(strideHandle);

                stride = int.Parse(UTF8Encoding.UTF8.GetString(buf_read));

                if (stride <= 0) {
                    throw new Exception("Could not identify fb scanline!");
                }

                //TODO:
                fbWidth = stride / 2; //TODO 2 is 16bpp / 8
                fbHeight = this.Height;

                fbSize = fbWidth * fbHeight * 2;

            }

            if (fbHandle >= 0 && fbPtr == IntPtr.Zero) {
                fbPtr = Interop.Mmap(IntPtr.Zero, fbSize, Interop.MemoryMappedProtections.PROT_READ | Interop.MemoryMappedProtections.PROT_WRITE, Interop.MemoryMappedFlags.MAP_SHARED, fbHandle, 0);

                if ((int)fbPtr == -1) {
                    throw new Exception("Could not create Framebuffer memory!");
                }
            }

            // Disable cursor
            while (!File.Exists("/sys/class/graphics/fbcon/cursor_blink")) {
                Thread.Sleep(10);
            }

            var script_config = new Script("ghi_disable_cursor.sh", "./", "");

            script_config.Start();


        }

        private void UnLoadResources() {
            if (fbHandle >= 0 && fbPtr != IntPtr.Zero) {
                Interop.Munmap(fbPtr, fbSize);

                fbSize = -1;
                fbPtr = IntPtr.Zero;
            }

            if (fbHandle >= 0) {
                Interop.Close(fbHandle);
                fbHandle = -1;
            }

            fbWidth = 0;
            fbHeight = 0;

            stride = -1;

        }

        private bool disposed = false;

        /// <exclude />
        ~FBDisplay() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {

            }


            this.Release();

            this.disposed = true;
        }

        public class Configuration : DisplayConfiguration {
            public int Clock { get; set; }
            public int Hsync_start { get; set; }
            public int Hsync_end { get; set; }
            public int Htotal { get; set; }
            public int Vsync_start { get; set; }
            public int Vsync_end { get; set; }
            public int Vtotal { get; set; }
            public int Num_modes { get; set; } = 1;
            public int Dpi_width { get; set; } = 0;
            public int Dpi_height { get; set; } = 0;
            public int Bus_flags { get; set; } = 0;
            public int Bus_format { get; set; } = 0;
            public int Connector_type { get; set; } = 0;
            public int Bpc { get; set; } = 8;

            public override string ToString() {
                var command = "";

                command += this.Clock.ToString("x8");
                command += this.Width.ToString("x8");
                command += this.Hsync_start.ToString("x8");
                command += this.Hsync_end.ToString("x8");
                command += this.Htotal.ToString("x8");
                command += this.Height.ToString("x8");
                command += this.Vsync_start.ToString("x8");
                command += this.Vsync_end.ToString("x8");
                command += this.Vtotal.ToString("x8");
                command += this.Num_modes.ToString("x8");
                command += this.Dpi_width.ToString("x8");
                command += this.Dpi_height.ToString("x8");
                command += this.Bus_flags.ToString("x8");
                command += this.Bus_format.ToString("x8");
                command += this.Connector_type.ToString("x8");

                return command;
            }
        }


    }
}
