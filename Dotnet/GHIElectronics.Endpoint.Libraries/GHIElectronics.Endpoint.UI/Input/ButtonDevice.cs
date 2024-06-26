////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#define TRACK_BUTTON_STATE

using System;
using System.Collections;

namespace GHIElectronics.Endpoint.UI.Input {
    /// <summary>
    ///     The ButtonDevice class represents the button device to the
    ///     members of a context.
    /// </summary>
    public sealed class ButtonDevice : InputDevice {
        internal ButtonDevice(InputManager inputManager) {
            this._inputManager = inputManager;

            this._inputManager.InputDeviceEvents[(int)InputManager.InputDeviceType.Button].PreNotifyInput += new NotifyInputEventHandler(this.PreNotifyInput);
            this._inputManager.InputDeviceEvents[(int)InputManager.InputDeviceType.Button].PostProcessInput += new ProcessInputEventHandler(this.PostProcessInput);

            this._isEnabledOrVisibleChangedEventHandler = new PropertyChangedEventHandler(this.OnIsEnabledOrVisibleChanged);
        }

        /// <summary>
        ///     Returns the element that input from this device is sent to.
        /// </summary>
        public override UIElement Target => this._focus;

        public override InputManager.InputDeviceType DeviceType => InputManager.InputDeviceType.Button;

        /// <summary>
        ///     Focuses the button input on a particular element.
        /// </summary>
        /// <param name="element">
        ///     The element to focus the button pad on.
        /// </param>
        /// <returns>Element focused to</returns>
        public UIElement Focus(UIElement obj) {
            this.VerifyAccess();

            var forceToNullIfFailed = false;

            // Make sure that the element is enabled.  This includes all parents.
            var enabled = true;
            var visible = true;
            if (obj != null) {
                enabled = obj.IsEnabled;
                visible = obj.IsVisible;

                if ((enabled && visible) && forceToNullIfFailed) {
                    obj = null;
                    enabled = true;
                    visible = true;
                }
            }

            if ((enabled && visible) && obj != this._focus) {
                // go ahead and change our internal sense of focus to the desired element.
                this.ChangeFocus(obj, DateTime.UtcNow);
            }

            return this._focus;
        }

        /// <summary>
        ///     Returns whether or not the specified button is down.
        /// </summary>
        public bool IsButtonDown(HardwareButton button) => this.GetButtonState(button) == ButtonState.Down;

        /// <summary>
        ///     Returns whether or not the specified button is up.
        /// </summary>
        public bool IsButtonUp(HardwareButton button) => this.GetButtonState(button) == ButtonState.None;

        /// <summary>
        ///     Returns whether or not the specified button is held.
        /// </summary>
        public bool IsButtonHeld(HardwareButton button) => this.GetButtonState(button) == ButtonState.Held;

        /// <summary>
        ///     Returns the state of the specified button.
        /// </summary>
        public ButtonState GetButtonState(HardwareButton button) {
#if TRACK_BUTTON_STATE         

            if (this._buttonStateList.Count != 0) {

                foreach (var bt in this._buttonStateList) {
                    var hardwareButtonState = bt as HardwareButtonState;

                    if (hardwareButtonState.Button == button) {

                        return hardwareButtonState.State;

                    }
                }

            }
            return ButtonState.None;
#else
            return ButtonState.None;
#endif
        }

#if TRACK_BUTTON_STATE

        private class HardwareButtonState {
            public HardwareButton Button { get; }
            public ButtonState State { get; set; }
            public HardwareButtonState(HardwareButton button, ButtonState state) {
                this.Button = button;
                this.State = state;
            }

        }
        internal void SetButtonState(HardwareButton button, ButtonState state) {
            //If the PreNotifyInput event sent by the InputManager is always sent by the
            //correct thread, this is redundant. Also, why is this function 'internal'
            //when we only access it from inside this class?
            this.VerifyAccess();            

            var foundButton = false;

            foreach (var bt in this._buttonStateList) {
                var hardwareButtonState = bt as HardwareButtonState;

                if (hardwareButtonState.Button == button) {

                    foundButton = true;
                    break;

                }
            }

            if (!foundButton) {

                var hardwareButtonState = new HardwareButtonState(button, state);
                this._buttonStateList.Add(hardwareButtonState);
            }

            foreach (var bt in this._buttonStateList) {
                var hardwareButtonState = bt as HardwareButtonState;

                if (hardwareButtonState.Button == button) {

                    hardwareButtonState.State = state;

                    break;

                }
            }
        }

#endif

