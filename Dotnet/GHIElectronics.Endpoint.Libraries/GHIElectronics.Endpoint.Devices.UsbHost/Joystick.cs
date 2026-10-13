using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>Allows a usb device to be used as a joystick.</summary>
    /// <remarks>
    /// By default, the reported range for position is between -512 and 512, as on
    /// TinyCLR. Cursor1 is the left stick; Cursor2 is the right stick of a game pad.
    /// Buttons are numbered from 0. The events come on the joystick's own thread.
    /// <code>
    /// var joystick = new Joystick(e.DeviceName);    // from UsbHostController, DeviceType.Joystick
    /// joystick.CursorMoved += (sender, a) => Console.WriteLine($"{a.Which} at {a.NewPosition.X}, {a.NewPosition.Y}");
    /// joystick.ButtonChanged += (sender, a) => Console.WriteLine($"Button {a.Which} {a.State}");
    /// joystick.HatSwitchPressed += (sender, a) => Console.WriteLine($"Hat {a.Direction}");
    /// </code>
    /// </remarks>
    public class Joystick : BaseDevice {
        /// <summary>The delegate for when one of the joystick's button is pressed or released.</summary>
        public delegate void ButtonChangedEventHandler(Joystick sender, ButtonChangedEventArgs e);

        /// <summary>The delegate for when one of the joystick's cursors moves.</summary>
        public delegate void CursorMovedEventHandler(Joystick sender, CursorMovedEventArgs e);

        /// <summary>The delegate for when the joystick's hat switch is pressed.</summary>
        public delegate void HatSwitchPressedEventHandler(Joystick sender, HatSwitchPressedEventArgs e);

        /// <summary>The event fired when one of the joystick's buttons is pressed or released.</summary>
        public event ButtonChangedEventHandler? ButtonChanged;

        /// <summary>The event fired when the joystick's cursor has moved.</summary>
        public event CursorMovedEventHandler? CursorMoved;

        /// <summary>The event fired when the joystick's hat switch has been pressed.</summary>
        public event HatSwitchPressedEventHandler? HatSwitchPressed;

        /// <summary>Joystick capabilities.</summary>
        [Flags]
        public enum DeviceCapabilities : byte {
            /// <summary>Has X on cursor 1.</summary>
            CursorX = 0x01,

            /// <summary>Has Y on cursor 1.</summary>
            CursorY = 0x02,

            /// <summary>Has X on cursor 2.</summary>
            Cursor2X = 0x04,

            /// <summary>Has Y on cursor 2.</summary>
            Cursor2Y = 0x08,

            /// <summary>Has a hat switch.</summary>
            HatSwitch = 0x10,

            /// <summary>Has buttons.</summary>
            Buttons = 0x20,
        }

        /// <summary>Opens the joystick that UsbHostController reported ("/dev/input/event3").</summary>
        public Joystick(string deviceName) : base(deviceName, DeviceType.Joystick) {
        }

        /// <summary>The capabilities of the joystick.</summary>
        public DeviceCapabilities Capabilities => (DeviceCapabilities)this.capabilities;

        /// <summary>The current position of the first cursor.</summary>
        public Position CursorPosition => this.Get(Cursor.Cursor1, out _, out _);

        /// <summary>The current position of the second cursor.</summary>
        public Position Cursor2Position => this.Get(Cursor.Cursor2, out _, out _);

        /// <summary>The current direction of the hat switch.</summary>
        public HatSwitchDirection CurrentHatSwitchDirection {
            get {
                this.Get(Cursor.Cursor1, out var hat, out _);
                return hat;
            }
        }

        /// <summary>Sets the bounds for given cursor.</summary>
        public void SetCursorBounds(Cursor cursor, int minX, int maxX, int minY, int maxY) =>
            Libendpoint.Check(Libendpoint.InputSetCursorBounds(this.Handle(), (int)cursor, minX, maxX, minY, maxY));

        /// <summary>Gets the current button state.</summary>
        /// <param name="buttonNumber">The button number to query (0 to 31).</param>
        /// <returns>The state of the button.</returns>
        public ButtonState GetButtonState(int buttonNumber) {
            ArgumentOutOfRangeException.ThrowIfNegative(buttonNumber);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(buttonNumber, 31);
            this.Get(Cursor.Cursor1, out _, out var buttons);
            return (buttons & (1u << buttonNumber)) != 0 ? ButtonState.Pressed : ButtonState.Released;
        }

        private Position Get(Cursor cursor, out HatSwitchDirection hat, out uint buttons) {
            Libendpoint.Check(Libendpoint.InputJoystickState(this.Handle(), (int)cursor, out var x, out var y, out var direction, out buttons));
            hat = (HatSwitchDirection)direction;
            return new Position { X = x, Y = y };
        }

        private protected override void OnEvent(int kind, int a, int b, int c, int d, int e) {
            switch (kind) {
                case 5:
                    this.ButtonChanged?.Invoke(this, new ButtonChangedEventArgs { Which = a, State = (ButtonState)b });
                    break;
                case 6:
                    this.CursorMoved?.Invoke(this, new CursorMovedEventArgs {
                        Which = (Cursor)a,
                        NewPosition = new Position { X = b, Y = c },
                        Delta = new Position { X = d, Y = e },
                    });
                    break;
                case 7:
                    this.HatSwitchPressed?.Invoke(this, new HatSwitchPressedEventArgs { Direction = (HatSwitchDirection)a });
                    break;
            }
        }

        /// <summary>The events args for the ButtonPressed and ButtonReleased events.</summary>
        public class ButtonChangedEventArgs : EventArgs {
            /// <summary>The index of the changed button.</summary>
            public int Which { get; internal set; }

            /// <summary>The new state of the button.</summary>
            public ButtonState State { get; internal set; }
        }

        /// <summary>The events args for the CursorMoved event.</summary>
        public class CursorMovedEventArgs : EventArgs {
            /// <summary>Which cursor moved.</summary>
            public Cursor Which { get; internal set; }

            /// <summary>The new position of the cursor.</summary>
            public Position NewPosition { get; internal set; }

            /// <summary>The change from the last position.</summary>
            public Position Delta { get; internal set; }
        }

        /// <summary>The events args for the HatSwitchPressed event.</summary>
        public class HatSwitchPressedEventArgs : EventArgs {
            /// <summary>The new direction of the hat switch.</summary>
            public HatSwitchDirection Direction { get; internal set; }
        }
    }
}
