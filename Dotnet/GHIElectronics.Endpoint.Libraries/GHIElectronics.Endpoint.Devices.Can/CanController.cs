#pragma warning disable CS8601 // Possible null reference assignment.
using GHIElectronics.Endpoint.Core;
using static GHIElectronics.Endpoint.Core.EPM815;


namespace GHIElectronics.Endpoint.Devices.Can
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

        private CanMessage[] canMessageRx;
        public int ReadBufferSize { get; set; } = 128;
        private int fifoRxIn;
        private int fifoRxOut;
        private int fifoRxCount;
        public int MessagesToRead => this.fifoRxCount;

        public bool IsEnabled { get; private set; } = false;

        object locker;

        private MessageReceivedEventHandler messageReceivedCallbacks;
        private ErrorReceivedEventHandler errorReceivedCallbacks;
        internal static bool IsCanFd = false;


        public CanController(int controllerId, int nominalBitrate, int dataBitrate = 0)
        {            
            if (controllerId != EPM815.Can.Can1 && controllerId != EPM815.Can.Can2) {
                throw new ArgumentException("Only CAN1 and CAN2 are supported.");
            }

            this.controllerId = controllerId-1;

            var fdBaurate = dataBitrate > 0 ? string.Format(" dbitrate {0} fd on", dataBitrate) : "";

            IsCanFd = fdBaurate.Length > 0 ? true: false;

            var script = new Script("ip", "./", "link set can" + this.controllerId + " type can bitrate " + nominalBitrate.ToString() + fdBaurate);

            script.Start();

            this.Acquire();

            this.canRaw = new CanRaw("can" + this.controllerId.ToString());

            if (IsCanFd)
                this.canRaw.EnableCanFd();

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

                this.canMessageRx = new CanMessage[this.ReadBufferSize];

                this.fifoRxIn = 0;
                this.fifoRxOut = 0;
                this.fifoRxCount = 0;

                this.locker = new object();

                this.IsEnabled = true;



                this.TaskReceive();




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
            this.canRaw.FilterError(error); ;
        }

        public void Reset() {            

            var script = new Script("ip", "./", "link set can" + this.controllerId + " type can restart-ms 100");

            script.Start();

        }

        private void LoadResources()
        {
            // load pins 
            var pinConfig = EPM815.Can.PinSettings[this.controllerId];

            if (Gpio.IsPinReserved(pinConfig.TxPin)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.TxPin);
            }

            if (Gpio.IsPinReserved(pinConfig.RxPin)) {
                EPM815.ThrowExceptionPinInUsed(pinConfig.RxPin);
            }

            Gpio.SetModer(pinConfig.TxPin, Gpio.Moder.Alternate);
            Gpio.SetModer(pinConfig.RxPin, Gpio.Moder.Alternate);

            Gpio.SetAlternate(pinConfig.TxPin, pinConfig.TxAlternate);
            Gpio.SetAlternate(pinConfig.RxPin, pinConfig.RxAlternate);

            // load driver
            // Can always loaded

            // bring can up
            var script = new Script("ifconfig", "./", "can" + this.controllerId + " up");

            script.Start();

            Gpio.PinReserve(pinConfig.TxPin);
            Gpio.PinReserve(pinConfig.RxPin);
        }

        private void UnLoadResources()
        {
            // releaset pins 
            var pinConfig = EPM815.Can.PinSettings[this.controllerId];

            

            var script = new Script("ifconfig", "./", "can" + this.controllerId + " down");

            script.Start();

            Gpio.PinRelease(pinConfig.TxPin);
            Gpio.PinRelease(pinConfig.RxPin);

            Gpio.SetModer(pinConfig.TxPin, Gpio.Moder.Input);
            Gpio.SetModer(pinConfig.RxPin, Gpio.Moder.Input);
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

            if (canId.IsValid) {
                if (IsCanFd)
                    this.canRaw.WriteFrameFd(message.Data, canId, message.BitRateSwitch);
                else
                    this.canRaw.WriteFrame(message.Data, canId);
            }
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
                var message = this.canMessageRx[this.fifoRxOut];

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
                this.messageReceivedCallbacks += value; ;
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
                this.errorReceivedCallbacks += value; ;
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

                    var read = false;
                    CanId canId;
                    var frameLength = -1;
                    var bitrateSwitch = false;

                    if (IsCanFd) {
                        read = this.canRaw.TryReadFrameFd(data64, out var v1, out var v2, out var v3);
                        frameLength = v1;
                        canId = v2;
                        bitrateSwitch = v3;
                    }
                    else {
                        read = this.canRaw.TryReadFrame(data64, out var v1, out var v2);
                        frameLength = v1;
                        canId = v2;
                    }

                    if (read && canId.IsValid && canId.Error == false)
                    {
                        var data = new byte[frameLength];                        

                        for (var i = 0; i < frameLength; i++)
                        {
                            data[i] = data64[i]; 
                        }

           

                        var message = new CanMessage
                        {
                            ArbitrationId = canId.Value,
                            Length = frameLength,
                            Data = data,
                            ExtendedFrameFormat = canId.ExtendedFrameFormat,
                            RemoteTransmissionRequest = canId.RemoteTransmissionRequest,
                            Timestamp = DateTime.Now,
                            BitRateSwitch = bitrateSwitch,

                            //TODO more will be add
                        };


                        this.canMessageRx[this.fifoRxIn] = message;

                        this.fifoRxIn = (this.fifoRxIn + 1) % this.ReadBufferSize;

                        lock (this.locker)
                        {
                            this.fifoRxCount++;
                        }

                        this.messageReceivedCallbacks?.Invoke(this);
                    }
                    else
                    {
                        if (canId.Error == true)
                        {
                            this.errorReceivedCallbacks?.Invoke(this, new ErrorReceivedEventArgs((CanError)canId.Raw, DateTime.Now));
                        }
                    }
                }
            }); ;
        }

        private bool disposed = false;
        /// <exclude />
        ~CanController()
        {
            this.Dispose(disposing: false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <exclude />
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