        private void ChangeFocus(UIElement focus, DateTime timestamp) {
            if (focus != this._focus) {
                // Update the critical pieces of data.
                var oldFocus = this._focus;
                this._focus = focus;
                this._focusRootUIElement = focus?.RootUIElement;

                // Adjust the handlers we use to track everything.
                if (oldFocus != null) {
                    oldFocus.IsEnabledChanged -= this._isEnabledOrVisibleChangedEventHandler;
                    oldFocus.IsVisibleChanged -= this._isEnabledOrVisibleChangedEventHandler;
                }

                if (focus != null) {
                    focus.IsEnabledChanged += this._isEnabledOrVisibleChangedEventHandler;
                    focus.IsVisibleChanged += this._isEnabledOrVisibleChangedEventHandler;
                }

                // Send the LostFocus and GotFocus events.
                if (oldFocus != null) {
                    var lostFocus = new FocusChangedEventArgs(this, timestamp, oldFocus, focus) {
                        RoutedEvent = Buttons.LostFocusEvent,
                        Source = oldFocus
                    };

                    this._inputManager.ProcessInput(lostFocus);
                }

                if (focus != null) {
                    var gotFocus = new FocusChangedEventArgs(this, timestamp, oldFocus, focus) {
                        RoutedEvent = Buttons.GotFocusEvent,
                        Source = focus
                    };

                    this._inputManager.ProcessInput(gotFocus);
                }
            }
        }

        private void OnIsEnabledOrVisibleChanged(object sender, PropertyChangedEventArgs e) =>
            // The element with focus just became disabled or non-visible
            //
            // We can't leave focus on a disabled element, so move it.
            //
            // Will need to change this for watch, but this solution is for aux now.

            this.Focus(this._focus.Parent);

