using System.Text;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.Adc
{
    public class AdcController : IDisposable {
        static int initializeCount;
        private int controllerId;
        private int channelId;
        private int pinId;

        private int fd = -1;        

        public int ResolutionInBits { get; } = 16;
       
        internal static int GetPinEncodeFromPin(int pin) {
            switch (pin) {
                //case AdcPin.ANA0: return AdcPin.ANA0;
                //case AdcPin.ANA1: return AdcPin.ANA1;
                case EPM815.Gpio.Pin.PF11: return EPM815.Adc.Pin.PF11;
                case EPM815.Gpio.Pin.PA6: return EPM815.Adc.Pin.PA6;
                case EPM815.Gpio.Pin.PF12: return EPM815.Adc.Pin.PF12;
                case EPM815.Gpio.Pin.PB0: return EPM815.Adc.Pin.PB0;
                case EPM815.Gpio.Pin.PC0: return EPM815.Adc.Pin.PC0;
                case EPM815.Gpio.Pin.PC3: return EPM815.Adc.Pin.PC3;
                case EPM815.Gpio.Pin.PA3: return EPM815.Adc.Pin.PA3;
                case EPM815.Gpio.Pin.PA0: return EPM815.Adc.Pin.PA0;
                case EPM815.Gpio.Pin.PA4: return EPM815.Adc.Pin.PA4;
                case EPM815.Gpio.Pin.PA5: return EPM815.Adc.Pin.PA5;
                case EPM815.Gpio.Pin.PF13: return EPM815.Adc.Pin.PF13;
                case EPM815.Gpio.Pin.PF14: return EPM815.Adc.Pin.PF14;
            }

            return EPM815.Gpio.Pin.NONE;
        }

        public AdcController(int adcPin) {

            var pin = adcPin;

            if (adcPin < 255) {
                pin = GetPinEncodeFromPin(adcPin);
            }


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

            if (this.pinId >= 0 && this.pinId < 255) {
                EPM815.Gpio.SetModer(this.pinId, EPM815.Gpio.Moder.Analog);

                EPM815.Gpio.PinReserve(this.pinId);
            }
            
            // load driver

        }

        private void UnLoadResources() {
            // releaset pins
            if (this.controllerId == 0 && (this.channelId < 2))
                return; // ANA0 and ANA1 are special, no pin

            //var pinConfig = STM32MP1.Adc.PinSettings[this.controllerId][this.channelId];

            if (this.pinId >= 0 && this.pinId < 255) {
                EPM815.Gpio.SetModer(this.pinId, EPM815.Gpio.Moder.Input);

                EPM815.Gpio.PinRelease(this.pinId);
            }

        }

        private bool disposed = false;
        /// <exclude />
        ~AdcController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
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
