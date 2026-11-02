using System.Device.Gpio;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;
using static GHIElectronics.Endpoint.Core.EPM815.Gpio;


namespace GHIElectronics.Endpoint.Devices.DigitalSignal {
    /// <summary>What a <see cref="DigitalSignalController"/> operation does on a GPIO pin.</summary>
    public enum DigitalSignalMode {
        /// <summary>Emits a train of timed high/low pulses on the pin.</summary>
        GeneratePulse = 0,
        /// <summary>Measures the duration of a single pulse on the pin.</summary>
        ReadPulse = 1,
        /// <summary>Records a train of edge timestamps from the pin into a buffer.</summary>
        CapturePulse = 2,
    }

    /// <summary>Why a <see cref="DigitalSignalController"/> operation stopped before producing its result.</summary>
    public enum DigitalSignalError {
        /// <summary>No error: the operation completed normally.</summary>
        None = 0,
        /// <summary>The capture buffer could not be allocated on the timing helper.</summary>
        OutOfMemory = 1,
        /// <summary>The expected edge did not arrive before the configured timeout.</summary>
        Timeout = 2,
    }

    /// <summary>
    /// Generates, reads, or captures digital pulse trains on a single GPIO pin with
    /// nanosecond-grade timing produced by a hardware timing helper. One
    /// <see cref="DigitalSignalController"/> owns one pin and runs one operation at
    /// a time; completion is reported through <see cref="GenerateFinished"/>,
    /// <see cref="ReadPulseFinished"/> or <see cref="CaptureFinished"/>. Each
    /// callback runs on the native receive thread - not the thread that started
    /// the operation.
    /// </summary>
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




        /// <summary>
        /// Fires once when <see cref="Generate(uint[], uint, int, uint, Edge)"/> finishes.
        /// Runs on the native receive thread.
        /// </summary>
        /// <param name="sender">The controller that generated the pulses.</param>
        /// <param name="endState">The pin level (0 or 1) after the last pulse.</param>
        /// <param name="aborted"><c>true</c> if <see cref="Abort"/> stopped the generation; otherwise <c>false</c>.</param>
        public delegate void PulseGenerateEventHandler(DigitalSignalController sender, uint endState, bool aborted);

        /// <summary>
        /// Fires once when <see cref="ReadPulse"/> finishes. Runs on the native receive thread.
        /// </summary>
        /// <param name="sender">The controller that read the pulse.</param>
        /// <param name="duration">Total time the matching edges accumulated (sum of the matching pulse widths).</param>
        /// <param name="count">Number of pulses actually measured (may be less than requested on abort/timeout).</param>
        /// <param name="pinState">Pin level (0 or 1) at the end of the read.</param>
        /// <param name="aborted"><c>true</c> if <see cref="Abort"/> stopped the read; otherwise <c>false</c>.</param>
        public delegate void PulseReadEventHandler(DigitalSignalController sender, TimeSpan duration, uint count, uint pinState, bool aborted);

        /// <summary>
        /// Fires once when <see cref="Capture(uint, Edge, bool, TimeSpan)"/> finishes.
        /// Runs on the native receive thread.
        /// </summary>
        /// <param name="sender">The controller that captured the pulses.</param>
        /// <param name="buffer">Pulse widths in nanoseconds, each the interval between consecutive edges.</param>
        /// <param name="count">Number of edges captured (<paramref name="buffer"/> contains <c>count - 1</c> deltas).</param>
        /// <param name="pinState">Pin level (0 or 1) at the end of the capture.</param>
        /// <param name="aborted"><c>true</c> if <see cref="Abort"/> stopped the capture; otherwise <c>false</c>.</param>
        public delegate void PulseCaptureEventHandler(DigitalSignalController sender, double[] buffer, uint count, uint pinState, bool aborted);