        private void PreNotifyInput(object sender, NotifyInputEventArgs e) {
            var buttonInput = this.ExtractRawButtonInputReport(e, InputManager.PreviewInputReportEvent);
            if (buttonInput != null) {
                this.CheckForDisconnectedFocus();
                /*

REFACTOR --

                the keyboard device is only active per app domain basis -- so like if your app domain doesn't have
                focus your keyboard device is not going to give you the real state of the keyboard.

                When it gets focus, it needs to know about this somehow.   We could use this keyboard action
                type stuff to do so.  Though this design really seem to be influenced a lot from living in
                the windows world.

                Essentially the input stack is being used like a message pump to say, hey dude you can
                use the keyboard now -- it's not real input, it's more or less a message.

                It could be interesting for elements to know about this -- since I think
                they will probalby still have focus (or do they get a Got and Lost Focus when the keyboard activates -- I don't think so,
                we need to know what we were focused on when the window gets focus again.

                So maybe elements want to stop some animation or something when input focus moves away from the activesource, and
                start them again later.  Could be interesting.
*/

                if ((buttonInput.Actions & RawButtonActions.Activate) == RawButtonActions.Activate) {
                    //System.Console.WriteLine("Initializing the button state.");

#if TRACK_BUTTON_STATE
                    // Clear out our key state storage.
                    //for (var i = 0; i < this._buttonState.Length; i++) {
                    //    this._buttonState[i] = 0;
                    //}

                    foreach (var bt in this._buttonStateList) {
                        var hardwareButtonState = bt as HardwareButtonState;

                        hardwareButtonState.State = ButtonState.None;


                    }

#endif
                    // we are now active.
                    // we should track which source is active so we don't confuse deactivations.
                    this._isActive = true;
                }

                // Generally, we need to check against redundant actions.
                // We never prevet the raw event from going through, but we
                // will only generate the high-level events for non-redundant
                // actions.  We store the set of non-redundant actions in
                // the dictionary of this event.

                // If the input is reporting a button down, the action is never
                // considered redundant.
                if ((buttonInput.Actions & RawButtonActions.ButtonDown) == RawButtonActions.ButtonDown) {
                    var actions = this.GetNonRedundantActions(e);
                    actions |= RawButtonActions.ButtonDown;
                    e.StagingItem.SetData(this._tagNonRedundantActions, actions);

                    // Pass along the button that was pressed, and update our state.
                    e.StagingItem.SetData(this._tagButton, buttonInput.Button);

#if TRACK_BUTTON_STATE
                    var buttonState = this.GetButtonState(buttonInput.Button);

                    if ((buttonState & ButtonState.Down) == ButtonState.Down) {
                        buttonState = ButtonState.Down | ButtonState.Held;
                    }
                    else {
                        buttonState |= ButtonState.Down;
                    }

                    this.SetButtonState(buttonInput.Button, buttonState);
#endif

                    // Tell the InputManager that the MostRecentDevice is us.
                    if (this._inputManager != null && this._inputManager.MostRecentInputDevice != this) {
                        this._inputManager.MostRecentInputDevice = (InputDevice)this;
                    }
                }

                // need to detect redundant ups.
                if ((buttonInput.Actions & RawButtonActions.ButtonUp) == RawButtonActions.ButtonUp) {
                    var actions = this.GetNonRedundantActions(e);
                    actions |= RawButtonActions.ButtonUp;
                    e.StagingItem.SetData(this._tagNonRedundantActions, actions);

                    // Pass along the button that was pressed, and update our state.
                    e.StagingItem.SetData(this._tagButton, buttonInput.Button);

#if TRACK_BUTTON_STATE
                    var buttonState = this.GetButtonState(buttonInput.Button);

                    if ((buttonState & ButtonState.Down) == ButtonState.Down) {
                        buttonState &= (~ButtonState.Down) & (ButtonState.Down | ButtonState.Held);
                    }
                    else {
                        buttonState |= ButtonState.Held;
                    }

                    this.SetButtonState(buttonInput.Button, buttonState);
#endif

                    // Tell the InputManager that the MostRecentDevice is us.
                    if (this._inputManager != null && this._inputManager.MostRecentInputDevice != this) {
                        this._inputManager.MostRecentInputDevice = (InputDevice)this;
                    }
                }
            }

            // On ButtonDown, we might need to set the Repeat flag

            if (e.StagingItem.Input.RoutedEvent == Buttons.PreviewButtonDownEvent) {
                this.CheckForDisconnectedFocus();

                var args = (ButtonEventArgs)e.StagingItem.Input;

                // Is this the same as the previous button?
                if (this._previousButton == args.Button) {
                    // Yes, this is a repeat (we got the buttondown for it twice, with no ButtonUp in between)
                    // what about chording?
                    args._isRepeat = true;
                }

                // Otherwise, keep this button to check against next time.
                else {
                    this._previousButton = args.Button;
                    args._isRepeat = false;
                }

            }

            // On ButtonUp, we clear Repeat flag
            else if (e.StagingItem.Input.RoutedEvent == Buttons.PreviewButtonUpEvent) {
                this.CheckForDisconnectedFocus();

                var args = (ButtonEventArgs)e.StagingItem.Input;
                args._isRepeat = false;

                // Clear _previousButton, so that down/up/down/up doesn't look like a repeat
                this._previousButton = HardwareButton.None;

            }
        }

