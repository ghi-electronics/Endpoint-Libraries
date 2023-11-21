using System.Text;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Pins;

namespace GHIElectronics.Endpoint.Devices.Adc
{
    public class AdcController : IDisposable {
        static int initializeCount;
        private int controllerId;
        private int channelId;
        private int pinId;

        private int fd = -1;        

        public int ResolutionInBits { get; } = 16;
        //public AdcController(int controllerId, int channelId) {
           
        //    this.controllerId = controllerId;
        //    this.channelId = channelId;

        //    this.Acquire();

        //    var path = "/sys/bus/iio/devices/iio:device" + this.controllerId.ToString() +  "/in_voltage"+ this.channelId.ToString() + "_raw";

        //    if (File.Exists(path)) {
        //        this.fd = Interop.Open(path, Interop.FileOpenFlags.O_RDONLY);
        //    }

        //    if (this.fd < 0) {
        //        this.Release();
        //        throw new Exception("Could not create device");
        //    }
        //}

        public AdcController(int pin) {

            this.controllerId = (pin >> 24) & 0xFF;
            this.channelId = (pin >> 16) & 0xFF;
            this.pinId = (pin>> 0) & 0xFFFF;


            this.Acquire();

            var path = "/sys/bus/iio/devices/iio:device" + this.controllerId.ToString() + "/in_voltage" + this.channelId.ToString() + "_raw";

            if (File.Exists(path)) {
                this.fd = Interop.Open(path, Interop.FileOpenFlags.O_RDONLY);
            }

            if (this.fd < 0) {
                this.Release();
                throw new Exception("Could not create device");
            }
        }

        public uint Read() {
            uint v = 0;

            var buf_read = new byte[8];

            unsafe {
                fixed (byte* readBufferPointer = buf_read) {
                    Interop.Seek(this.fd, 0, Interop.SeekFlags.SEEK_SET);
                    Interop.Read(this.fd, new IntPtr(readBufferPointer), 8);
                }
            }            

            v = uint.Parse(UTF8Encoding.UTF8.GetString(buf_read));

            return v;
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

        private void LoadResources() {
            // load pins 
            if (this.controllerId == 0 && (this.channelId < 2))
                return; // ANA0 and ANA1 are special, no pin

            //var pinConfig = STM32MP1.Adc.PinSettings[this.controllerId][this.channelId];

            if (this.pinId >=0)
                STM32MP1.GpioPin.SetModer(this.pinId, STM32MP1.Moder.Analog);
            
            // load driver

        }

        private void UnLoadResources() {
            // releaset pins
            if (this.controllerId == 0 && (this.channelId < 2))
                return; // ANA0 and ANA1 are special, no pin

            //var pinConfig = STM32MP1.Adc.PinSettings[this.controllerId][this.channelId];

            if (this.pinId >= 0)
                STM32MP1.GpioPin.SetModer(this.pinId, STM32MP1.Moder.Input);            

        }

        private bool disposed = false;
        ~AdcController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                Interop.Close(this.fd);
            }

            this.Release();

            this.disposed = true;
        }
    }
}