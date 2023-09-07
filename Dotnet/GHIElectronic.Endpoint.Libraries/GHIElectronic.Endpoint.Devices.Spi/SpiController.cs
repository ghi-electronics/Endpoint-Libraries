using GHIElectronic.Endpoint.Core;
using GHIElectronic.Endpoint.Pins;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.Spi;

namespace GHIElectronic.Endpoint.Devices.Spi {

    public class SpiController : IDisposable {
        const string CMD_LOCATION = "/sbin";
        const string DRIVER_LOCATION = "/lib/modules/5.13.0/kernel/drivers/spi/spidev.ko";
        private SpiDevice spiDev;

        public SpiConnectionSettings ConnectionSettings => this.setting;

        static int initializeCount = 0;

        private int chipselectPin;
        private int chipselectPort;

        GpioController gpioController = default!;
        SpiConnectionSettings setting;


        public SpiController(SpiConnectionSettings setting) {
            if (setting.BusId < 0)
                throw new Exception(string.Format("Not supported bus id {0}", setting.BusId));

            this.chipselectPin = setting.ChipSelectLine;

            this.setting = setting;

            setting.ChipSelectLine = 0; // always 0;

            this.Acquire();

            this.spiDev = SpiDevice.Create(setting);

            if (this.spiDev == null) {

                this.Release();

                throw new Exception("Could not create device");
            }


            if (this.chipselectPin >= 0) {
                var port_divider = Processor.Name == Processor.AM335x ? 31 : 16;
                this.chipselectPort = this.chipselectPin / port_divider;
                this.chipselectPin = this.chipselectPin % port_divider;

                var gpioDriver = new LibGpiodDriver(this.chipselectPort);

                this.gpioController = new GpioController(PinNumberingScheme.Logical, gpioDriver);

                this.gpioController.OpenPin(this.chipselectPin, PinMode.Output);

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

        private void LoadResources() {

            // load pins 
            var pinConfig = STM32MP1.SpiBus.PinSettings[this.setting.BusId];

            STM32MP1.GpioPin.SetModer(pinConfig.MosiPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.MisoPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.ClockPin, STM32MP1.Moder.Alternate);

            STM32MP1.GpioPin.SetAlternate(pinConfig.MosiPin, pinConfig.MosiAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.MisoPin, pinConfig.MisoAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.ClockPin, pinConfig.ClockAlternate);


            // load driver
            if (Directory.Exists("/sys/class/spidev"))
                return;

            var script = new Script("insmod", CMD_LOCATION, DRIVER_LOCATION);
            script.Start();
        }

        private void UnLoadResources() {
            // releaset pins 

            var pinConfig = STM32MP1.SpiBus.PinSettings[this.setting.BusId];

            STM32MP1.GpioPin.SetModer(pinConfig.MosiPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.MisoPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.ClockPin, STM32MP1.Moder.Input);


            // release driver
            if (!Directory.Exists("/sys/class/spidev")) // unloaded
                return;

            var script = new Script("rmmod", CMD_LOCATION, DRIVER_LOCATION);
            script.Start();
        }

        private void SetupChipselectLine(bool start) {
            if (this.chipselectPin > 0) {
                if (start)
                    this.gpioController.Write(this.chipselectPin, this.setting.ChipSelectLineActiveState);

                else
                    this.gpioController.Write(this.chipselectPin, !this.setting.ChipSelectLineActiveState);
            }
        }
        public byte ReadByte() {
            this.SetupChipselectLine(true);

            var v = this.spiDev.ReadByte();

            this.SetupChipselectLine(false);

            return v;
        }

        public void Read(Span<byte> buffer) {
            this.SetupChipselectLine(true);

            this.spiDev.Read(buffer);

            this.SetupChipselectLine(false);
        }

        public void WriteByte(byte value) {
            this.SetupChipselectLine(true);

            this.spiDev.WriteByte(value);

            this.SetupChipselectLine(false);
        }

        public void Write(ReadOnlySpan<byte> buffer) {
            this.SetupChipselectLine(true);

            this.spiDev.Write(buffer);

            this.SetupChipselectLine(false);
        }

        public void TransferFullDuplex(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer) {
            this.SetupChipselectLine(true);

            this.spiDev.TransferFullDuplex(writeBuffer, readBuffer);

            this.SetupChipselectLine(false);
        }

        public void TransferSequential(byte[] writeBuffer, byte[] readBuffer) => this.TransferSequential(writeBuffer, 0, writeBuffer.Length, readBuffer, 0, readBuffer.Length);
        public void TransferSequential(byte[] writeBuffer, int offsetWrite, int lengthWrite, byte[] readBuffer, int offsetRead, int lengthRead) {


            var length = Math.Max(lengthWrite, lengthRead);

            var writebuff = new byte[length];
            var readbuff = new byte[length];

            Array.Copy(writeBuffer, offsetWrite, writebuff, 0, lengthWrite);

            this.SetupChipselectLine(true);

            this.spiDev.TransferFullDuplex(writebuff, readbuff);

            this.SetupChipselectLine(false);

            Array.Copy(readbuff, 0, readBuffer, offsetRead, lengthRead);

        }

        private bool disposed = false;
        ~SpiController() {
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
                this.spiDev?.Dispose();
                this.gpioController?.ClosePin(this.chipselectPin);
                this.gpioController?.Dispose();
            }


            this.Release();

            this.disposed = true;
        }
    }
}
