using GHIElectronic.Endpoint.Libraries;
using GHIElectronic.Endpoint.Pins;

namespace GHIElectronic.Endpoint.Devices.Can {
    
    public class CanController : IDisposable {
        static int initializeCount = 0;
        private int controllerId;

        private CanRaw canRaw;

        public CanController(int controllerId, int baudrate) {

            this.controllerId = controllerId;

            var script = new Script("ip", "/sbin/", "link set can" + this.controllerId + " type can bitrate " + baudrate.ToString());

            script.Start();

            this.Acquire();

            this.canRaw = new CanRaw("can" + this.controllerId.ToString());

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
            var pinConfig = STM32MP1.Can.PinSettings[this.controllerId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Alternate);

            STM32MP1.GpioPin.SetAlternate(pinConfig.TxPin, pinConfig.TxAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.RxPin, pinConfig.RxAlternate);

            // load driver
            // Can always loaded

            // bring can up
            var script = new Script("ifconfig", "/sbin/", "can" + this.controllerId + " up");

            script.Start();

        }

        private void UnLoadResources() {
            // releaset pins 
            var pinConfig = STM32MP1.Can.PinSettings[this.controllerId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Input);

            var script = new Script("ifconfig", "/sbin/", "can" + this.controllerId + " down");

            script.Start();
        }

        public void Write(CanMessage message) {

             var canId = new CanId();

            if (message.ExtendedId)
                canId.Extended = message.ArbitrationId;
            else
                canId.Standard = message.ArbitrationId;

            // TODO - add more properties;

            if (canId.IsValid)
                this.canRaw.WriteFrame(message.Data, canId);
            else
                throw new Exception("Invalid id!");
        }

        public CanMessage Read() {

            var data = new byte[64];

            var read = this.canRaw.TryReadFrame(data, out var data_length, out var canId);

            CanMessage message = default!;

            if (read && canId.IsValid) {
                message = new CanMessage {
                    ArbitrationId = canId.Value,
                    Length = data_length,
                    Data = data,

                    //TODO more will be add
                };
            }               
            return message;
        }

        private bool disposed = false;
        ~CanController() {
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
                this.canRaw.Dispose();

            }

            this.Release();

            this.disposed = true;
        }


    }
}
