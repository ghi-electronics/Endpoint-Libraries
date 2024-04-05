using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Devices.Dac {
    public class DacController : IDisposable {
        static int initializeCount = 0;

        public DacController(int dacPin) {
            if (dacPin != EPM815.Gpio.Pin.PA5) {
                throw new ArgumentException("Pin invalid");
            }

            if (dacPin == EPM815.Gpio.Pin.PA5) {
                this.channelNum = 2;
            }

            this.channelPath = string.Format("/sys/bus/iio/devices/iio:device{0}", this.channelNum);

            this.Acquire();

            this.scale = this.GetScale();

            this.Powerdown(false);

        }

        private int channelNum;
        private string channelPath;
        private double scale;
        public int MaxValue { get; } = 4095;
        public int MinValue { get; } = 0;

        public double Scale() => this.scale;
        private double GetScale() {
            var script = new Script("cat", "./", string.Format("{0}/out_voltage{1}_scale", this.channelPath, this.channelNum));

            script.Start();

            return double.Parse(script.Output);
        }
        public void WriteValue(int value) {
            if (value < 0 || value > this.MaxValue) {
                throw new ArgumentException("Value out of range");
            }


            var script = new Script("write_dac.sh", "./", string.Format("{0} {1}/out_voltage{2}_raw", value, this.channelPath, this.channelNum));
            script.Start();
        }

        private void Powerdown(bool enable) {
            var v = enable ? 1 : 0;
            var script = new Script("write_dac.sh", "./", string.Format("{0} {1}/out_voltage{2}_powerdown", v, this.channelPath, this.channelNum));
            script.Start();
        }

        public void WriteValue(double ratio) => this.WriteValue((int)(ratio * (this.MaxValue - this.MinValue) + this.MinValue));

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

            if (!Directory.Exists(this.channelPath)) {
                // Load DAC module
                var script = new Script("modprobe", "./", "stm32-dac-core");
                script.Start();

                script = new Script("modprobe", "./", "stm32-dac");
                script.Start();

            }

        }

        private void UnLoadResources() {
            this.Powerdown(true);

            var script = new Script("rmmod", "./", "stm32-dac-core");
            script.Start();

            script = new Script("rmmod", "./", "stm32-dac");
            script.Start();

        }

        private bool disposed = false;

        /// <exclude />
        ~DacController() {
            this.Dispose(disposing: false);
        }

        public void Dispose() {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
        protected void Dispose(bool disposing) {
            if (this.disposed)
                return;

            if (disposing) {

            }


            this.Release();

            this.disposed = true;
        }
    }
}