        private void PostProcessInput(object sender, ProcessInputEventArgs e) {
            // PreviewButtonDown --> ButtonDown
            if (e.StagingItem.Input.RoutedEvent == Buttons.PreviewButtonDownEvent) {
                this.CheckForDisconnectedFocus();

                if (!e.StagingItem.Input.Handled) {
                    var previewButtonDown = (ButtonEventArgs)e.StagingItem.Input;
                    var buttonDown = new ButtonEventArgs(this, previewButtonDown.InputSource, previewButtonDown.Timestamp, previewButtonDown.Button) {
                        _isRepeat = previewButtonDown.IsRepeat,
                        RoutedEvent = Buttons.ButtonDownEvent
                    };

                    e.PushInput(buttonDown, e.StagingItem);
                }
            }

            // PreviewButtonUp --> ButtonUp
            if (e.StagingItem.Input.RoutedEvent == Buttons.PreviewButtonUpEvent) {
                this.CheckForDisconnectedFocus();

                if (!e.StagingItem.Input.Handled) {
                    var previewButtonUp = (ButtonEventArgs)e.StagingItem.Input;

                    var buttonUp = new ButtonEventArgs(this, previewButtonUp.InputSource, previewButtonUp.Timestamp, previewButtonUp.Button) {
                        RoutedEvent = Buttons.ButtonUpEvent
                    };

                    e.PushInput(buttonUp, e.StagingItem);
                }
            }

            var buttonInput = this.ExtractRawButtonInputReport(e, InputManager.InputReportEvent);
            if (buttonInput != null) {
                this.CheckForDisconnectedFocus();

                if (!e.StagingItem.Input.Handled) {
                    // In general, this is where we promote the non-redundant
                    // reported actions to our premier events.
                    var actions = this.GetNonRedundantActions(e);

                    // Raw --> PreviewButtonDown
                    if ((actions & RawButtonActions.ButtonDown) == RawButtonActions.ButtonDown) {
                        var button = (HardwareButton)e.StagingItem.GetData(this._tagButton);
                        if (button != HardwareButton.None) {
                            var previewButtonDown = new ButtonEventArgs(this, buttonInput.InputSource, buttonInput.Timestamp, button) {
                                RoutedEvent = Buttons.PreviewButtonDownEvent
                            };
                            e.PushInput(previewButtonDown, e.StagingItem);
                        }
                    }

                    // Raw --> PreviewButtonUp
                    if ((actions & RawButtonActions.ButtonUp) == RawButtonActions.ButtonUp) {
                        var button = (HardwareButton)e.StagingItem.GetData(this._tagButton);
                        if (button != HardwareButton.None) {
                            var previewButtonUp = new ButtonEventArgs(this, buttonInput.InputSource, buttonInput.Timestamp, button) {
                                RoutedEvent = Buttons.PreviewButtonUpEvent
                            };
                            e.PushInput(previewButtonUp, e.StagingItem);
                        }
                    }
                }

                // Deactivate
                if ((buttonInput.Actions & RawButtonActions.Deactivate) == RawButtonActions.Deactivate) {
                    if (this._isActive) {
                        this._isActive = false;

                        // Even if handled, a button deactivate results in a lost focus.
                        this.ChangeFocus(null, e.StagingItem.Input.Timestamp);
                    }
                }
            }
        }

        private RawButtonActions GetNonRedundantActions(NotifyInputEventArgs e) {
            RawButtonActions actions;

            // The CLR throws a null-ref exception if it tries to unbox a
            // null.  So we have to special case that.
            var o = e.StagingItem.GetData(this._tagNonRedundantActions);
            if (o != null) {
                actions = (RawButtonActions)o;
            }
            else {
                actions = new RawButtonActions();
            }

            return actions;
        }

        // at the moment we don't have a good way of detecting when an
        // element gets deleted from the tree (logical or visual).  The
        // best we can do right now is clear out the focus if we detect
        // that the tree containing the focus was disconnected.
        private bool CheckForDisconnectedFocus() {
            var wasDisconnected = false;

            if (this._focus != null && this._focus.RootUIElement != this._focusRootUIElement) {
                wasDisconnected = true;

                // need to remove this for the watch, placed here for aux now.
                this.Focus(this._focusRootUIElement);
            }

            return wasDisconnected;
        }

        private RawButtonInputReport ExtractRawButtonInputReport(NotifyInputEventArgs e, RoutedEvent Event) {
            RawButtonInputReport buttonInput = null;

            if (e.StagingItem.Input is InputReportEventArgs input) {
                if (input.Report is RawButtonInputReport && input.RoutedEvent == Event) {
                    buttonInput = (RawButtonInputReport)input.Report;
                }
            }

            return buttonInput;
        }

        private InputManager _inputManager;
        private bool _isActive;

        private UIElement _focus;
        private UIElement _focusRootUIElement;
        private HardwareButton _previousButton;

        private PropertyChangedEventHandler _isEnabledOrVisibleChangedEventHandler;

#if TRACK_BUTTON_STATE
        // Device state we track

        private ArrayList _buttonStateList = new ArrayList(); //new byte[(int)HardwareButton.LastSystemDefinedButton / 4];
#endif

        // Data tags for information we pass around the staging area.
        private object _tagNonRedundantActions = new object();
        private object _tagButton = new object();
    }
}


