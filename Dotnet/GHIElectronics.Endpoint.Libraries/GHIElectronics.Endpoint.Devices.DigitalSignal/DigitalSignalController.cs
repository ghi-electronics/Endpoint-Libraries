using System.Device.Gpio;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;
using static GHIElectronics.Endpoint.Core.EPM815.Gpio;


namespace GHIElectronics.Endpoint.Devices.DigitalSignal {
    public enum DigitalSignalMode {
        GeneratePulse = 0,
        ReadPulse = 1,
        CapturePulse = 2,
    }

    public enum DigitalSignalError {
        None = 0,
        OutOfMemory = 1,
        Timeout = 2,
    }

    public class DigitalSignalController {

        internal const int CMD_RAW_DATA_START_OFFSET = 64;

        internal const int CMD_GENERATE_PULSE = 0x10;

        internal const int CMD_RESPONSE_DONE = 0x30;
        internal const int CMD_ABORT = 0x31;
        internal const int CMD_CHECK_BUSY = 0x32;

        internal const int MODE_GENERATE_PULSE = 0;
        internal const int MODE_READ_PULSE = 1;
        internal const int MODE_CAPTURE_PULSE = 2;

        internal const int TIMER_CLOCK = 205626176;




        public delegate void PulseGenerateEventHandler(DigitalSignalController sender, uint endState, bool aborted);
        public delegate void PulseReadEventHandler(DigitalSignalController sender, TimeSpan duration, uint count, uint pinState, bool aborted);
        public delegate void PulseCaptureEventHandler(DigitalSignalController sender, double[] buffer, uint count, uint pinState, bool aborted);

        public delegate void ErrorEventHandler(DigitalSignalController sender, DigitalSignalMode mode, DigitalSignalError error);

        private PulseReadEventHandler pulseReadCallback;
        private PulseGenerateEventHandler pulseGenerateCallback;
        private PulseCaptureEventHandler pulseCaptureCallback;
        private ErrorEventHandler errorCallback;

        public event PulseReadEventHandler ReadPulseFinished {
            add => this.pulseReadCallback += value;
            remove {
                if (this.pulseReadCallback != null)
                    this.pulseReadCallback -= value;
            }
        }

        public event PulseGenerateEventHandler GenerateFinished {
            add => this.pulseGenerateCallback += value;
            remove {
                if (this.pulseGenerateCallback != null)
                    this.pulseGenerateCallback -= value;
            }
        }

        public event PulseCaptureEventHandler CaptureFinished {
            add => this.pulseCaptureCallback += value;
            remove {
                if (this.pulseCaptureCallback != null)
                    this.pulseCaptureCallback -= value;
            }
        }

        public event ErrorEventHandler ErrorReceived {
            add => this.errorCallback += value;
            remove {
                if (this.errorCallback != null)
                    this.errorCallback -= value;
            }
        }

        public bool CanGeneratePulse { get; private set; } = true;
        public bool CanReadPulse { get; private set; } = true;

        public bool CanCapture { get; private set; } = true;



        private int pin;

        internal static bool InitLibCount = false;
        public DigitalSignalController(int pin) {

            if (pin < 0)
                throw new ArgumentException("Pin invalid.");

            if (IsPinReserved(pin))
                throw new ArgumentException("Pin reserved.");

           

            if (!File.Exists("/dev/rpmsg_ctrl0")) { // load remoteproc.sh
                var script = new Script("sremoteproc.sh", "./", "start");

                script.Start();

                while (!File.Exists("/dev/rpmsg_ctrl0")) ;
            }

            if (!InitLibCount) {

                if (File.Exists("/dev/rpmsg0")) { // reset rpmsg0
                    NativeRpmsgHelper.Release();

                    while (File.Exists("/dev/rpmsg0")) ;
                }

                NativeRpmsgHelper.Acquire(); // load rpmsg0

                InitLibCount = true;
            }

            this.pin = pin;
            NativeRpmsgHelper.DataReceived += this.NativeRpmsgHelper_DataReceived;

            NativeRpmsgHelper.TaskReceive();

            PinReserve(pin);

        }


