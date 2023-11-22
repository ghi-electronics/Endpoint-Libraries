using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;


namespace GHIElectronics.Endpoint.Devices.DigitalSignal {
    public class DigitalSignalController {

        private const int CMD_RAW_DATA_START_OFFSET = 64;

        private const int CMD_GENERATE_PULSE = 0x10;
        
        private const int CMD_RESPONSE_DONE = 0x30;
        private const int CMD_ABORT = 0x31;
        private const int CMD_CHECK_BUSY = 0x32;

        private const int MODE_GENERATE_PULSE = 0;
        private const int MODE_READ_PULSE = 1;
        private const int MODE_CAPTURE_PULSE = 2;

        private const int TIMER_CLOCK = 205626176;


        public delegate void PulseGenerateEventHandler(DigitalSignalController sender, uint endState, bool aborted);
        public delegate void PulseReadEventHandler(DigitalSignalController sender, TimeSpan duration, uint count, uint initialState, bool aborted);
        public delegate void PulseCaptureEventHandler(DigitalSignalController sender, double[] buffer, uint count, uint initialState, bool aborted);

        private PulseReadEventHandler pulseReadCallback;
        private PulseGenerateEventHandler pulseGenerateCallback;
        private PulseCaptureEventHandler pulseCaptureCallback;

        public event PulseReadEventHandler OnReadPulseFinished {
            add => this.pulseReadCallback += value;
            remove {
                if (this.pulseReadCallback != null)
                    this.pulseReadCallback -= value;
            }
        }

        public event PulseGenerateEventHandler OnGenerateFinished {
            add => this.pulseGenerateCallback += value;
            remove {
                if (this.pulseGenerateCallback != null)
                    this.pulseGenerateCallback -= value;
            }
        }

        public event PulseCaptureEventHandler OnCaptureFinished {
            add => this.pulseCaptureCallback += value;
            remove {
                if (this.pulseCaptureCallback != null)
                    this.pulseCaptureCallback -= value;
            }
        }

        public bool CanGeneratePulse { get; private set; } = true;
        public bool CanReadPulse { get; private set; } = true;

        public bool CanCapture { get; private set; } = true;



        private int pin;
        public DigitalSignalController(int pin) {



            if (File.Exists("/dev/rpmsg0")) {
                NativeRpmsgHelper.Release();
            }

            if (File.Exists("/dev/rpmsg_ctrl0") && !File.Exists("/dev/rpmsg0"))
                NativeRpmsgHelper.Acquire();
            else {
                throw new Exception("Could not start rpmsg");
            }



            this.pin = pin;
            NativeRpmsgHelper.DataReceived += this.NativeRpmsgHelper_DataReceived;

            NativeRpmsgHelper.TaskReceive();




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


                    if (this.pulseGenerateCallback != null) {
                        this.pulseGenerateCallback?.Invoke(this, edge, aborted);


                    }

                    this.CanGeneratePulse = true;
                }

                else if (mode == MODE_READ_PULSE) {
                    var durationTick = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var readPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 1];
                    var initialState = data[CMD_RAW_DATA_START_OFFSET / 2 + 2]; ;
                    var aborted = data[CMD_RAW_DATA_START_OFFSET / 2 + 3] == 0 ? false : true;
                    var durationTime = TimeSpan.FromTicks(durationTick);




                    if (this.pulseReadCallback != null) {
                        this.pulseReadCallback?.Invoke(this, durationTime, readPulseCount, initialState, aborted);


                    }

                    this.CanReadPulse = true;
                }

                if (mode == MODE_CAPTURE_PULSE) {

                    var capturedPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var initialState = data[CMD_RAW_DATA_START_OFFSET / 2 + 1];

                    var waitForEdge = data[CMD_RAW_DATA_START_OFFSET / 2 + 2] != 0 ? true : false;
                    var aborted = data[CMD_RAW_DATA_START_OFFSET / 2 + 3] == 0 ? false : true;
                    var startCopy = CMD_RAW_DATA_START_OFFSET;




                    var buffer = new uint[capturedPulseCount];

                    

                    Array.Copy(data, startCopy, buffer, 0, capturedPulseCount);


                    if (this.pulseCaptureCallback != null) {

                        //Convert to double
                        var d = new double[buffer.Length];
                        var scale = 4.863;

                        if (capturedPulseCount > 0) {
                            d[0] = buffer[0] * scale;

                            for (var i = 1; i < buffer.Length; i++) {
                                d[i] = (buffer[i] - buffer[i - 1]) * scale;
                            }

              
                        }
                        if (waitForEdge) {
                            var d2 = new double[d.Length-1];
                            Array.Copy(d, 1, d2, 0, d2.Length);

                            this.pulseCaptureCallback?.Invoke(this, d2, (uint)d2.Length, initialState, aborted);
                        }
                        else {
                            this.pulseCaptureCallback?.Invoke(this, d, capturedPulseCount, initialState, aborted);
                        }


                    }

                    this.CanCapture = true;
                }




            }
        }

        ~DigitalSignalController() {
            if (File.Exists("/dev/rpmsg0"))
                NativeRpmsgHelper.Release();
        }

        public void Generate(uint[] data, uint offset, int count, uint multiplier, uint edge) {

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

            var i = 0;
            while (i < count) {
                buffer[CMD_RAW_DATA_START_OFFSET + i] = data[offset + i];
                i++;

            }

            this.CanGeneratePulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);
        }

        public void ReadPulse(uint pulseNum, uint edge, bool waitForEdge) {
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
            buffer[4] = edge; ;

            // param 5      
            buffer[5] = waitForEdge == true ? 1U : 0;

            this.CanReadPulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        public void Capture(uint count, uint edge, bool waitForEdge) => this.Capture(count, edge, waitForEdge, TimeSpan.Zero);
        public void Capture(uint count, uint edge, bool waitForEdge, TimeSpan timeout) {

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
            buffer[4] = edge;

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

}
