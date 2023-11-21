using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GHIElectronics.Endpoint.Core;


namespace GHIElectronics.Endpoint.Devices.DigitalSignal {
    public class DigitalSignalController {

        private const int CMD_RAW_DATA_START_OFFSET = 64;

        private const int CMD_GENERATE_PULSE = 0x10;
        private const int CMD_CHECK_BUSY = 0x30;
        private const int CMD_RESPONSE_DONE = 0x31;

        private const int MODE_GENERATE_PULSE = 0;
        private const int MODE_READ_PULSE = 1;
        private const int MODE_CAPTURE_PULSE = 2;


        public delegate void PulseGenerateEventHandler(DigitalSignalController sender, uint endState);
        public delegate void PulseReadEventHandler(DigitalSignalController sender, TimeSpan duration, uint count, bool initialState);
        public delegate void PulseCaptureEventHandler(DigitalSignalController sender, uint[] buffer, uint count, bool initialState);

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


                    if (this.pulseGenerateCallback != null) {
                        this.pulseGenerateCallback?.Invoke(this, edge);


                    }

                    this.CanGeneratePulse = true;
                }

                else if (mode == MODE_READ_PULSE) {
                    var durationTick = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var readPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 1];
                    var initialState = data[CMD_RAW_DATA_START_OFFSET / 2 + 2] == 1 ? true : false;
                    var durationTime = TimeSpan.FromTicks(durationTick);




                    if (this.pulseReadCallback != null) {
                        this.pulseReadCallback?.Invoke(this, durationTime, readPulseCount, initialState);


                    }

                    this.CanReadPulse = true;
                }

                if (mode == MODE_CAPTURE_PULSE) {

                    var capturedPulseCount = data[CMD_RAW_DATA_START_OFFSET / 2 + 0];
                    var initialState = data[CMD_RAW_DATA_START_OFFSET / 2 + 1] == 1 ? true : false;

                    var buffer = new uint[capturedPulseCount];

                    Array.Copy(data, CMD_RAW_DATA_START_OFFSET, buffer, 0, capturedPulseCount);


                    if (this.pulseCaptureCallback != null) {
                        this.pulseCaptureCallback?.Invoke(this, buffer, capturedPulseCount, initialState);


                    }

                    this.CanCapture = true;
                }




            }
        }

        ~DigitalSignalController() {
            if (File.Exists("/dev/rpmsg0"))
                NativeRpmsgHelper.Release();
        }

        public void Generate(uint[] data, uint offset, int count, uint multipler, bool edge) {
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
            buffer[4] = multipler;

            // param 5
            var e = edge == true ? 1 : 0;
            buffer[5] = (uint)e;

            var i = 0;
            while (i < count) {
                buffer[CMD_RAW_DATA_START_OFFSET + i] = data[offset + i];
                i++;

            }

            this.CanGeneratePulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);
        }

        public void ReadPulse(uint pulseNum, bool edge, bool waitForEdge) {
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
            buffer[4] = (edge == true) ? 1U : 0; ;

            // param 5      
            buffer[5] = waitForEdge == true ? 1U : 0;

            this.CanReadPulse = false;
            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }

        public void Capture(uint count, bool edge, bool waitForEdge) => this.Capture(count, edge, waitForEdge, TimeSpan.Zero);
        public void Capture(uint count, bool edge, bool waitForEdge, TimeSpan timeout) {

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
            buffer[4] = (edge == true) ? 1U : 0; ;

            // param 5      
            buffer[5] = waitForEdge == true ? 1U : 0;

            // param 6      
            buffer[6] = (uint)timeout.Ticks >> 0;

            // param 7  
            buffer[7] = (uint)(timeout.Ticks >> 32);

            this.CanCapture = false;

            NativeRpmsgHelper.Send(buffer, 0, buffer.Length);

        }
    }

}