        private void NativeRpmsgHelper_DataReceived(uint[] data) {
            if (data == null || data.Length < CMD_RAW_DATA_START_OFFSET)
                throw new Exception("Bad data found!");

            var size = data[0] / 4;



            var cmd = data[1];


            var pin = data[2];


            var mode = data[3] << 0;


            if (cmd == CMD_RESPONSE_DONE) {
                if (mode == MODE_GENERATE_PULSE) {
                    var edge = data[CMD_RAW_DATA_START_OFFSET / 2];
                    var aborted = data[CMD_RAW_DATA_START_OFFSET / 2 + 1] == 0 ? false : true;
                    var error = (DigitalSignalError)data[CMD_RAW_DATA_START_OFFSET / 2 + 2];


                    if (error != DigitalSignalError.None) {
                        if (this.errorCallback != null) {
                            this.errorCallback?.Invoke(this, DigitalSignalMode.GeneratePulse, error);


                        }
                    }
                    else {
                        if (this.pulseGenerateCallback != null) {
                            this.pulseGenerateCallback?.Invoke(this, edge, aborted);


                        }
                    }

                    this.CanGeneratePulse = true;
                }

                else if (mode == MODE_READ_PULSE) {
                    var durationTick = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var readPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 1];
                    var pinState = data[CMD_RAW_DATA_START_OFFSET / 2 + 2]; ;
                    var aborted = data[CMD_RAW_DATA_START_OFFSET / 2 + 3] == 0 ? false : true;
                    var durationTime = TimeSpan.FromTicks(durationTick);
                    var error = (DigitalSignalError)data[CMD_RAW_DATA_START_OFFSET / 2 + 4];


                    if (error != DigitalSignalError.None) {
                        if (this.errorCallback != null) {
                            this.errorCallback?.Invoke(this, DigitalSignalMode.GeneratePulse, error);


                        }
                    }
                    else {

                        if (this.pulseReadCallback != null) {
                            this.pulseReadCallback?.Invoke(this, durationTime, readPulseCount, pinState, aborted);


                        }
                    }

                    this.CanReadPulse = true;
                }

                if (mode == MODE_CAPTURE_PULSE) {

                    var capturedPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var pinState = data[CMD_RAW_DATA_START_OFFSET / 2 + 1];

                    var waitForEdge = data[CMD_RAW_DATA_START_OFFSET / 2 + 2] != 0 ? true : false;
                    var aborted = data[CMD_RAW_DATA_START_OFFSET / 2 + 3] == 0 ? false : true;
                    var error = (DigitalSignalError)data[CMD_RAW_DATA_START_OFFSET / 2 + 4];
                    var startCopy = CMD_RAW_DATA_START_OFFSET;




                    if (error == DigitalSignalError.None) {
                        var buffer = new uint[capturedPulseCount];



                        Array.Copy(data, startCopy, buffer, 0, capturedPulseCount);


                        if (this.pulseCaptureCallback != null) {

                            //Convert to double
                            var d = new double[buffer.Length];
                            var scale = 4.863; // 1000000000/205.6MHz

                            if (capturedPulseCount > 0) {


                                for (var i = 1; i < buffer.Length; i++) {
                                    d[i - 1] = (buffer[i] - buffer[i - 1]) * scale;
                                }


                            }

                            this.pulseCaptureCallback?.Invoke(this, d, capturedPulseCount, pinState, aborted);


                        }
                    }
                    else {
                        this.errorCallback?.Invoke(this, DigitalSignalMode.CapturePulse, error);
                    }

                    this.CanCapture = true;
                }
            }
        }

        private bool disposed = false;
        /// <exclude />
        ~DigitalSignalController() {
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


            NativeRpmsgHelper.DataReceived -= this.NativeRpmsgHelper_DataReceived;
            PinRelease(this.pin);


            this.disposed = true;
        }

