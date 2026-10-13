using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>Allows a usb device to be used as a keyboard.</summary>
    /// <remarks>
    /// The CharUp and CharDown events are only fired when a given key press has a
    /// corresponding ASCII key code (a US keyboard; Ctrl+A to Ctrl+Z give 1 to 26).
    /// A key held down gives one KeyDown. The events come on the keyboard's own thread.
    /// <code>
    /// var keyboard = new Keyboard(e.DeviceName);    // from UsbHostController, DeviceType.Keyboard
    /// keyboard.CharDown += (sender, a) => Console.Write(a.ASCII);
    /// keyboard.KeyDown += (sender, a) => { if (a.Which == Key.Escape) Console.WriteLine("Esc"); };
    /// </code>
    /// </remarks>
    public class Keyboard : BaseDevice {
        /// <summary>The handler for used for keyboard callbacks.</summary>
        public delegate void KeyboardEventHandler(Keyboard sender, KeyboardEventArgs args);

        /// <summary>Fired when a key is released.</summary>
        public event KeyboardEventHandler? KeyUp;

        /// <summary>Fired when a key is pressed.</summary>
        public event KeyboardEventHandler? KeyDown;

        /// <summary>Fired when a key is released and it can be converted to ASCII.</summary>
        public event KeyboardEventHandler? CharUp;

        /// <summary>Fired when a key is pressed and it can be converted to ASCII.</summary>
        public event KeyboardEventHandler? CharDown;

        /// <summary>Opens the keyboard that UsbHostController reported ("/dev/input/event1").</summary>
        public Keyboard(string deviceName) : base(deviceName, DeviceType.Keyboard) {
        }

        /// <summary>Gets the current state of a key.</summary>
        /// <param name="key">The key to query.</param>
        /// <returns>The state of the key.</returns>
        public KeyState IsKeyPressed(Key key) {
            Libendpoint.Check(Libendpoint.InputKeyState(this.Handle(), (int)key, out var state));
            return (KeyState)state;
        }

        private protected override void OnEvent(int kind, int a, int b, int c, int d, int e) {
            if (kind != 1) {
                return;
            }
            var args = new KeyboardEventArgs { Which = (Key)a, ASCII = (char)c };
            if ((KeyState)b == KeyState.Pressed) {
                this.KeyDown?.Invoke(this, args);
                if (args.ASCII != 0) {
                    this.CharDown?.Invoke(this, args);
                }
            }
            else {
                this.KeyUp?.Invoke(this, args);
                if (args.ASCII != 0) {
                    this.CharUp?.Invoke(this, args);
                }
            }
        }

        /// <summary>Event arguments for the keyboard events.</summary>
        public class KeyboardEventArgs : EventArgs {
            /// <summary>The Key associated with the event.</summary>
            public Key Which { get; set; }

            /// <summary>The ASCII representation of the key, if available. Otherwise, this value is <b>0</b>.</summary>
            public char ASCII { get; set; }
        }
    }
}
