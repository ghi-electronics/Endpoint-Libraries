using System.IO.Ports;
using GHIElectronics.Endpoint.Pins;

namespace GHIElectronics.Endpoint.Devices.Uart {
    public class UartController : SerialPort, IDisposable {
        static int initializeCount = 0;
        private int portId = -1;
        public UartController(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits) : base(portName, baudRate, parity, dataBits, stopBits) {
            try {
                var port = portName[portName.Length - 1].ToString();

                this.portId = int.Parse(port);
            }
            catch {
                throw new Exception(string.Format("Could not detect the port {0}", portName));                
            }


            this.Acquire();

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
            var pinConfig = STM32MP1.Uart.PinSettings[this.portId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Alternate);

            STM32MP1.GpioPin.SetAlternate(pinConfig.TxPin, pinConfig.TxAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.RxPin, pinConfig.RxAlternate);

            if (pinConfig.CtsPin != STM32MP1.GpioPin.NONE) {
                STM32MP1.GpioPin.SetModer(pinConfig.CtsPin, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetAlternate(pinConfig.CtsPin, pinConfig.CtsAlternate);
            }

            if (pinConfig.RtsPin != STM32MP1.GpioPin.NONE) {
                STM32MP1.GpioPin.SetModer(pinConfig.RtsPin, STM32MP1.Moder.Alternate);
                STM32MP1.GpioPin.SetAlternate(pinConfig.RtsPin, pinConfig.RtsAlternate);
            }

            // load driver
            // Uart always loaded            
        }

        private void UnLoadResources() {
            // releaset pins 
            var pinConfig = STM32MP1.Uart.PinSettings[this.portId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Input);

            if (pinConfig.CtsPin != STM32MP1.GpioPin.NONE) 
                STM32MP1.GpioPin.SetModer(pinConfig.CtsPin, STM32MP1.Moder.Input);

            if (pinConfig.RtsPin != STM32MP1.GpioPin.NONE)
                STM32MP1.GpioPin.SetModer(pinConfig.RtsPin, STM32MP1.Moder.Input);
        }

        private bool disposed = false;
        ~UartController() {
            this.Dispose(disposing: false);
        }
        protected override void Dispose(bool disposing) {
            if (this.disposed)
                return;

            this.Release();

            this.disposed = true;
        }
    }
}
