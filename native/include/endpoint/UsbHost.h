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
//
// A program's own devices: libusb for one with no driver (Unknown) and hidapi
// for HID devices; the board has both, and C/C++ projects have their headers:
//
//   #include <libusb-1.0/libusb.h>    // libusb_open_device_with_vid_pid(nullptr, e.VendorId(), e.ProductId())
//   #include <hidapi/hidapi.h>        // hid_open(e.VendorId(), e.ProductId(), nullptr)
#pragma once

#include <array>
#include <cstdint>
#include <cstdio>
#include <exception>
#include <span>
#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::UsbHost {

using DeviceConnectionStatus = Core::DeviceConnectionStatus;

// What a device gives, and its DeviceName
enum class DeviceType {
    Unknown = 0,      // no driver on the board: the program talks to it with libusb ("/dev/bus/usb/001/005")
    Hub = 1,          // "/dev/bus/usb/001/002"
    HID = 2,          // a HID device that is no keyboard, mouse or joystick ("/dev/hidraw0"): use hidapi
    Mouse = 3,        // "/dev/input/event2": open it with Mouse
    Keyboard = 4,     // "/dev/input/event1": open it with Keyboard
    Joystick = 5,     // joysticks and gamepads ("/dev/input/event3"): open it with Joystick
    MassStorage = 6,  // a drive: use StorageController (no name)
    Printer = 7,      // "/dev/usb/lp0": open it with Printer
    Serial = 8,       // a serial port, for SerialPort ("/dev/ttyUSB0", "/dev/ttyACM0")
    Webcam = 9,       // "/dev/video0"
    Audio = 10,       // a sound card: speaker, microphone ("hw:1"), for Media::SoundDevice
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

// The types of TinyCLR's USB host (GHIElectronics.TinyCLR.Devices.Usb): the
// same names and values. Key is the USB key code.
enum class Buttons : uint8_t { None = 0x00, Left = 0x01, Right = 0x02, Middle = 0x04, Extended1 = 0x08, Extended2 = 0x10 };
enum class ButtonState { Released = 0, Pressed = 1 };
enum class Cursor { Cursor1 = 0, Cursor2 = 1 };  // a joystick's left and right stick
enum class KeyState { Released = 2, Pressed = 1 };
enum class HatSwitchDirection { Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft, None };

// The keys on the keyboard: USB key codes (D0 to D9 are the number keys). A key
// TinyCLR has no name for (F13, media keys) comes as its USB key code.
enum class Key : uint8_t {
    None = 0x00, A = 0x04, B = 0x05, C = 0x06, D = 0x07, E = 0x08,
    F = 0x09, G = 0x0A, H = 0x0B, I = 0x0C, J = 0x0D, K = 0x0E,
    L = 0x0F, M = 0x10, N = 0x11, O = 0x12, P = 0x13, Q = 0x14,
    R = 0x15, S = 0x16, T = 0x17, U = 0x18, V = 0x19, W = 0x1A,
    X = 0x1B, Y = 0x1C, Z = 0x1D, D1 = 0x1E, D2 = 0x1F, D3 = 0x20,
    D4 = 0x21, D5 = 0x22, D6 = 0x23, D7 = 0x24, D8 = 0x25, D9 = 0x26,
    D0 = 0x27, Enter = 0x28, Escape = 0x29, BackSpace = 0x2A, Tab = 0x2B, Space = 0x2C,
    Substract = 0x2D, Equal = 0x2E, OpenBrackets = 0x2F, CloseBrackets = 0x30, Backslash = 0x31, NonUS = 0x32,
    Semicolon = 0x33, Quotes = 0x34, GraveAccent = 0x35, Comma = 0x36, Period = 0x37, Divide = 0x38,
    CapsLock = 0x39, F1 = 0x3A, F2 = 0x3B, F3 = 0x3C, F4 = 0x3D, F5 = 0x3E,
    F6 = 0x3F, F7 = 0x40, F8 = 0x41, F9 = 0x42, F10 = 0x43, F11 = 0x44,
    F12 = 0x45, PrintScreen = 0x46, ScrollLock = 0x47, Pause = 0x48, Insert = 0x49, Home = 0x4A,
    PageUp = 0x4B, Delete = 0x4C, End = 0x4D, PageDown = 0x4E, RightArrow = 0x4F, LeftArrow = 0x50,
    DownArrow = 0x51, UpArrow = 0x52, NumLock = 0x53, KeypadDivide = 0x54, KeypadMultiply = 0x55, KeypadSubstract = 0x56,
    KeypadAdd = 0x57, KeypadEnter = 0x58, Keypad1 = 0x59, Keypad2 = 0x5A, Keypad3 = 0x5B, Keypad4 = 0x5C,
    Keypad5 = 0x5D, Keypad6 = 0x5E, Keypad7 = 0x5F, Keypad8 = 0x60, Keypad9 = 0x61, Keypad0 = 0x62,
    KeypadDelete = 0x63, NonUS2 = 0x64, Application = 0x65, LeftCtrl = 0xE0, LeftShift = 0xE1, LeftAlt = 0xE2,
    LeftGUI = 0xE3, RightCtrl = 0xE4, RightShift = 0xE5, RightAlt = 0xE6, RightGUI = 0xE7,
};

struct Position {
    int X = 0;
    int Y = 0;
    int Z = 0;