        public void Generate(uint[] data) {
            this.Generate(data, 0, data.Length); ;
        }
        public void Generate(uint[] data, uint offset, int count) {
            this.Generate(data, offset, count, 100, Edge.Falling); ;
        }
        public void Generate(uint[] data, uint offset, int count, uint multiplier, Edge edge) {

            if (!this.CanGeneratePulse) {
                throw new Exception("Generate mode is busy");
            }

            var prescaler = TIMER_CLOCK / (1000000000UL / multiplier);

            if (prescaler == 0 || prescaler > 0xFFFF) {
                throw new Exception("Invalid multiplier.");
            }



            var buffer = new uint[count + CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = CMD_GENERATE_PULSE;

            // param 2 - pin
            buffer[2] = (uint)this.pin;

            // param 3
            buffer[3] = (uint)count; // count in uint

            // param 4
            buffer[4] = multiplier;

            // param 5
            buffer[5] = (uint)edge;



            buffer[CMD_RAW_DATA_START_OFFSET + 0] = data[offset + 0];

            var i = 1;

            while (i < count) {
                buffer[CMD_RAW_DATA_START_OFFSET + i] = data[offset + i] + data[offset + (i - 1)];
                data[offset + i] = buffer[CMD_RAW_DATA_START_OFFSET + i];
                i++;
            }

            this.CanGeneratePulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);
        }

