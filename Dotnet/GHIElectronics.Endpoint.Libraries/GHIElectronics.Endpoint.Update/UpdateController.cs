using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Net;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint.Update {
    public class UpdateController {
        public enum Mode: uint {
            SdToMmc=0,
            Firmware,
            Bootloader,
            Dotnet
        }

        string sourcePath;
        Mode mode;
        int pinIndicator;

        LibGpiodDriver gpioDriver;
        GpioController gpioController;

        bool updating;
        public UpdateController(Mode mode, string sourcePath, int pinIndicator) {
           

            if (sourcePath == null || !Directory.Exists(sourcePath))
                throw new Exception("invalid source path");

            var pinPort = pinIndicator / 16;
            var pinNumber = pinIndicator % 16;

            this.gpioDriver = new LibGpiodDriver(pinPort);
            this.gpioController = new GpioController(PinNumberingScheme.Logical, this.gpioDriver);

            this.gpioController.OpenPin(pinNumber);
            this.gpioController.SetPinMode(pinNumber, PinMode.Output);

            this.mode = mode;
            this.sourcePath = sourcePath;
            this.pinIndicator = pinIndicator;
            this.updating = false;
        }

        public bool Update() {
            var argument = ((uint)this.mode).ToString() + " " + this.sourcePath;
            var script = new Script("ifu.sh", "./", argument);

            this.updating = true;

            this.TaskIndicator();

            script.Start();

            this.updating = false;

            if (script.Output.Contains("Error"))
                return false;

            return true;
        }

        private Task TaskIndicator() {

            var pinNumber = this.pinIndicator % 16;

            return Task.Run(() => {
               while (this.updating) {

                    this.gpioController.Write(pinNumber, PinValue.High);
                    Thread.Sleep(100);

                    this.gpioController.Write(pinNumber, PinValue.Low);
                    Thread.Sleep(100);
                }
            }
            ); ;

        }




    }
}
