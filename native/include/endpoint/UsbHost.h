// USB devices for Endpoint, with the same API as .NET's
// GHIElectronics.Endpoint.Devices.UsbHost:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::UsbHost;
//
//   auto usb = UsbHostController::GetDefault();
//   usb.OnConnectionChangedEvent += [](UsbHostController& sender, const UsbDeviceConnectionEventArgs& e) {
//       if (e.DeviceStatus() == DeviceConnectionStatus::Connected && e.Type() == DeviceType::Serial) {
//           std::printf("USB serial port %s\n", e.DeviceName().c_str());   // "/dev/ttyUSB0"
//       }
//   };
//   usb.Enable();   // the devices already plugged in are reported too
//
// One event for each thing a device gives: a keyboard with a touchpad gives
// Keyboard and Mouse, a webcam gives Webcam and Audio (its microphone).
// DeviceName is what to open it with (see DeviceType). The events come on the
// library's USB thread. USB drives come as MassStorage: use StorageController
// (Storage.h) for them.
#pragma once

#include <cstdint>
#include <cstdio>
#include <exception>
#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::UsbHost {

using DeviceConnectionStatus = Core::DeviceConnectionStatus;

// What a device gives, and its DeviceName
enum class DeviceType {
    Unknown = 0,      // no driver on the board: the program talks to it ("/dev/bus/usb/001/005")
    Hub = 1,          // "/dev/bus/usb/001/002"
    HID = 2,          // a HID device that is no keyboard, mouse or joystick ("/dev/hidraw0")
    Mouse = 3,        // "/dev/input/event2"
    Keyboard = 4,     // "/dev/input/event1"
    Joystick = 5,     // joysticks and gamepads ("/dev/input/event3")
    MassStorage = 6,  // a drive: use StorageController (no name)
    Printer = 7,      // "/dev/usb/lp0"
    Serial = 8,       // a serial port, for SerialPort ("/dev/ttyUSB0", "/dev/ttyACM0")
    Webcam = 9,       // "/dev/video0"
    Audio = 10,       // a sound card: speaker, microphone ("hw:1")
    Network = 11,     // an Ethernet, WiFi or cellular adapter: the network interface ("eth1", "wlan1")
    Bluetooth = 12,   // "hci0"
};

class UsbDeviceConnectionEventArgs {
public:
    UsbDeviceConnectionEventArgs(DeviceConnectionStatus status, uint32_t id, DeviceType type, uint16_t vendorId,
                                 uint16_t productId, std::string deviceName, std::string manufacturer,
                                 std::string product, std::string serialNumber)
        : status_(status), id_(id), type_(type), vendorId_(vendorId), productId_(productId),
          deviceName_(std::move(deviceName)), manufacturer_(std::move(manufacturer)), product_(std::move(product)),
          serialNumber_(std::move(serialNumber)) {}

    DeviceConnectionStatus DeviceStatus() const { return status_; }

    // The device's number: the same for all its events, a new one when it is plugged in again
    uint32_t Id() const { return id_; }

    DeviceType Type() const { return type_; }
    uint16_t VendorId() const { return vendorId_; }
    uint16_t ProductId() const { return productId_; }

    // What to open: "/dev/ttyUSB0", "eth1"... (see DeviceType)
    const std::string& DeviceName() const { return deviceName_; }

    // The device's own names ("Logitech", "USB Receiver"), empty if it has none
    const std::string& Manufacturer() const { return manufacturer_; }
    const std::string& Product() const { return product_; }
    const std::string& SerialNumber() const { return serialNumber_; }

private:
    DeviceConnectionStatus status_;
    uint32_t id_;
    DeviceType type_;
    uint16_t vendorId_;
    uint16_t productId_;
    std::string deviceName_;
    std::string manufacturer_;
    std::string product_;
    std::string serialNumber_;
};

class UsbHostController {
public:
    static UsbHostController GetDefault() { return UsbHostController(); }

    ~UsbHostController() { Dispose(); }
    UsbHostController(const UsbHostController&) = delete;
    UsbHostController& operator=(const UsbHostController&) = delete;

    // A device connected or disconnected. += gives an id for -=.
    Core::Event<UsbHostController&, const UsbDeviceConnectionEventArgs&> OnConnectionChangedEvent;

    // Starts reporting devices: first the ones already plugged in
    void Enable() { Core::detail::Check(endpoint_usbhost_enable(handle_.Get("UsbHostController"))); }
    void Disable() { Core::detail::Check(endpoint_usbhost_disable(handle_.Get("UsbHostController"))); }

    // Stops the events (one being delivered finishes first)
    void Dispose() { handle_.Reset(); }

private:
    UsbHostController() {
        void* handle = nullptr;
        Core::detail::Check(endpoint_usbhost_create(&Changed, this, &handle));
        handle_ = Handle(handle);
    }

    // From the library's USB thread. A handler's error must not stop the
    // events: it is printed, as .NET prints an unhandled exception.
    static void Changed(void* context, int status, uint32_t id, int type, int vendorId, int productId,
                        const char* deviceName, const char* manufacturer, const char* product,
                        const char* serialNumber) {
        auto self = static_cast<UsbHostController*>(context);
        try {
            self->OnConnectionChangedEvent(
                *self, UsbDeviceConnectionEventArgs(static_cast<DeviceConnectionStatus>(status), id,
                                                    static_cast<DeviceType>(type), static_cast<uint16_t>(vendorId),
                                                    static_cast<uint16_t>(productId), deviceName ? deviceName : "",
                                                    manufacturer ? manufacturer : "", product ? product : "",
                                                    serialNumber ? serialNumber : ""));
        }
        catch (const std::exception& e) {
            std::fprintf(stderr, "Unhandled exception in OnConnectionChangedEvent: %s\n", e.what());
        }
    }

    using Handle = Core::detail::Handle<endpoint_usbhost_dispose>;

    Handle handle_;  // last: closed first, before the event goes away
};

}  // namespace GHIElectronics::Endpoint::Devices::UsbHost