    friend Position operator-(const Position& a, const Position& b) { return {a.X - b.X, a.Y - b.Y, a.Z - b.Z}; }
    bool operator==(const Position&) const = default;
};

// A USB keyboard, mouse, joystick or printer (Keyboard, Mouse, Joystick, Printer),
// opened with the DeviceName its UsbHostController event gives ("/dev/input/event2"). Its events
// come on the device's own thread. It cannot be copied or moved: keep it in a
// std::unique_ptr (or a variable that lives long enough).
class BaseDevice {
public:
    virtual ~BaseDevice() { Dispose(); }
    BaseDevice(const BaseDevice&) = delete;
    BaseDevice& operator=(const BaseDevice&) = delete;

    // What the device was opened with: "/dev/input/event2", "/dev/usb/lp0"
    const std::string& DeviceName() const { return name_; }
    DeviceType Type() const { return type_; }
    uint16_t VendorId() const { return vendorId_; }
    uint16_t ProductId() const { return productId_; }

    bool Connected() const {
        int vendorId, productId, connected = 0, capabilities;
        if (type_ == DeviceType::Printer) {
            Core::detail::Check(endpoint_printer_info(Get(), &vendorId, &productId, &connected));
        }
        else {
            Core::detail::Check(endpoint_input_info(Get(), &vendorId, &productId, &connected, &capabilities));
        }
        return connected != 0;
    }

    // The device was unplugged
    Core::Event<BaseDevice&> Disconnected;

    // Stops the device's events (one being delivered finishes first)
    void Dispose() {
        if (handle_ != nullptr) {
            (type_ == DeviceType::Printer ? endpoint_printer_close : endpoint_input_close)(handle_);
            handle_ = nullptr;
        }
    }

protected:
    BaseDevice(std::string deviceName, DeviceType type) : name_(std::move(deviceName)), type_(type) {}

    // Called last by the constructors of Mouse, Keyboard, Joystick and Printer: the events can come from now on
    void Open() {
        int vendorId = 0, productId = 0, connected = 0;
        if (type_ == DeviceType::Printer) {
            Core::detail::Check(endpoint_printer_open(name_.c_str(), &Unplugged, this, &handle_));
            Core::detail::Check(endpoint_printer_info(handle_, &vendorId, &productId, &connected));
        }
        else {
            Core::detail::Check(endpoint_input_open(name_.c_str(), static_cast<int>(type_), &Changed, this, &handle_));
            Core::detail::Check(endpoint_input_info(handle_, &vendorId, &productId, &connected, &capabilities_));
        }
        vendorId_ = static_cast<uint16_t>(vendorId);
        productId_ = static_cast<uint16_t>(productId);
    }

    void* Get() const {
        if (handle_ == nullptr) {
            throw Core::ObjectDisposedException("The device is disposed");
        }
        return handle_;
    }

    // A keyboard's, mouse's or joystick's events (see Input::Event in libendpoint)
    virtual void OnEvent(int, int, int, int, int, int) {}

    int capabilities_ = 0;

private:
    // From the device's thread. A handler's error must not stop the events: it
    // is printed, as .NET prints an unhandled exception.
    static void Changed(void* context, int kind, int a, int b, int c, int d, int e) {
        auto self = static_cast<BaseDevice*>(context);
        try {
            if (kind == 8) {
                self->Disconnected(*self);
            }
            else {
                self->OnEvent(kind, a, b, c, d, e);
            }
        }
        catch (const std::exception& error) {
            std::fprintf(stderr, "Unhandled exception in a USB device event: %s\n", error.what());
        }
    }

