#pragma warning disable CS8601 // Possible null reference assignment.
using GHIElectronic.Endpoint.Core;
using GHIElectronic.Endpoint.Pins;

namespace GHIElectronic.Endpoint.Devices.Can {

    public delegate void MessageReceivedEventHandler(CanController sender, MessageReceivedEventArgs e);
    public delegate void ErrorReceivedEventHandler(CanController sender, ErrorReceivedEventArgs e);

    public enum CanError {
        ReadBufferOverrun = 0,
        ReadBufferFull = 1,
        BusOff = 2,
        Passive = 3,
    }

    public enum ErrorStateIndicator {
        Active = 0,
        Passive = 1,
    }
    public class MessageReceivedEventArgs {     
        public DateTime Timestamp { get; }

        internal MessageReceivedEventArgs(DateTime timestamp) {            
            this.Timestamp = timestamp;
        }
    }

    public class ErrorReceivedEventArgs {
        public CanError Error { get; }
        public DateTime Timestamp { get; }

        internal ErrorReceivedEventArgs(CanError error, DateTime timestamp) {
            this.Error = error;
            this.Timestamp = timestamp;
        }
    }

    public class CanController : IDisposable {
        static int initializeCount = 0;
        private int controllerId;

        private CanRaw canRaw;

        private CanMessage[] CanMessageRx;
        public int ReadBufferSize { get; set; } = 128;
        private int fifoRxIn;
        private int fifoRxOut;
        private int fifoRxCount;
        public int MessagesToRead => this.fifoRxCount;

        public bool IsEnabled { get; private set; } = false;

        object locker;

        private MessageReceivedEventHandler messageReceivedCallbacks;
        private ErrorReceivedEventHandler errorReceivedCallbacks;

        Thread threadReceive;

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

        public void Enable() {
            if (!this.IsEnabled) {
                // bring can up


                this.CanMessageRx = new CanMessage[this.ReadBufferSize];

                this.fifoRxIn = 0;
                this.fifoRxOut = 0;
                this.fifoRxCount = 0;

                this.locker = new object();

                this.IsEnabled = true;


                this.threadReceive = new Thread(ThreadReceive);
                this.threadReceive.Start(); 
               
            }
        }

        public void Disable() {
            if (this.IsEnabled) {

                this.IsEnabled = false;

                try {
                    this.threadReceive.Abort();
                }
                catch {
                }

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
            if (!this.IsEnabled) {
                throw new Exception("CAN is disabled.");
            }
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
            if (!this.IsEnabled) {
                throw new Exception("CAN is disabled.");
            }

            if (this.fifoRxCount > 0) {
                var message = this.CanMessageRx[this.fifoRxOut];

                this.fifoRxOut = (this.fifoRxOut + 1) % this.ReadBufferSize;

                lock (this.locker)
                {
                    this.fifoRxCount--;
                }

                return message;
            }

            return null;
        }

        public event MessageReceivedEventHandler MessageReceived {
            add {
                this.messageReceivedCallbacks += value;
            }
            remove {

                if (this.messageReceivedCallbacks != null)
                    this.messageReceivedCallbacks -= value;

            }
        }
        private void ThreadReceive() {
            //return Task.Run(() => {

            while (!this.disposed && this.IsEnabled) {

                var data = new byte[64];

                var read = this.canRaw.TryReadFrame(data, out var data_length, out var canId);


                if (read && canId.IsValid) {
                    var message = new CanMessage {
                        ArbitrationId = canId.Value,
                        Length = data_length,
                        Data = data,

                        //TODO more will be add
                    };


                    this.CanMessageRx[this.fifoRxIn] = message;

                    this.fifoRxIn = (this.fifoRxIn + 1) % this.ReadBufferSize;

                    lock (this.locker)
                    {
                        this.fifoRxCount++;
                    }

                    messageReceivedCallbacks?.Invoke(this, new MessageReceivedEventArgs(DateTime.Now) ); 
                }               
            }
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

            this.Disable();
            this.Release();

            this.disposed = true;
        }


    }
}
#pragma warning restore IDE1006
