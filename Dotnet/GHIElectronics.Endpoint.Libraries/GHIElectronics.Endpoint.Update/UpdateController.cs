using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Net;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Native;

namespace GHIElectronics.Endpoint.Update {
    public class UpdateController {
        public enum Mode: uint {
            ProgrameMMC=0,
            Firmware,
            //Bootloader, No need
            //Dotnet // Can't update dotnet when dotnet is running. Use EP tool instead.
        }

        string sourcePath;
        Mode mode;
        int pinIndicator;

        LibGpiodDriver gpioDriver;
        GpioController gpioController;

        bool updating;
        public UpdateController(Mode mode, int pinIndicator = EPM815.Gpio.Pin.NONE, string sourcePath = null) {           
            if (mode == Mode.ProgrameMMC) {
                if (!DeviceInformation.SdBoot()) {
                    throw new ArgumentException("Program eMMC requires boot from SD.");
                }
            }
            else {
                if (sourcePath == null || !Directory.Exists(sourcePath))
                    throw new Exception("Invalid source path");
            }

            if (pinIndicator != EPM815.Gpio.Pin.NONE) {
                var pinPort = pinIndicator / 16;
                var pinNumber = pinIndicator % 16;

                this.gpioDriver = new LibGpiodDriver(pinPort);
                this.gpioController = new GpioController(PinNumberingScheme.Logical, this.gpioDriver);

                this.gpioController.OpenPin(pinNumber);
                this.gpioController.SetPinMode(pinNumber, PinMode.Output);
            }

            this.mode = mode;
            this.sourcePath = sourcePath;
            this.pinIndicator = pinIndicator;
            this.updating = false;
        }

        public bool Update() {
            var argument = ((uint)this.mode).ToString() + " " + this.sourcePath;
            var script = new Script("ifu.sh", "./", argument);

            this.updating = true;

            if (this.pinIndicator != EPM815.Gpio.Pin.NONE) {
                this.TaskIndicator();
            }

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