        public void ReadPulse(uint pulseNum, Edge edge, bool waitForEdge = false) {
            if (!this.CanReadPulse) {
                throw new Exception("ReadPulse mode is busy");
            }

            var buffer = new uint[CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = MODE_READ_PULSE;

            // param 2 - pin
            buffer[2] = (uint)this.pin;

            // param 3
            buffer[3] = pulseNum; // count in uint

            // param 4
            buffer[4] = (uint)edge;

            // param 5      
            buffer[5] = waitForEdge == true ? 1U : 0;

            this.CanReadPulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        public void Capture(uint count, Edge edge, bool waitForEdge = false) => this.Capture(count, edge, waitForEdge, TimeSpan.Zero);
        public void Capture(uint count, Edge edge, bool waitForEdge, TimeSpan timeout) {

            if (!this.CanCapture) {
                throw new Exception("CanCapture mode is busy");
            }

            var buffer = new uint[CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = MODE_CAPTURE_PULSE;

            // param 2 - pin
            buffer[2] = (uint)this.pin;

            // param 3
            buffer[3] = count; // count in uint

            // param 4
            buffer[4] = (uint)edge;

            // param 5      
            buffer[5] = waitForEdge == true ? 1U : 0;

            // param 6      
            buffer[6] = (uint)timeout.Ticks >> 0;

            // param 7  
            buffer[7] = (uint)(timeout.Ticks >> 32);

            this.CanCapture = false;

            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        public void Abort() {

            var buffer = new uint[CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = CMD_ABORT;

            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        
    }

    public enum PulseFeedbackMode {
        DrainDuration = 0,
        EchoDuration = 1,
        DurationUntilEcho = 2
    }
    public class PulseFeedbackController : IDisposable {

        private readonly PulseFeedbackMode mode;

        private readonly int pulsePin;
        private readonly int echoPin;


        public bool DisableInterrupts { get; set; }
        public TimeSpan Timeout { get; set; }
        public TimeSpan PulseLength { get; set; }
        public bool PulseValue { get; set; }
        public bool EchoValue { get; set; }

        internal const int MODE_PULSEFEEDBACK = 3;

        public PulseFeedbackController(int pin, PulseFeedbackMode mode)
            : this(pin, -1, mode) {
        }

        public PulseFeedbackController(int pulsePin, int echoPin, PulseFeedbackMode mode) {


            if (pulsePin < 0)
                throw new ArgumentException("Pin invalid.");

            if (IsPinReserved(pulsePin))
                throw new ArgumentException("Pin reserved.");

            if (echoPin >= 0 && IsPinReserved(echoPin))
                throw new ArgumentException("Pin reserved.");

            this.DisableInterrupts = false;
            this.Timeout = TimeSpan.FromMilliseconds(100);
            this.PulseLength = TimeSpan.FromMilliseconds(20);
            this.PulseValue = true;
            this.EchoValue = true;

            this.mode = mode;

            this.pulsePin = pulsePin;
            this.echoPin = echoPin;

            if (mode == PulseFeedbackMode.DrainDuration) {
                if (this.echoPin != -1 || this.pulsePin == -1)
                    throw new ArgumentException();
            }
            else {
                if (this.echoPin == -1 || this.pulsePin == -1) {
                    throw new ArgumentException();
                }
            }

            // Resvered pins then set input

            //this.pulsePin.SetDriveMode(GpioPinDriveMode.Input);
            //this.echoPin?.SetDriveMode(GpioPinDriveMode.Input);

            if (!File.Exists("/dev/rpmsg_ctrl0")) { // load remoteproc.sh
                var script = new Script("sremoteproc.sh", "./", "start");

                script.Start();

                while (!File.Exists("/dev/rpmsg_ctrl0")) ;
            }

            if (!DigitalSignalController.InitLibCount) {

                if (File.Exists("/dev/rpmsg0")) { // reset rpmsg0
                    NativeRpmsgHelper.Release();

                    while (File.Exists("/dev/rpmsg0")) ;
                }

                NativeRpmsgHelper.Acquire(); // load rpmsg0

                DigitalSignalController.InitLibCount = true;
            }


            NativeRpmsgHelper.DataReceived += this.NativeRpmsgHelper_PulseFeedbackDataReceived;

            NativeRpmsgHelper.TaskReceive();

            PinReserve(this.pulsePin);

            if (this.echoPin >= 0) {
                PinReserve(this.echoPin);
            }

        }

        

        private void NativeRpmsgHelper_PulseFeedbackDataReceived(uint[] data) {

            if (data == null || data.Length < DigitalSignalController.CMD_RAW_DATA_START_OFFSET)
                throw new Exception("Bad data found!");

            var size = data[0] / 4;



            var cmd = data[1];


            var pin = data[2];


            var mode = data[3];


            if (cmd == DigitalSignalController.CMD_RESPONSE_DONE ) {

                this.value = ((long)(data[DigitalSignalController.CMD_RAW_DATA_START_OFFSET / 2]) << 32) | data[DigitalSignalController.CMD_RAW_DATA_START_OFFSET / 2 + 1];

                this.valueReady = true;
            }

        }

        private bool valueReady = false;
        private long value = 0;
        public TimeSpan Trigger() {
            var buffer = new uint[DigitalSignalController.CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = MODE_PULSEFEEDBACK;

            // param 2 - pulse feedback mode
            buffer[2] = (uint)this.mode;

            // param 3
            buffer[3] = (uint)this.pulsePin; // count in uint

            // param 4
            buffer[4] = (uint)this.echoPin;

            // param 5
            buffer[5] = (this.DisableInterrupts == true) ? 1U : 0;

            // param 6
            buffer[6] = (this.PulseValue == true) ? 1U : 0;

            // param 7
            buffer[7] = (this.EchoValue == true) ? 1U : 0;

            // param 8
            buffer[8] = (uint)((this.Timeout.Ticks >> 0) & 0xFFFFFFFF);

            // param 9
            buffer[9] = (uint)((this.Timeout.Ticks >> 32) & 0xFFFFFFFF);

            // param 10
            buffer[10] = (uint)((this.PulseLength.Ticks >> 0) & 0xFFFFFFFF);

            // param 11
            buffer[11] = (uint)((this.PulseLength.Ticks >> 32) & 0xFFFFFFFF);

            this.valueReady = false;

            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

            while (!this.valueReady) {
                Thread.Sleep(10);
            }

            return new TimeSpan(this.value);
        }

        private bool disposed = false;
        /// <exclude />
        ~PulseFeedbackController() {
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


            NativeRpmsgHelper.DataReceived -= this.NativeRpmsgHelper_PulseFeedbackDataReceived;

            PinRelease(this.pulsePin);

            if (this.echoPin >= 0) {
                PinRelease(this.echoPin);
            }
            this.disposed = true;
        }


    }

}