        /// <summary>
        /// Fires on the native receive thread when an operation fails. The usual <c>Finished</c>
        /// callback is not raised when this fires.
        /// </summary>
        /// <param name="sender">The controller that was running the operation.</param>
        /// <param name="mode">Which operation produced the error.</param>
        /// <param name="error">The failure cause.</param>
        public delegate void ErrorEventHandler(DigitalSignalController sender, DigitalSignalMode mode, DigitalSignalError error);

        private PulseReadEventHandler pulseReadCallback;
        private PulseGenerateEventHandler pulseGenerateCallback;
        private PulseCaptureEventHandler pulseCaptureCallback;
        private ErrorEventHandler errorCallback;

        /// <summary>Raised once when <see cref="ReadPulse"/> completes. Handler runs on the native receive thread.</summary>
        public event PulseReadEventHandler ReadPulseFinished {
            add => this.pulseReadCallback += value;
            remove {
                if (this.pulseReadCallback != null)
                    this.pulseReadCallback -= value;
            }
        }

        /// <summary>Raised once when <see cref="Generate(uint[], uint, int, uint, Edge)"/> completes. Handler runs on the native receive thread.</summary>
        public event PulseGenerateEventHandler GenerateFinished {
            add => this.pulseGenerateCallback += value;
            remove {
                if (this.pulseGenerateCallback != null)
                    this.pulseGenerateCallback -= value;
            }
        }

        /// <summary>Raised once when <see cref="Capture(uint, Edge, bool, TimeSpan)"/> completes. Handler runs on the native receive thread.</summary>
        public event PulseCaptureEventHandler CaptureFinished {
            add => this.pulseCaptureCallback += value;
            remove {
                if (this.pulseCaptureCallback != null)
                    this.pulseCaptureCallback -= value;
            }
        }

        /// <summary>Raised instead of the normal <c>Finished</c> event when an operation fails (timeout or out-of-memory). Handler runs on the native receive thread.</summary>
        public event ErrorEventHandler ErrorReceived {
            add => this.errorCallback += value;
            remove {
                if (this.errorCallback != null)
                    this.errorCallback -= value;
            }
        }

        /// <summary><c>true</c> while no <see cref="Generate(uint[], uint, int, uint, Edge)"/> operation is in flight.</summary>
        public bool CanGeneratePulse { get; private set; } = true;
        /// <summary><c>true</c> while no <see cref="ReadPulse"/> operation is in flight.</summary>
        public bool CanReadPulse { get; private set; } = true;

        /// <summary><c>true</c> while no <see cref="Capture(uint, Edge, bool, TimeSpan)"/> operation is in flight.</summary>
        public bool CanCapture { get; private set; } = true;



        private int pin;

        internal static bool InitLibCount = false;
        /// <summary>
        /// Reserves the given GPIO pin, starts the timing helper (loading it if needed),
        /// and subscribes to its data stream. The constructor returns once the helper is reachable;
        /// subsequent calls to <see cref="Generate(uint[], uint, int, uint, Edge)"/>,
        /// <see cref="ReadPulse"/> and <see cref="Capture(uint, Edge, bool, TimeSpan)"/> go through it.
        /// </summary>
        /// <param name="pin">GPIO pin number (combined port+index, see <c>EPM815.Gpio</c>).</param>
        /// <exception cref="ArgumentException">Pin is negative or already reserved by another driver.</exception>
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

            NativeRpmsgHelper.StartReceiving();

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

        /// <summary>Unsubscribes from the timing helper and releases the pin so another driver can claim it.</summary>
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