    static void Unplugged(void* context) {
        auto self = static_cast<BaseDevice*>(context);
        try {
            self->Disconnected(*self);
        }
        catch (const std::exception& error) {
            std::fprintf(stderr, "Unhandled exception in Disconnected: %s\n", error.what());
        }
    }

    std::string name_;
    DeviceType type_;
    uint16_t vendorId_ = 0;
    uint16_t productId_ = 0;
    void* handle_ = nullptr;
};

// A USB mouse. The cursor stays between 0 and 512 at first, as on TinyCLR: set
// it to the screen with SetCursorBounds.
//
//   auto mouse = std::make_unique<Mouse>(e.DeviceName());   // DeviceType::Mouse
//   mouse->SetCursorBounds({0, 0}, {799, 479});
//   mouse->CursorMoved += [](Mouse&, const Mouse::CursorMovedEventArgs& a) {
//       std::printf("Cursor at %d, %d\n", a.NewPosition().X, a.NewPosition().Y);
//   };
class Mouse : public BaseDevice {
public:
    class ButtonChangedEventArgs {
    public:
        ButtonChangedEventArgs(Buttons which, ButtonState state) : which_(which), state_(state) {}
        Buttons Which() const { return which_; }
        ButtonState State() const { return state_; }

    private:
        Buttons which_;
        ButtonState state_;
    };

    class WheelMovedEventArgs {
    public:
        WheelMovedEventArgs(int newPosition, int delta) : newPosition_(newPosition), delta_(delta) {}
        int NewPosition() const { return newPosition_; }
        int Delta() const { return delta_; }

    private:
        int newPosition_;
        int delta_;
    };

    class CursorMovedEventArgs {
    public:
        CursorMovedEventArgs(Position newPosition, Position delta) : newPosition_(newPosition), delta_(delta) {}
        Position NewPosition() const { return newPosition_; }
        Position Delta() const { return delta_; }

    private:
        Position newPosition_;
        Position delta_;
    };

    explicit Mouse(const std::string& deviceName) : BaseDevice(deviceName, DeviceType::Mouse) { Open(); }
    ~Mouse() override { Dispose(); }

    // A button was pressed or released; the wheel moved; the cursor moved
    Core::Event<Mouse&, const ButtonChangedEventArgs&> ButtonChanged;
    Core::Event<Mouse&, const WheelMovedEventArgs&> WheelMoved;
    Core::Event<Mouse&, const CursorMovedEventArgs&> CursorMoved;

    ButtonState LeftButtonState() const { return Button(Buttons::Left); }
    ButtonState RightButtonState() const { return Button(Buttons::Right); }
    ButtonState MiddleButtonState() const { return Button(Buttons::Middle); }
    ButtonState Extended1ButtonState() const { return Button(Buttons::Extended1); }
    ButtonState Extended2ButtonState() const { return Button(Buttons::Extended2); }

    Position CursorPosition() const {
        auto s = State();
        return {s[0], s[1]};
    }
    int WheelPosition() const { return State()[2]; }
    bool AbsolutePosition() const { return false; }  // a USB mouse moves relative

    // Scales the x, y, and wheel delta values
    void SetScale(float scale) { Core::detail::Check(endpoint_input_set_scale(Get(), scale)); }
    void SetCursorBounds(Position minimum, Position maximum) {
        Core::detail::Check(endpoint_input_set_cursor_bounds(Get(), 0, minimum.X, maximum.X, minimum.Y, maximum.Y));
    }
    void SetCursorPosition(Position newPosition) {
        Core::detail::Check(endpoint_input_set_cursor_position(Get(), newPosition.X, newPosition.Y));
    }

protected:
    void OnEvent(int kind, int a, int b, int c, int d, int) override {
        switch (kind) {
            case 2: ButtonChanged(*this, ButtonChangedEventArgs(static_cast<Buttons>(a), static_cast<ButtonState>(b))); break;
            case 3: CursorMoved(*this, CursorMovedEventArgs({a, b}, {c, d})); break;
            case 4: WheelMoved(*this, WheelMovedEventArgs(a, b)); break;
        }
    }

private:
    std::array<int, 4> State() const {
        std::array<int, 4> s{};
        Core::detail::Check(endpoint_input_mouse_state(Get(), &s[0], &s[1], &s[2], &s[3]));
        return s;
    }
    ButtonState Button(Buttons button) const {
        return (State()[3] & static_cast<int>(button)) ? ButtonState::Pressed : ButtonState::Released;
    }
};

// A USB keyboard. CharDown and CharUp come only for keys with an ASCII character
// (a US keyboard; Ctrl+A to Ctrl+Z give 1 to 26). A key held down gives one KeyDown.
//
//   auto keyboard = std::make_unique<Keyboard>(e.DeviceName());   // DeviceType::Keyboard
//   keyboard->CharDown += [](Keyboard&, const Keyboard::KeyboardEventArgs& a) { std::putchar(a.ASCII()); };
class Keyboard : public BaseDevice {
public:
    class KeyboardEventArgs {
    public:
        KeyboardEventArgs(Key which, char ascii) : which_(which), ascii_(ascii) {}
        Key Which() const { return which_; }
        // The ASCII character, 0 if the key has none
        char ASCII() const { return ascii_; }

