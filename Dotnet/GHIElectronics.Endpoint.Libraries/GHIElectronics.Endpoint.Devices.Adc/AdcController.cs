using System.Text;
using GHIElectronics.Endpoint.Core;

using static GHIElectronics.Endpoint.Core.Configuration;

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
                case Gpio.PF11: return AdcPin.PF11;
                case Gpio.PA6: return AdcPin.PA6;
                case Gpio.PF12: return AdcPin.PF12;
                case Gpio.PB0: return AdcPin.PB0;
                case Gpio.PC0: return AdcPin.PC0;
                case Gpio.PC3: return AdcPin.PC3;
                case Gpio.PA3: return AdcPin.PA3;
                case Gpio.PA0: return AdcPin.PA0;
                case Gpio.PA4: return AdcPin.PA4;
                case Gpio.PA5: return AdcPin.PA5;
                case Gpio.PF13: return AdcPin.PF13;
                case Gpio.PF14: return AdcPin.PF14;
            }

            return Gpio.NONE;
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
                Gpio.SetModer(this.pinId, Gpio.Moder.Analog);

                Gpio.PinReserve(this.pinId);
            }
            
            // load driver

        }

        private void UnLoadResources() {
            // releaset pins
            if (this.controllerId == 0 && (this.channelId < 2))
                return; // ANA0 and ANA1 are special, no pin

            //var pinConfig = STM32MP1.Adc.PinSettings[this.controllerId][this.channelId];

            if (this.pinId >= 0 && this.pinId < 255) {
                Gpio.SetModer(this.pinId, Gpio.Moder.Input);

                Gpio.PinRelease(this.pinId);
            }

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