        /// <summary>Generates the whole <paramref name="data"/> array with the default multiplier of 100 ns and <see cref="Edge.Falling"/> start edge.</summary>
        /// <param name="data">Pulse widths; see <see cref="Generate(uint[], uint, int, uint, Edge)"/>.</param>
        public void Generate(uint[] data) {
            this.Generate(data, 0, data.Length); ;
        }
        /// <summary>Generates a slice of <paramref name="data"/> with the default multiplier of 100 ns and <see cref="Edge.Falling"/> start edge.</summary>
        /// <param name="data">Pulse widths; see <see cref="Generate(uint[], uint, int, uint, Edge)"/>.</param>
        /// <param name="offset">Index of the first value in <paramref name="data"/> to generate.</param>
        /// <param name="count">Number of values to generate.</param>
        public void Generate(uint[] data, uint offset, int count) {
            this.Generate(data, offset, count, 100, Edge.Falling); ;
        }
        /// <summary>
        /// Emits a pulse train on the pin. Each entry in <paramref name="data"/> is a pulse width in
        /// units of <paramref name="multiplier"/> nanoseconds; consecutive entries alternate between
        /// the two pin levels, starting from the level opposite of <paramref name="edge"/>. Returns
        /// immediately; completion arrives on <see cref="GenerateFinished"/>.
        /// </summary>
        /// <param name="data">Pulse widths, in units of <paramref name="multiplier"/> ns.</param>
        /// <param name="offset">Index of the first width to emit.</param>
        /// <param name="count">Number of widths to emit.</param>
        /// <param name="multiplier">Nanoseconds per unit (prescaler sanity-checked against the hardware timer; must be &gt; 0 and fit in 16 bits).</param>
        /// <param name="edge">Which edge the first pulse ends on (<see cref="Edge.Falling"/> means start high, finish on a falling edge).</param>
        /// <exception cref="Exception">Thrown when a previous Generate is still running or the multiplier is out of range.</exception>
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

        /// <summary>
        /// Measures the aggregate duration of <paramref name="pulseNum"/> matching-edge pulses on the pin.
        /// Returns immediately; the measured duration arrives on <see cref="ReadPulseFinished"/>.
        /// </summary>
        /// <param name="pulseNum">Number of pulses to measure (counted on the <paramref name="edge"/> transitions).</param>
        /// <param name="edge">Which edge marks the start/end of each counted pulse.</param>
        /// <param name="waitForEdge">If <c>true</c>, align the first measurement to the first matching edge; if <c>false</c>, start measuring at once.</param>
        /// <exception cref="Exception">Thrown when a previous ReadPulse is still running.</exception>
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

        /// <summary>Captures <paramref name="count"/> edges with no timeout (<see cref="TimeSpan.Zero"/>).</summary>
        /// <param name="count">Number of edge timestamps to capture.</param>
        /// <param name="edge">Which edge to timestamp (<see cref="Edge.Rising"/> or <see cref="Edge.Falling"/>).</param>
        /// <param name="waitForEdge">If <c>true</c>, defer capturing until the first matching edge; if <c>false</c>, start sampling the current state at once.</param>
        public void Capture(uint count, Edge edge, bool waitForEdge = false) => this.Capture(count, edge, waitForEdge, TimeSpan.Zero);
        /// <summary>
        /// Captures <paramref name="count"/> edge-to-edge timings on the pin. Returns immediately; the
        /// captured timings arrive on <see cref="CaptureFinished"/> as nanosecond values (consecutive
        /// edge deltas, so the array has <c>count - 1</c> useful entries). Fails with
        /// <see cref="DigitalSignalError.Timeout"/> if <paramref name="timeout"/> elapses before all
        /// edges arrive.
        /// </summary>
        /// <param name="count">Number of edges to capture.</param>
        /// <param name="edge">Which edge to timestamp.</param>
        /// <param name="waitForEdge">If <c>true</c>, align the first sample with the first matching edge.</param>
        /// <param name="timeout">Maximum wait before giving up; <see cref="TimeSpan.Zero"/> means no timeout.</param>
        /// <exception cref="Exception">Thrown when a previous Capture is still running.</exception>
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

        /// <summary>
        /// Cancels the current <see cref="Generate(uint[], uint, int, uint, Edge)"/>,
        /// <see cref="ReadPulse"/>, or <see cref="Capture(uint, Edge, bool, TimeSpan)"/>. The
        /// corresponding <c>Finished</c> event still fires with <c>aborted</c> set to <c>true</c>.
        /// </summary>
        public void Abort() {

            var buffer = new uint[CMD_RAW_DATA_START_OFFSET];

            // length
            buffer[0] = (uint)buffer.Length * 4; // length in byte

            //param1
            buffer[1] = CMD_ABORT;

            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        
    }