    private:
        Key which_;
        char ascii_;
    };

    explicit Keyboard(const std::string& deviceName) : BaseDevice(deviceName, DeviceType::Keyboard) { Open(); }
    ~Keyboard() override { Dispose(); }

    Core::Event<Keyboard&, const KeyboardEventArgs&> KeyUp;
    Core::Event<Keyboard&, const KeyboardEventArgs&> KeyDown;
    Core::Event<Keyboard&, const KeyboardEventArgs&> CharUp;
    Core::Event<Keyboard&, const KeyboardEventArgs&> CharDown;

    KeyState IsKeyPressed(Key key) const {
        int state = 0;
        Core::detail::Check(endpoint_input_key_state(Get(), static_cast<int>(key), &state));
        return static_cast<KeyState>(state);
    }

protected:
    void OnEvent(int kind, int a, int b, int c, int, int) override {
        if (kind != 1) {
            return;
        }
        KeyboardEventArgs args(static_cast<Key>(a), static_cast<char>(c));
        if (static_cast<KeyState>(b) == KeyState::Pressed) {
            KeyDown(*this, args);
            if (c != 0) {
                CharDown(*this, args);
            }
        }
        else {
            KeyUp(*this, args);
            if (c != 0) {
                CharUp(*this, args);
            }
        }
    }
};

// A USB joystick or game pad. The sticks go from -512 to 512 at first, as on
// TinyCLR: Cursor1 is the left stick, Cursor2 the right one. Buttons are
// numbered from 0.
//
//   auto joystick = std::make_unique<Joystick>(e.DeviceName());   // DeviceType::Joystick
//   joystick->ButtonChanged += [](Joystick&, const Joystick::ButtonChangedEventArgs& a) {
//       std::printf("Button %d %s\n", a.Which(), a.State() == ButtonState::Pressed ? "pressed" : "released");
//   };
class Joystick : public BaseDevice {
public:
    enum DeviceCapabilities : uint8_t {
        CursorX = 0x01,
        CursorY = 0x02,
        Cursor2X = 0x04,
        Cursor2Y = 0x08,
        HatSwitch = 0x10,
        Buttons = 0x20,
    };

    class ButtonChangedEventArgs {
    public:
        ButtonChangedEventArgs(int which, ButtonState state) : which_(which), state_(state) {}
        int Which() const { return which_; }  // the button's number
        ButtonState State() const { return state_; }

    private:
        int which_;
        ButtonState state_;
    };

    class CursorMovedEventArgs {
    public:
        CursorMovedEventArgs(Cursor which, Position newPosition, Position delta)
            : which_(which), newPosition_(newPosition), delta_(delta) {}
        Cursor Which() const { return which_; }
        Position NewPosition() const { return newPosition_; }
        Position Delta() const { return delta_; }

    private:
        Cursor which_;
        Position newPosition_;
        Position delta_;
    };

    class HatSwitchPressedEventArgs {
    public:
        explicit HatSwitchPressedEventArgs(HatSwitchDirection direction) : direction_(direction) {}
        HatSwitchDirection Direction() const { return direction_; }

    private:
        HatSwitchDirection direction_;
    };

    explicit Joystick(const std::string& deviceName) : BaseDevice(deviceName, DeviceType::Joystick) { Open(); }
    ~Joystick() override { Dispose(); }

    Core::Event<Joystick&, const ButtonChangedEventArgs&> ButtonChanged;
    Core::Event<Joystick&, const CursorMovedEventArgs&> CursorMoved;
    Core::Event<Joystick&, const HatSwitchPressedEventArgs&> HatSwitchPressed;

