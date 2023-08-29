using System;
using System.Device.I2c;
using GHIElectronic.Endpoint.Pins;

namespace GHIElectronic.Endpoint.Devices.I2c
{
    public class I2cController : I2cDevice {
        static int initializeCount = 0;
        private int busId;
        private I2cDevice i2cDev;
        public I2cController(int busId, int deviceAddress) {
            if (busId < 0)
                throw new Exception(string.Format("Not supported bus id {0}", busId));

            this.busId = busId;

            this.Acquire();

            this.i2cDev = I2cDevice.Create(new I2cConnectionSettings(this.busId, deviceAddress));

            if (this.i2cDev == null) {
                this.Release();
                throw new Exception("Could not create device");
            }
        }
        public override I2cConnectionSettings ConnectionSettings => throw new NotImplementedException();

        public override void Read(Span<byte> buffer) => this.i2cDev.Read(buffer);
        public override byte ReadByte() => this.i2cDev.ReadByte();
        public override void Write(ReadOnlySpan<byte> buffer) => this.i2cDev.Write(buffer);
        public override void WriteByte(byte value) => this.i2cDev.WriteByte(value);
        public override void WriteRead(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer) => this.i2cDev.WriteRead(writeBuffer, readBuffer);

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
            var pinConfig = STM32MP1.I2c.PinSettings[this.busId];

            STM32MP1.GpioPin.SetModer(pinConfig.SclPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.SdaPin, STM32MP1.Moder.Alternate);

            STM32MP1.GpioPin.SetAlternate(pinConfig.SclPin, pinConfig.SclAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.SdaPin, pinConfig.SdaAlternate);


            STM32MP1.GpioPin.SetPull(pinConfig.SclPin, STM32MP1.Pull.Up);
            STM32MP1.GpioPin.SetPull(pinConfig.SdaPin, STM32MP1.Pull.Up);

            STM32MP1.GpioPin.SetOutputType(pinConfig.SclPin, STM32MP1.OutputType.OpenDrain);
            STM32MP1.GpioPin.SetOutputType(pinConfig.SdaPin, STM32MP1.OutputType.OpenDrain);


            // load driver

        }

        private void UnLoadResources() {
            // releaset pins 
            var pinConfig = STM32MP1.I2c.PinSettings[this.busId];

            STM32MP1.GpioPin.SetModer(pinConfig.SclPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.SdaPin, STM32MP1.Moder.Input);

        }

        private bool disposed = false;
        ~I2cController() {
            this.Dispose(disposing: false);
        }
        protected override void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {                
                this.i2cDev.Dispose();
            }

            this.Release();

            this.disposed = true;
        }
    }
}
