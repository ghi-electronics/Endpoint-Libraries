#pragma warning disable CS8601 // Possible null reference assignment.
using GHIElectronic.Endpoint.Core;
using GHIElectronic.Endpoint.Pins;

namespace GHIElectronic.Endpoint.Devices.Can
{

    public delegate void MessageReceivedEventHandler(CanController sender);
    public delegate void ErrorReceivedEventHandler(CanController sender, ErrorReceivedEventArgs e);

    public enum CanError:uint
    {
        ErrorTxTimeout = 0x00000001U, 
        ErrorLostArbitration = 0x00000002U,
        ErrorCrtl = 0x00000004U, 
        ErrorProtocol = 0x00000008U,
        ErrorTransceiver = 0x00000010U,
        ErrorAck = 0x00000020U, 
        ErrorBusOff = 0x00000040U, 
        ErrorBusError = 0x00000080U, 
        ErrorRestarted = 0x00000100U,
    }

    public class ErrorReceivedEventArgs
    {
        public CanError Error { get; }
        public DateTime Timestamp { get; }

        internal ErrorReceivedEventArgs(CanError error, DateTime timestamp)
        {
            this.Error = error;
            this.Timestamp = timestamp;
        }
    }

    public class CanController : IDisposable
    {
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

        public CanController(int controllerId, int baudrate)
        {

            this.controllerId = controllerId;

            var script = new Script("ip", "./", "link set can" + this.controllerId + " type can bitrate " + baudrate.ToString());

            script.Start();

            this.Acquire();

            this.canRaw = new CanRaw("can" + this.controllerId.ToString());

        }

        private void Acquire()
        {
            if (initializeCount == 0)
            {
                this.LoadResources();
            }

            initializeCount++;
        }

        private void Release()
        {
            initializeCount--;

            if (initializeCount == 0)
            {
                this.UnLoadResources();
            }
        }

        public void Enable()
        {
            if (!this.IsEnabled)
            {
                // bring can up


                this.CanMessageRx = new CanMessage[this.ReadBufferSize];

                this.fifoRxIn = 0;
                this.fifoRxOut = 0;
                this.fifoRxCount = 0;

                this.locker = new object();

                this.IsEnabled = true;



                TaskReceive();




            }
        }

        public void Disable()
        {
            if (this.IsEnabled)
            {

                this.IsEnabled = false;
            }

        }

        public void EnableFilter(uint[] id, uint[] mask, bool invert = false )
        {
            if (id == null || mask == null || id.Length != mask.Length)
            {
                throw new ArgumentException("Invalid agrument!");
            }
            this.canRaw.Filter(id, mask, invert);
        }

        public void EnableError(uint error)
        {
            this.canRaw.FilterError(error);
        }
        private void LoadResources()
        {
            // load pins 
            var pinConfig = STM32MP1.Can.PinSettings[this.controllerId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Alternate);

            STM32MP1.GpioPin.SetAlternate(pinConfig.TxPin, pinConfig.TxAlternate);
            STM32MP1.GpioPin.SetAlternate(pinConfig.RxPin, pinConfig.RxAlternate);

            // load driver
            // Can always loaded

            // bring can up
            var script = new Script("ifconfig", "./", "can" + this.controllerId + " up");

            script.Start();


        }

        private void UnLoadResources()
        {
            // releaset pins 
            var pinConfig = STM32MP1.Can.PinSettings[this.controllerId];

            STM32MP1.GpioPin.SetModer(pinConfig.TxPin, STM32MP1.Moder.Input);
            STM32MP1.GpioPin.SetModer(pinConfig.RxPin, STM32MP1.Moder.Input);

            var script = new Script("ifconfig", "./", "can" + this.controllerId + " down");

            script.Start();
        }

        public void Write(CanMessage message)
        {
            if (!this.IsEnabled)
            {
                throw new Exception("CAN is disabled.");
            }
            var canId = new CanId();

            if (message.ExtendedFrameFormat)
                canId.Extended = message.ArbitrationId;
            else
                canId.Standard = message.ArbitrationId;

            canId.RemoteTransmissionRequest = message.RemoteTransmissionRequest;
            canId.ExtendedFrameFormat = message.ExtendedFrameFormat;

            // TODO - add more properties;

            if (canId.IsValid)
                this.canRaw.WriteFrame(message.Data, canId);
            else
                throw new Exception("Invalid id!");
        }

        public CanMessage Read()
        {
            if (!this.IsEnabled)
            {
                throw new Exception("CAN is disabled.");
            }

            if (this.fifoRxCount > 0)
            {
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

        public event MessageReceivedEventHandler MessageReceived
        {
            add
            {
                this.messageReceivedCallbacks += value;
            }
            remove
            {

                if (this.messageReceivedCallbacks != null)
                    this.messageReceivedCallbacks -= value;

            }
        }

        public event ErrorReceivedEventHandler ErrorReceived
        {
            add
            {
                this.errorReceivedCallbacks += value;
            }
            remove
            {

                if (this.errorReceivedCallbacks != null)
                    this.errorReceivedCallbacks -= value;

            }
        }
        private Task TaskReceive()
        {
            return Task.Run(() =>
            {
                while (!this.disposed && this.IsEnabled)
                {

                    var data64 = new byte[64];

                    var read = this.canRaw.TryReadFrame(data64, out var data_length, out var canId);

                    if (read && canId.IsValid && canId.Error == false)
                    {
                        var data8 = new byte[data_length];                        

                        for (int i = 0; i < data_length; i++)
                        {
                            data8[i] = data64[i]; 
                        }

                        var message = new CanMessage
                        {
                            ArbitrationId = canId.Value,
                            Length = data_length,
                            Data = data8,
                            ExtendedFrameFormat = canId.ExtendedFrameFormat,
                            RemoteTransmissionRequest = canId.RemoteTransmissionRequest,
                            Timestamp = DateTime.Now,

                            //TODO more will be add
                        };


                        this.CanMessageRx[this.fifoRxIn] = message;

                        this.fifoRxIn = (this.fifoRxIn + 1) % this.ReadBufferSize;

                        lock (this.locker)
                        {
                            this.fifoRxCount++;
                        }

                        messageReceivedCallbacks?.Invoke(this);
                    }
                    else
                    {
                        if (canId.Error == true)
                        {
                            this.errorReceivedCallbacks?.Invoke(this, new ErrorReceivedEventArgs((CanError)canId.Raw, DateTime.Now));
                        }
                    }
                }
            });
        }

        private bool disposed = false;
        ~CanController()
        {
            this.Dispose(disposing: false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                this.canRaw.Dispose();

            }

            this.Disable();
            this.Release();

            this.disposed = true;
        }


    }
}
#pragma warning restore IDE1006
