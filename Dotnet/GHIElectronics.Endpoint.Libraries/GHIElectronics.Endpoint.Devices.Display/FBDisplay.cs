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

        const string CMD_LOCATION = "/sbin";
        const string DRIVER_LOCATION = "/lib/modules/5.13.0/kernel/drivers/gpu/drm/panel/panel-simple.ko";

        const string LTDC_GENERIC_PATH = "/dev/ltdc-generic";
        const string FB_PATH = "/dev/fb0";

        const string FB_STRIDE = "/sys/class/graphics/fb0/stride";

        static int stride = -1;

        public int Width { get; }
        public int Height { get; }

        private ParallelConfiguration configuration;
        public FBDisplay(DisplayConfiguration configuration) {

            if (configuration == null) {
                throw new Exception("The configuration can not be null");
            }

            if (!File.Exists(LTDC_GENERIC_PATH)) {
                throw new Exception("No ltdc generic driver found!");
            }

            this.Width = configuration.Width;
            this.Height = configuration.Height;
            this.configuration = (ParallelConfiguration)configuration;

            this.Acquire();
        }
        public void Flush(byte[] data, int offset, int length) {
            unsafe {
                var mptr = (byte*)fbPtr;

                var x = 0;
                var y = 0;

                for (y = 0; y < this.Height; y++) {
                    for (x = 0; x < this.Width * 2; x++) {
                        var posDest = (y * fbWidth * 2) + x;
                        var posSrc = (y * this.Width * 2) + x;

                        mptr[posDest] = data[posSrc + offset];

                    }
                }
            }
        }

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
            var script_lsmod = new Script("lsmod", CMD_LOCATION, "");
            script_lsmod.Start();

            if (script_lsmod.Output.IndexOf("panel_simple") > 0) {
                var script_rm = new Script("rmmod", CMD_LOCATION, DRIVER_LOCATION);
                script_rm.Start();
            }


            var script_config = new Script("ltdc-config.sh", "/sbin", this.configuration.ToString());
            script_config.Start();

            var script_insmod = new Script("insmod", CMD_LOCATION, DRIVER_LOCATION);
            script_insmod.Start();

            while (!File.Exists(FB_PATH)) ;


            if (fbHandle < 0) {
                fbHandle = Interop.Open(FB_PATH, Interop.FileOpenFlags.O_RDWR);
            }

            if (stride < 0) {
                while (!File.Exists(FB_STRIDE)) ;

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
            while (!File.Exists("/sys/class/graphics/fbcon/cursor_blink")) ;

            script_config = new Script("ghi_disable_cursor.sh", "./", "");

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

            var script_lsmod = new Script("lsmod", CMD_LOCATION, "");
            script_lsmod.Start();

            if (script_lsmod.Output.IndexOf("panel_simple") > 0) {
                var script_rmmod = new Script("rmmod", CMD_LOCATION, DRIVER_LOCATION);
                script_rmmod.Start();
            }
        }

        private bool disposed = false;
        ~FBDisplay() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {

            }


            this.Release();

            this.disposed = true;
        }

        public class ParallelConfiguration : DisplayConfiguration {
            public int Clock { get; set; }
            public int Hsync_start { get; set; }
            public int Hsync_end { get; set; }
            public int Htotal { get; set; }
            public int Vsync_start { get; set; }
            public int Vsync_end { get; set; }
            public int Vtotal { get; set; }
            public int Num_modes { get; set; } = 0;
            public int Dpi_width { get; set; } = 0;
            public int Dpi_height { get; set; } = 0;
            public int Bus_flags { get; set; } = 0;
            public int Bus_format { get; set; } = 0;
            public int Connector_type { get; set; } = 0;

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