    /// <summary>What a <see cref="PulseFeedbackController"/> measures.</summary>
    public enum PulseFeedbackMode {
        /// <summary>Measures how long a charged pin takes to drain through its pull-down (single-pin mode, no echo pin).</summary>
        DrainDuration = 0,
        /// <summary>Measures the duration of the response pulse on the echo pin after a trigger pulse is sent on the pulse pin.</summary>
        EchoDuration = 1,
        /// <summary>Measures the delay from the end of the trigger pulse to the first edge on the echo pin (classic HC-SR04 ultrasonic ranging).</summary>
        DurationUntilEcho = 2
    }
    /// <summary>
    /// Blocking pulse-and-measure helper for simple two-wire sensors (capacitive sensors,
    /// HC-SR04 ultrasonic rangers, etc.). <see cref="Trigger"/> sends a pulse on the pulse
    /// pin and waits for the result on the echo pin; measurement runs on the hardware
    /// timing helper for microsecond-grade timing.
    /// </summary>
    public class PulseFeedbackController : IDisposable {

        private readonly PulseFeedbackMode mode;

        private readonly int pulsePin;
        private readonly int echoPin;


        /// <summary>If <c>true</c>, disable GPIO interrupts around the measurement for less jitter. Defaults to <c>false</c>.</summary>
        public bool DisableInterrupts { get; set; }
        /// <summary>Maximum time <see cref="Trigger"/> waits for the echo before returning. Defaults to 100 ms.</summary>
        public TimeSpan Timeout { get; set; }
        /// <summary>Width of the trigger pulse on the pulse pin. Defaults to 20 ms.</summary>
        public TimeSpan PulseLength { get; set; }
        /// <summary>Active level of the pulse pin while the trigger is asserted. Defaults to <c>true</c> (high).</summary>
        public bool PulseValue { get; set; }
        /// <summary>Level the echo pin takes while asserted. Defaults to <c>true</c> (high).</summary>
        public bool EchoValue { get; set; }

        internal const int MODE_PULSEFEEDBACK = 3;

        /// <summary>Single-pin constructor; valid only for <see cref="PulseFeedbackMode.DrainDuration"/>.</summary>
        /// <param name="pin">The pin used for both the drive and the measurement.</param>
        /// <param name="mode">Must be <see cref="PulseFeedbackMode.DrainDuration"/>.</param>
        /// <exception cref="ArgumentException">Pin is negative, already reserved, or <paramref name="mode"/> requires an echo pin.</exception>
        public PulseFeedbackController(int pin, PulseFeedbackMode mode)
            : this(pin, -1, mode) {
        }

        /// <summary>Two-pin constructor for echo-based modes.</summary>
        /// <param name="pulsePin">Pin that drives the trigger pulse.</param>
        /// <param name="echoPin">Pin that is sampled for the response; must be &gt;= 0 unless <paramref name="mode"/> is <see cref="PulseFeedbackMode.DrainDuration"/>.</param>
        /// <param name="mode">Which measurement to perform on <see cref="Trigger"/>.</param>
        /// <exception cref="ArgumentException">Pins are invalid or already reserved, or <paramref name="mode"/> contradicts the pin layout.</exception>
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

            NativeRpmsgHelper.StartReceiving();

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
        /// <summary>
        /// Drives the pulse pin for <see cref="PulseLength"/>, measures the matching echo and returns it.
        /// Blocks until the measurement completes or <see cref="Timeout"/> elapses (polls every 10 ms).
        /// </summary>
        /// <returns>The measured duration (drain time, echo width, or trigger-to-echo delay depending on the mode).</returns>
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

        /// <summary>Unsubscribes from the timing helper and releases the pulse and echo pins.</summary>
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
