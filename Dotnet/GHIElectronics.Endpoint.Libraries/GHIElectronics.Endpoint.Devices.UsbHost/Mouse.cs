using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.UsbHost {
    /// <summary>Allows a usb device to be used a mouse.</summary>
    /// <remarks>
    /// By default, the reported range for position is between 0 and 512, as on
    /// TinyCLR: set it to the screen with SetCursorBounds. The events come on
    /// the mouse's own thread.
    /// <code>
    /// var mouse = new Mouse(e.DeviceName);    // from UsbHostController, DeviceType.Mouse
    /// mouse.SetCursorBounds(new Position { X = 0, Y = 0 }, new Position { X = 799, Y = 479 });
    /// mouse.CursorMoved += (sender, a) => Console.WriteLine($"Cursor at {a.NewPosition.X}, {a.NewPosition.Y}");
    /// mouse.ButtonChanged += (sender, a) => Console.WriteLine($"{a.Which} {a.State}");
    /// </code>
    /// </remarks>
    public class Mouse : BaseDevice {
        /// <summary>The delegate for when one of the mouse's button is pressed or released.</summary>
        public delegate void ButtonChangedEventHandler(Mouse sender, ButtonChangedEventArgs e);

        /// <summary>The delegate for when the mouse's wheel moves.</summary>
        public delegate void WheelMovedEventHandler(Mouse sender, WheelMovedEventArgs e);

        /// <summary>The delegate for when the mouse's cursor moves.</summary>
        public delegate void CursorMovedEventHandler(Mouse sender, CursorMovedEventArgs e);

        /// <summary>The event fired when one of the mouse's buttons is pressed or released.</summary>
        public event ButtonChangedEventHandler? ButtonChanged;

        /// <summary>The event fired when the mouse's wheel has moved.</summary>
        public event WheelMovedEventHandler? WheelMoved;

        /// <summary>The event fired when the mouse's cursor has moved.</summary>
        public event CursorMovedEventHandler? CursorMoved;

        /// <summary>Opens the mouse that UsbHostController reported ("/dev/input/event2").</summary>
        public Mouse(string deviceName) : base(deviceName, DeviceType.Mouse) {
        }

        /// <summary>The current state of the mouse's left button.</summary>
        public ButtonState LeftButtonState => this.Button(Buttons.Left);

        /// <summary>The current state of the mouse's right button.</summary>
        public ButtonState RightButtonState => this.Button(Buttons.Right);

        /// <summary>The current state of the mouse's middle button.</summary>
        public ButtonState MiddleButtonState => this.Button(Buttons.Middle);

        /// <summary>The current state of the mouse's first extended button.</summary>
        public ButtonState Extended1ButtonState => this.Button(Buttons.Extended1);

        /// <summary>The current state of the mouse's second extended button.</summary>
        public ButtonState Extended2ButtonState => this.Button(Buttons.Extended2);

        /// <summary>The current position of the cursor.</summary>
        public Position CursorPosition {
            get {
                Libendpoint.Check(Libendpoint.InputMouseState(this.Handle(), out var x, out var y, out _, out _));
                return new Position { X = x, Y = y };
            }
        }

        /// <summary>The current position of the mouse wheel.</summary>
        public int WheelPosition {
            get {
                Libendpoint.Check(Libendpoint.InputMouseState(this.Handle(), out _, out _, out var wheel, out _));
                return wheel;
            }
        }

        /// <summary>Return true for absolute, false for relative mode (a USB mouse moves relative).</summary>
        public bool AbsolutePosition => false;

        /// <summary>Scales the x, y, and wheel delta values.</summary>
        /// <param name="scale">The value by which to scale the data.</param>
        public void SetScale(float scale) => Libendpoint.Check(Libendpoint.InputSetScale(this.Handle(), scale));

        /// <summary>Sets the cursor bounds.</summary>
        /// <param name="minimum">The minimum bound.</param>
        /// <param name="maximum">The maximum bound.</param>
        public void SetCursorBounds(Position minimum, Position maximum) =>
            Libendpoint.Check(Libendpoint.InputSetCursorBounds(this.Handle(), 0, minimum.X, maximum.X, minimum.Y, maximum.Y));

        /// <summary>Sets the cursor position.</summary>
        /// <param name="newPosition">The new position of the cursor.</param>
        public void SetCursorPosition(Position newPosition) =>
            Libendpoint.Check(Libendpoint.InputSetCursorPosition(this.Handle(), newPosition.X, newPosition.Y));

        private ButtonState Button(Buttons button) {
            Libendpoint.Check(Libendpoint.InputMouseState(this.Handle(), out _, out _, out _, out var buttons));
            return (buttons & (int)button) != 0 ? ButtonState.Pressed : ButtonState.Released;
        }

        private protected override void OnEvent(int kind, int a, int b, int c, int d, int e) {
            switch (kind) {
                case 2:
                    this.ButtonChanged?.Invoke(this, new ButtonChangedEventArgs { Which = (Buttons)a, State = (ButtonState)b });
                    break;
                case 3:
                    this.CursorMoved?.Invoke(this, new CursorMovedEventArgs {
                        NewPosition = new Position { X = a, Y = b },
                        Delta = new Position { X = c, Y = d },
                    });
                    break;
                case 4:
                    this.WheelMoved?.Invoke(this, new WheelMovedEventArgs { NewPosition = a, Delta = b });
                    break;
            }
        }

        /// <summary>The events args for the ButtonPressed and ButtonReleased events.</summary>
        public class ButtonChangedEventArgs : EventArgs {
            /// <summary>Which button changed its state.</summary>
            public Buttons Which { get; set; }

            /// <summary>The new state of the button.</summary>
            public ButtonState State { get; set; }
        }

        /// <summary>The events args for the WheelMoved event.</summary>
        public class WheelMovedEventArgs : EventArgs {
            /// <summary>The new position of the wheel.</summary>
            public int NewPosition { get; set; }

            /// <summary>The change from the last position.</summary>
            public int Delta { get; set; }
        }

        /// <summary>The events args for the CursorMoved event.</summary>
        public class CursorMovedEventArgs : EventArgs {
            /// <summary>The new position of the cursor.</summary>
            public Position NewPosition { get; set; }

            /// <summary>The change from the last position.</summary>
            public Position Delta { get; set; }
        }
    }
}
