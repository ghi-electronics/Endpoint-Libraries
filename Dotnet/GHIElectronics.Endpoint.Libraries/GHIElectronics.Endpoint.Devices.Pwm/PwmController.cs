using System.Device.Pwm;
using GHIElectronics.Endpoint.Pins;
using static GHIElectronics.Endpoint.Pins.STM32MP1;

namespace GHIElectronics.Endpoint.Devices.Pwm {
    public class PwmController : PwmChannel {
        static int initializeCount = 0;
        private int controllerId;
        private int channelId;
        private int pinId;
        private int alternateId;

        PwmChannel pwmChannel = default!;
        //public PwmController(int controllerId, int channelId, int frequency, double dutyCyclePercentage) {
        //    this.pwmChannel = Create(STM32MP1.Pwm.ToActualController(controllerId), channelId, frequency, dutyCyclePercentage);
        //    this.controllerId = controllerId;
        //    this.channelId = channelId;

        //    this.Acquire();

        //    if (this.pwmChannel == null) {
        //        this.Release(); 
        //        throw new Exception("Could not create device");
        //    }



        //}

        public PwmController(int pin, int frequency, double dutyCyclePercentage) {
            this.controllerId = (pin >> 24) & 0xFF;
            this.channelId = (pin >> 16) & 0xFF;
            this.pinId = (pin >> 8) & 0xFF;
            this.alternateId = (pin >> 0) & 0xFF;

            this.pwmChannel = Create(STM32MP1.Pwm.ToActualController(controllerId), channelId, frequency, dutyCyclePercentage);
            
            this.Acquire();

            if (this.pwmChannel == null) {
                this.Release();
                throw new Exception("Could not create device");
            }



        }
        public override int Frequency { get => this.pwmChannel.Frequency; set => this.pwmChannel.Frequency = value; }
        public override double DutyCycle { get => this.pwmChannel.DutyCycle; set => this.pwmChannel.DutyCycle = value; }

        public override void Start() => this.pwmChannel.Start();
        public override void Stop() => this.pwmChannel.Stop();

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
            //var pinConfig = STM32MP1.Pwm.PinSettings[this.controllerId][this.channelId];

            STM32MP1.GpioPin.SetModer(this.pinId, STM32MP1.Moder.Alternate);
            STM32MP1.GpioPin.SetAlternate(this.pinId, (Alternate)this.alternateId);
            
            // load driver
         
        }

        private void UnLoadResources() {
            // releaset pins 
            //var pinConfig = STM32MP1.Pwm.PinSettings[this.controllerId][this.channelId];

            STM32MP1.GpioPin.SetModer(this.pinId, STM32MP1.Moder.Input);
           
        }

        private bool disposed = false;
        ~PwmController() {
            this.Dispose(disposing: false);
        }
        protected override void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {
                this.pwmChannel.Stop();
                this.pwmChannel.Dispose();
            }

            this.Release();

            this.disposed = true;
        }
    }
}