    uint8_t Capabilities() const { return static_cast<uint8_t>(capabilities_); }
    Position CursorPosition() const { return State(Cursor::Cursor1).position; }
    Position Cursor2Position() const { return State(Cursor::Cursor2).position; }
    HatSwitchDirection CurrentHatSwitchDirection() const { return State(Cursor::Cursor1).hat; }

    void SetCursorBounds(Cursor cursor, int minX, int maxX, int minY, int maxY) {
        Core::detail::Check(endpoint_input_set_cursor_bounds(Get(), static_cast<int>(cursor), minX, maxX, minY, maxY));
    }

    // The state of a button (0 to 31)
    ButtonState GetButtonState(int buttonNumber) const {
        if (buttonNumber < 0 || buttonNumber > 31) {
            throw Core::ArgumentOutOfRangeException("The button number must be 0 to 31");
        }
        return (State(Cursor::Cursor1).buttons >> buttonNumber) & 1 ? ButtonState::Pressed : ButtonState::Released;
    }

protected:
    void OnEvent(int kind, int a, int b, int c, int d, int e) override {
        switch (kind) {
            case 5: ButtonChanged(*this, ButtonChangedEventArgs(a, static_cast<ButtonState>(b))); break;
            case 6: CursorMoved(*this, CursorMovedEventArgs(static_cast<Cursor>(a), {b, c}, {d, e})); break;
            case 7: HatSwitchPressed(*this, HatSwitchPressedEventArgs(static_cast<HatSwitchDirection>(a))); break;
        }
    }

private:
    struct Snapshot {
        Position position;
        HatSwitchDirection hat;
        uint32_t buttons;
    };
    Snapshot State(Cursor cursor) const {
        int x = 0, y = 0, hat = 0;
        uint32_t buttons = 0;
        Core::detail::Check(endpoint_input_joystick_state(Get(), static_cast<int>(cursor), &x, &y, &hat, &buttons));
        return {{x, y}, static_cast<HatSwitchDirection>(hat), buttons};
    }
};

// What a printer reports about itself
enum class PrinterStatus { Ready = 0, PaperOut = 0x01, Offline = 0x02, Error = 0x04 };  // flags

// A USB printer: the bytes it understands (ESC/POS for receipt printers, ZPL or
// EPL for label printers, PCL or PostScript for office printers) go to it as they are.
//
//   auto printer = std::make_unique<Printer>(e.DeviceName());   // DeviceType::Printer
//   printer->Write("Hello\n\n\n");
class Printer : public BaseDevice {
public:
    explicit Printer(const std::string& deviceName) : BaseDevice(deviceName, DeviceType::Printer) { Open(); }
    ~Printer() override { Dispose(); }

    // How long Write waits for the printer to take the data, in milliseconds (-1: as long as it takes)
    int WriteTimeout() const { return writeTimeout_; }
    void WriteTimeout(int milliseconds) { writeTimeout_ = milliseconds; }

    // The printer's IEEE 1284 Device ID: "MFG:EPSON;CMD:ESC/POS;MDL:TM-T20II;CLS:PRINTER;"
    std::string DeviceId() const {
        char text[1024];
        Core::detail::Check(endpoint_printer_device_id(Get(), text, sizeof text));
        return text;
    }

    // PaperOut, Offline and Error flags, as the printer reports them (Ready: none)
    PrinterStatus Status() const {
        int status = 0;
        Core::detail::Check(endpoint_printer_status(Get(), &status));
        return static_cast<PrinterStatus>(status);
    }

    // Sends bytes (or text) to the printer: all of them, or a TimeoutException after WriteTimeout
    int Write(std::span<const uint8_t> data) {
        int written = 0;
        Core::detail::Check(endpoint_printer_write(Get(), data.data(), static_cast<int>(data.size()), writeTimeout_, &written));
        return written;
    }
    int Write(const std::string& text) {
        return Write(std::span<const uint8_t>(reinterpret_cast<const uint8_t*>(text.data()), text.size()));
    }

    // What the printer answers (to a status request): its length, 0 if nothing came in timeoutMs (-1: no limit)
    int Read(std::span<uint8_t> buffer, int timeoutMs = -1) {
        int length = 0;
        Core::detail::Check(endpoint_printer_read(Get(), buffer.data(), static_cast<int>(buffer.size()), timeoutMs, &length));
        return length;
    }

private:
    int writeTimeout_ = -1;
};

}  // namespace GHIElectronics::Endpoint::Devices::UsbHost
