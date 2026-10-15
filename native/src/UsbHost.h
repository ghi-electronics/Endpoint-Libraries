#pragma once

// USB devices plugged into the board (directly or through hubs): connected
// and disconnected events, with what the device is and what the board made
// of it. The kernel has the drivers; this only reports. One event per thing
// the device gives: a keyboard with a touchpad gives Keyboard and Mouse, a
// webcam gives Webcam and Audio (its microphone). Each event has the name to
// open it with: "/dev/ttyUSB0" (Serial), "eth1" (Network), "/dev/input/event3"
// (Keyboard, Mouse, Joystick), "/dev/video0" (Webcam), "hw:1" (Audio),
// "/dev/usb/lp0" (Printer), "/dev/hidraw0" (HID), "hci0" (Bluetooth),
// "/dev/bus/usb/001/005" (Hub, Unknown: no driver, the program talks to it).
// USB drives are reported as MassStorage with no name: StorageController
// owns them.
//
// The board has no udev: the library loads the driver of a device that has
// none yet (the board's agent does the same, a few seconds after boot).

#include <cstdint>
#include <functional>
#include <string>

namespace GHIElectronics::Endpoint::Internal::UsbHost {

// The values of DeviceType (.NET, Python, C++)
enum Type : int {
    Unknown = 0,
    Hub = 1,
    Hid = 2,
    Mouse = 3,
    Keyboard = 4,
    Joystick = 5,
    MassStorage = 6,
    Printer = 7,
    Serial = 8,
    Webcam = 9,
    Audio = 10,
    Network = 11,
    Bluetooth = 12,
    Touch = 13,
};

enum Status : int { Disconnected = 0, Connected = 1 };

struct DeviceEvent {
    int status = Disconnected;
    uint32_t id = 0;  // the device's: the same for all its events, new when it is plugged in again
    int type = Unknown;
    int vendorId = 0;
    int productId = 0;
    std::string deviceName;
    std::string manufacturer;
    std::string product;
    std::string serialNumber;
};

// After Enable, the callback gets every device connected (first the ones
// already there) or disconnected, on the library's USB thread
class Controller {
public:
    using Callback = std::function<void(const DeviceEvent& event)>;

    explicit Controller(Callback callback);
    ~Controller();
    Controller(const Controller&) = delete;
    Controller& operator=(const Controller&) = delete;

    void Enable();
    void Disable();

    void Notify(const DeviceEvent& event) const { callback(event); }

private:
    Callback callback;
};

}  // namespace GHIElectronics::Endpoint::Internal::UsbHost
