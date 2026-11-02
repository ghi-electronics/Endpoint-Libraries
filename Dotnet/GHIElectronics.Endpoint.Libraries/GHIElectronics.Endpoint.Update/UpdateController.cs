using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Net;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Native;

namespace GHIElectronics.Endpoint.Update {
    /// <summary>
    /// In-field update helper that invokes <c>ifu.sh</c> on the board.
    /// Supports flashing the eMMC image from an SD-card boot, and installing a new firmware payload
    /// from a local directory. An optional GPIO pin is blinked at 10 Hz while the update runs.
    /// </summary>
    public class UpdateController {
        /// <summary>What <see cref="UpdateController"/> is asked to update.</summary>
        public enum Mode: uint {
            /// <summary>Program the on-board eMMC from the SD card. The board must currently be booted from SD; see <see cref="GHIElectronics.Endpoint.Native.DeviceInformation.SdBoot"/>.</summary>
            ProgramEmmc=0,
            /// <summary>Install a new firmware payload from the <c>sourcePath</c> directory supplied to the constructor.</summary>
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
        /// <summary>
        /// Prepares an update. For <see cref="Mode.ProgramEmmc"/>, verifies the board is currently SD-booted
        /// and throws otherwise. For <see cref="Mode.Firmware"/>, verifies <paramref name="sourcePath"/> exists.
        /// If <paramref name="pinIndicator"/> is set, that pin is opened as an output for the activity blinker.
        /// </summary>
        /// <param name="mode">What to update; see <see cref="Mode"/>.</param>
        /// <param name="pinIndicator">Optional GPIO pin (an <c>EPM815.Gpio.Pin</c> constant) toggled while the update runs. Defaults to <c>EPM815.Gpio.Pin.NONE</c> which disables the blinker.</param>
        /// <param name="sourcePath">Directory containing the firmware payload. Required for <see cref="Mode.Firmware"/>; ignored for <see cref="Mode.ProgramEmmc"/>.</param>
        public UpdateController(Mode mode, int pinIndicator = EPM815.Gpio.Pin.NONE, string sourcePath = null) {
            if (mode == Mode.ProgramEmmc) {
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
                this.gpioController = new GpioController(this.gpioDriver);

                this.gpioController.OpenPin(pinNumber);
                this.gpioController.SetPinMode(pinNumber, PinMode.Output);
            }

            this.mode = mode;
            this.sourcePath = sourcePath;
            this.pinIndicator = pinIndicator;
            this.updating = false;
        }

        /// <summary>
        /// Runs the update synchronously by invoking <c>ifu.sh</c> with the mode and source path.
        /// Blinks the indicator pin at 10 Hz while the script is running.
        /// </summary>
        /// <returns><c>true</c> if the script output does not contain the word <c>Error</c>; <c>false</c> otherwise.</returns>
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
