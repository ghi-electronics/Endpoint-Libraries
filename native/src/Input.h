#pragma once

// USB keyboards, mice and joysticks (the UsbHostController's events give
// their DeviceName, "/dev/input/event2"), with TinyCLR's behavior: keys as
// USB key codes with their ASCII character (US layout), a mouse cursor kept
// inside bounds (0 to 512 at first), joystick sticks scaled to -512..512,
// the hat switch, numbered buttons. Each open device has a thread that reads
// it and calls the callback; a device that is pulled out gives Disconnected.

#include <cstdint>
#include <functional>
#include <memory>
#include <string>

namespace GHIElectronics::Endpoint::Internal::Input {

// DeviceType values
enum Kind : int { Mouse = 3, Keyboard = 4, Joystick = 5 };

// What the callback gets: (event, a, b, c, d, e)
enum Event : int {
    KeyChanged = 1,             // a: Key (USB key code), b: KeyState (1 pressed, 2 released), c: ASCII (0: none)
    MouseButtonChanged = 2,     // a: Buttons (one bit), b: ButtonState (0 released, 1 pressed)
    MouseCursorMoved = 3,       // a, b: new X, Y; c, d: change of X, Y
    MouseWheelMoved = 4,        // a: new position, b: change
    JoystickButtonChanged = 5,  // a: button number, b: ButtonState
    JoystickCursorMoved = 6,    // a: Cursor (0, 1); b, c: new X, Y; d, e: change of X, Y
    JoystickHatSwitch = 7,      // a: HatSwitchDirection
    Disconnected = 8,
};

using Callback = std::function<void(int event, int a, int b, int c, int d, int e)>;

struct State;

class Device {
public:
    // deviceName: "/dev/input/event2"; kind: what the program uses it as
    Device(const std::string& deviceName, int kind, Callback callback);
    // Stops the events (one being delivered finishes first)
    ~Device();
    Device(const Device&) = delete;
    Device& operator=(const Device&) = delete;

    int VendorId() const;
    int ProductId() const;
    bool Connected() const;
    // A joystick's DeviceCapabilities (TinyCLR's flags)
    int Capabilities() const;

    // 1 pressed, 2 released (TinyCLR's KeyState)
    int KeyState(int key) const;
    void GetMouse(int& x, int& y, int& wheel, int& buttons) const;
    void GetJoystick(int cursor, int& x, int& y, int& hat, uint32_t& buttons) const;

    void SetCursorBounds(int cursor, int minX, int maxX, int minY, int maxY);
    void SetCursorPosition(int x, int y);
    void SetScale(double scale);

private:
    std::shared_ptr<State> state;
};

}  // namespace GHIElectronics::Endpoint::Internal::Input
