#include "Input.h"

#include <fcntl.h>
#include <linux/input.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <algorithm>
#include <atomic>
#include <bitset>
#include <cerrno>
#include <climits>
#include <mutex>
#include <thread>
#include <vector>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Input {

namespace {

// The kernel's key numbers to USB key codes (TinyCLR's Key): the reverse of
// the kernel's own table (drivers/hid/hid-input.c, hid_keyboard)
constexpr uint8_t UsbKey[256] = {
    0x00, 0x29, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x2D, 0x2E, 0x2A, 0x2B,
    0x14, 0x1A, 0x08, 0x15, 0x17, 0x1C, 0x18, 0x0C, 0x12, 0x13, 0x2F, 0x30, 0x28, 0xE0, 0x04, 0x16,
    0x07, 0x09, 0x0A, 0x0B, 0x0D, 0x0E, 0x0F, 0x33, 0x34, 0x35, 0xE1, 0x31, 0x1D, 0x1B, 0x06, 0x19,
    0x05, 0x11, 0x10, 0x36, 0x37, 0x38, 0xE5, 0x55, 0xE2, 0x2C, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E,
    0x3F, 0x40, 0x41, 0x42, 0x43, 0x53, 0x47, 0x5F, 0x60, 0x61, 0x56, 0x5C, 0x5D, 0x5E, 0x57, 0x59,
    0x5A, 0x5B, 0x62, 0x63, 0x00, 0x94, 0x64, 0x44, 0x45, 0x87, 0x92, 0x93, 0x8A, 0x88, 0x8B, 0x8C,
    0x58, 0xE4, 0x54, 0x46, 0xE6, 0x00, 0x4A, 0x52, 0x4B, 0x50, 0x4F, 0x4D, 0x51, 0x4E, 0x49, 0x4C,
    0x00, 0x7F, 0x81, 0x80, 0x66, 0x67, 0x00, 0x48, 0x00, 0x85, 0x90, 0x91, 0x89, 0xE3, 0xE7, 0x65,
    0x78, 0x79, 0x76, 0x7A, 0x77, 0x7C, 0x74, 0x7D, 0x7E, 0x7B, 0x75, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0xB6, 0xB7, 0x00, 0x00, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70,
    0x71, 0x72, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
};

// USB key codes
constexpr int KeyCapsLock = 0x39, KeyNumLock = 0x53;
constexpr int KeyLeftCtrl = 0xE0, KeyLeftShift = 0xE1, KeyLeftAlt = 0xE2;
constexpr int KeyRightCtrl = 0xE4, KeyRightShift = 0xE5, KeyRightAlt = 0xE6;

// TinyCLR's HatSwitchDirection
enum Hat : int { Up = 0, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft, NoDirection };

// TinyCLR's Joystick.DeviceCapabilities
enum Capability : int { CursorX = 0x01, CursorY = 0x02, Cursor2X = 0x04, Cursor2Y = 0x08, HatSwitch = 0x10, Buttons = 0x20 };

// A key's character on a US keyboard (0: none). Ctrl+A to Ctrl+Z give 1 to 26.
int Ascii(int key, bool shift, bool capsLock, bool numLock, bool ctrl, bool alt) {
    if (alt) {
        return 0;
    }
    if (key >= 0x04 && key <= 0x1D) {  // A to Z
        if (ctrl) {
            return key - 0x04 + 1;
        }
        return (shift != capsLock ? 'A' : 'a') + (key - 0x04);
    }
    if (ctrl) {
        return 0;
    }
    if (key >= 0x1E && key <= 0x27) {
        return (shift ? "!@#$%^&*()" : "1234567890")[key - 0x1E];
    }
    if (key >= 0x2D && key <= 0x38 && key != 0x32) {
        return (shift ? "_+{}|~:\"~<>?" : "-=[]\\#;'`,./")[key - 0x2D];
    }
    switch (key) {
        case 0x28: return '\r';
        case 0x29: return 0x1B;
        case 0x2A: return '\b';
        case 0x2B: return '\t';
        case 0x2C: return ' ';
        case 0x54: return '/';
        case 0x55: return '*';
        case 0x56: return '-';
        case 0x57: return '+';
        case 0x58: return '\r';
    }
    if (numLock && key >= 0x59 && key <= 0x63) {
        return "1234567890."[key - 0x59];
    }
    return 0;
}

// The bits an input device has (EVIOCGBIT, EVIOCGKEY...)
class Bits {
public:
    bool Has(int bit) const {
        return bit >= 0 && bit < static_cast<int>(sizeof words * 8) && (words[bit / LongBits] >> (bit % LongBits)) & 1;
    }
    void* Data() { return words; }
    static constexpr size_t Size = (KEY_MAX + 1 + 7) / 8;

private:
    static constexpr int LongBits = sizeof(unsigned long) * 8;
    unsigned long words[(KEY_MAX + LongBits) / LongBits] = {};
};

Bits Get(int fd, unsigned long request) {
    Bits bits;
    ioctl(fd, request, bits.Data());
    return bits;
}

struct Axis {
    int code = -1;  // none
    int min = 0, max = 0, value = 0;
};

struct Out {
    int event, a = 0, b = 0, c = 0, d = 0, e = 0;
};

}  // namespace

struct State {
    int fd = -1;
    int wake = -1;
    int kind = 0;
    std::string name;
    Callback callback;
    std::thread thread;
    std::atomic<bool> stop{false};
    mutable std::mutex mutex;
    bool connected = true;
    int vendorId = 0, productId = 0;

    // Keyboard
    std::bitset<256> pressed;
    bool capsLock = false, numLock = false;

    // Mouse: TinyCLR's 0..512 at first
    int x = 0, y = 0, wheel = 0, buttons = 0;
    int minX = 0, maxX = 512, minY = 0, maxY = 512;
    double scale = 1, restX = 0, restY = 0, restWheel = 0;
    int movedX = 0, movedY = 0, movedWheel = 0;           // since the last report
    std::vector<std::pair<int, int>> changedButtons;      // since the last report

    // Joystick: TinyCLR's -512..512 at first
    Axis axes[2][2];                                      // [cursor][x, y]
    int bounds[2][4] = {{-512, 512, -512, 512}, {-512, 512, -512, 512}};
    int positions[2][2] = {};                             // as reported
    int hatX = 0, hatY = 0, hat = NoDirection;
    int dpad = 0;                                         // up 1, down 2, left 4, right 8
    bool hasHat = false, hasDpad = false;
    std::vector<int> buttonCodes;                         // button number -> key code
    uint32_t joystickButtons = 0, reportedButtons = 0;
    int capabilities = 0;

    ~State() {
        if (fd >= 0) {
            close(fd);
        }
        if (wake >= 0) {
            close(wake);
        }
    }

    int Scale(int cursor, int axis) const {
        const Axis& a = axes[cursor][axis];
        int lo = bounds[cursor][axis * 2], hi = bounds[cursor][axis * 2 + 1];
        if (a.code < 0 || a.max <= a.min) {
            return a.code < 0 ? 0 : lo;
        }
        int64_t value = std::clamp(a.value, a.min, a.max);
        return static_cast<int>(lo + (value - a.min) * (static_cast<int64_t>(hi) - lo) / (static_cast<int64_t>(a.max) - a.min));
    }

    static int Direction(int x, int y) {
        static const int table[3][3] = {{UpLeft, Left, DownLeft}, {Up, NoDirection, Down}, {UpRight, Right, DownRight}};
        return table[std::clamp(x, -1, 1) + 1][std::clamp(y, -1, 1) + 1];
    }

    void SetLed(int led, bool on) {
        input_event events[2] = {};
        events[0].type = EV_LED;
        events[0].code = static_cast<uint16_t>(led);
        events[0].value = on ? 1 : 0;
        events[1].type = EV_SYN;
        events[1].code = SYN_REPORT;
        (void)!write(fd, events, sizeof events);
    }

    void Handle(const input_event& e, std::vector<Out>& out) {
        switch (kind) {
            case Keyboard: HandleKeyboard(e, out); break;
            case Mouse: HandleMouse(e, out); break;
            case Joystick: HandleJoystick(e, out); break;
        }
    }

    void HandleKeyboard(const input_event& e, std::vector<Out>& out) {
        // value 2: the key repeats while it is held (USB keyboards do not do that)
        if (e.type != EV_KEY || e.code >= 256 || e.value == 2) {
            return;
        }
        int key = UsbKey[e.code];
        if (key == 0) {
            return;
        }
        bool down = e.value != 0;
        pressed.set(key, down);
        if (down && key == KeyCapsLock) {
            capsLock = !capsLock;
            SetLed(LED_CAPSL, capsLock);
        }
        if (down && key == KeyNumLock) {
            numLock = !numLock;
            SetLed(LED_NUML, numLock);
        }
        bool shift = pressed[KeyLeftShift] || pressed[KeyRightShift];
        bool ctrl = pressed[KeyLeftCtrl] || pressed[KeyRightCtrl];
        bool alt = pressed[KeyLeftAlt] || pressed[KeyRightAlt];
        out.push_back({KeyChanged, key, down ? 1 : 2, Ascii(key, shift, capsLock, numLock, ctrl, alt)});
    }

    static int MouseButton(int code) {
        switch (code) {
            case BTN_LEFT: return 0x01;
            case BTN_RIGHT: return 0x02;
            case BTN_MIDDLE: return 0x04;
            case BTN_SIDE: case BTN_BACK: return 0x08;
            case BTN_EXTRA: case BTN_FORWARD: return 0x10;
        }
        return 0;
    }

    void HandleMouse(const input_event& e, std::vector<Out>& out) {
        if (e.type == EV_REL) {
            if (e.code == REL_X) movedX += e.value;
            else if (e.code == REL_Y) movedY += e.value;
            else if (e.code == REL_WHEEL) movedWheel += e.value;
        }
        else if (e.type == EV_KEY && e.value != 2) {
            if (int button = MouseButton(e.code)) {
                changedButtons.emplace_back(button, e.value != 0 ? 1 : 0);
            }
        }
        else if (e.type == EV_SYN && e.code == SYN_REPORT) {
            // As TinyCLR: the wheel, the cursor, then the buttons
            if (movedWheel != 0) {
                restWheel += movedWheel * scale;
                int step = static_cast<int>(restWheel);
                restWheel -= step;
                if (step != 0) {
                    wheel += step;
                    out.push_back({MouseWheelMoved, wheel, step});
                }
            }
            if (movedX != 0 || movedY != 0) {
                restX += movedX * scale;
                restY += movedY * scale;
                int stepX = static_cast<int>(restX), stepY = static_cast<int>(restY);
                restX -= stepX;
                restY -= stepY;
                int newX = std::clamp(static_cast<int>(std::clamp<int64_t>(static_cast<int64_t>(x) + stepX, INT_MIN, INT_MAX)), minX, maxX);
                int newY = std::clamp(static_cast<int>(std::clamp<int64_t>(static_cast<int64_t>(y) + stepY, INT_MIN, INT_MAX)), minY, maxY);
                if (newX != x || newY != y) {
                    out.push_back({MouseCursorMoved, newX, newY, newX - x, newY - y});
                    x = newX;
                    y = newY;
                }
            }
            for (auto [button, state] : changedButtons) {
                if (((buttons & button) != 0) != (state != 0)) {
                    buttons = state ? buttons | button : buttons & ~button;
                    out.push_back({MouseButtonChanged, button, state});
                }
            }
            movedX = movedY = movedWheel = 0;
            changedButtons.clear();
        }
    }

    void HandleJoystick(const input_event& e, std::vector<Out>& out) {
        if (e.type == EV_ABS) {
            for (auto& cursor : axes) {
                for (auto& axis : cursor) {
                    if (axis.code == e.code) {
                        axis.value = e.value;
                    }
                }
            }
            if (e.code == ABS_HAT0X) hatX = e.value;
            else if (e.code == ABS_HAT0Y) hatY = e.value;
        }
        else if (e.type == EV_KEY && e.value != 2) {
            if (e.code >= BTN_DPAD_UP && e.code <= BTN_DPAD_RIGHT) {
                int bit = 1 << (e.code - BTN_DPAD_UP);
                dpad = e.value ? dpad | bit : dpad & ~bit;
                return;
            }
            auto it = std::find(buttonCodes.begin(), buttonCodes.end(), e.code);
            if (it != buttonCodes.end()) {
                uint32_t bit = 1u << (it - buttonCodes.begin());
                joystickButtons = e.value ? joystickButtons | bit : joystickButtons & ~bit;
            }
        }
        else if (e.type == EV_SYN && e.code == SYN_REPORT) {
            for (int cursor = 0; cursor < 2; cursor++) {
                int newX = Scale(cursor, 0), newY = Scale(cursor, 1);
                int oldX = positions[cursor][0], oldY = positions[cursor][1];
                if (newX != oldX || newY != oldY) {
                    positions[cursor][0] = newX;
                    positions[cursor][1] = newY;
                    out.push_back({JoystickCursorMoved, cursor, newX, newY, newX - oldX, newY - oldY});
                }
            }
            int across = hatX, upDown = hatY;
            if (hasDpad && !hasHat) {
                across = ((dpad & 8) ? 1 : 0) - ((dpad & 4) ? 1 : 0);
                upDown = ((dpad & 2) ? 1 : 0) - ((dpad & 1) ? 1 : 0);
            }
            int direction = Direction(across, upDown);
            if (direction != hat) {
                hat = direction;
                out.push_back({JoystickHatSwitch, direction});
            }
            uint32_t changed = joystickButtons ^ reportedButtons;
            for (int i = 0; i < 32; i++) {
                if (changed & (1u << i)) {
                    out.push_back({JoystickButtonChanged, i, (joystickButtons >> i) & 1 ? 1 : 0});
                }
            }
            reportedButtons = joystickButtons;
        }
    }
};

namespace {

void Run(std::shared_ptr<State> s) {
    input_event buffer[64];
    while (!s->stop) {
        pollfd fds[] = {{s->fd, POLLIN, 0}, {s->wake, POLLIN, 0}};
        if (poll(fds, 2, -1) < 0) {
            if (errno == EINTR) {
                continue;
            }
            break;
        }
        if (s->stop) {
            break;
        }
        // Pulled out: the kernel first releases the keys still held, then
        // ends the device (ENODEV): read what is left until then
        bool gone = (fds[0].revents & POLLNVAL) != 0;
        std::vector<Out> out;
        if (!gone && (fds[0].revents & (POLLIN | POLLERR | POLLHUP))) {
            ssize_t length = read(s->fd, buffer, sizeof buffer);
            if ((length < 0 && errno != EAGAIN && errno != EINTR) || length == 0) {
                gone = true;
            }
            else if (length > 0) {
                std::lock_guard lock(s->mutex);
                for (size_t i = 0; i < static_cast<size_t>(length) / sizeof(input_event); i++) {
                    s->Handle(buffer[i], out);
                }
            }
        }
        if (gone) {
            std::lock_guard lock(s->mutex);
            s->connected = false;
            out.push_back({Disconnected});
        }
        for (const auto& o : out) {
            if (s->stop) {
                break;
            }
            try {
                s->callback(o.event, o.a, o.b, o.c, o.d, o.e);
            }
            catch (...) {
                // a callback's error is not the reader's
            }
        }
        if (gone) {
            break;
        }
    }
}

const char* KindName(int kind) {
    switch (kind) {
        case Mouse: return "a mouse";
        case Keyboard: return "a keyboard";
        default: return "a joystick";
    }
}

}  // namespace

Device::Device(const std::string& deviceName, int kind, Callback callback) : state(std::make_shared<State>()) {
    if (kind != Mouse && kind != Keyboard && kind != Joystick) {
        ThrowArgument("The device type must be Mouse, Keyboard or Joystick");
    }
    State& s = *state;
    s.kind = kind;
    s.name = deviceName;
    s.callback = std::move(callback);
    // A keyboard is also written to: its Caps Lock and Num Lock lights
    s.fd = open(deviceName.c_str(), (kind == Keyboard ? O_RDWR : O_RDONLY) | O_NONBLOCK | O_CLOEXEC);
    if (s.fd < 0 && kind == Keyboard) {
        s.fd = open(deviceName.c_str(), O_RDONLY | O_NONBLOCK | O_CLOEXEC);
    }
    if (s.fd < 0) {
        if (errno == ENOENT || errno == ENODEV) {
            ThrowArgument("There is no device " + deviceName + " (it was unplugged, or the name is not a DeviceName from UsbHostController)");
        }
        ThrowSystemError("Cannot open " + deviceName);
    }

    Bits types = Get(s.fd, EVIOCGBIT(0, Bits::Size));
    Bits keys = Get(s.fd, EVIOCGBIT(EV_KEY, Bits::Size));
    Bits rel = Get(s.fd, EVIOCGBIT(EV_REL, Bits::Size));
    Bits abs = Get(s.fd, EVIOCGBIT(EV_ABS, Bits::Size));
    Bits down = Get(s.fd, EVIOCGKEY(Bits::Size));

    bool fits = false;
    if (kind == Mouse) {
        fits = types.Has(EV_REL) && rel.Has(REL_X) && rel.Has(REL_Y);
        for (int code = BTN_LEFT; code <= BTN_TASK; code++) {
            if (down.Has(code)) {
                s.buttons |= State::MouseButton(code);
            }
        }
    }
    else if (kind == Keyboard) {
        fits = types.Has(EV_KEY) && keys.Has(KEY_A);
        for (int code = 0; code < 256; code++) {
            if (down.Has(code) && UsbKey[code] != 0) {
                s.pressed.set(UsbKey[code]);
            }
        }
        Bits leds = Get(s.fd, EVIOCGLED(Bits::Size));
        s.capsLock = leds.Has(LED_CAPSL);
        s.numLock = leds.Has(LED_NUML);
    }
    else {
        // The first stick is X, Y; the second RX, RY (game pads) or else Z, RZ
        auto useAxis = [&](int cursor, int axis, int code) {
            if (!abs.Has(code)) {
                return;
            }
            input_absinfo info{};
            if (ioctl(s.fd, EVIOCGABS(code), &info) == 0) {
                s.axes[cursor][axis] = {code, info.minimum, info.maximum, info.value};
            }
        };
        useAxis(0, 0, ABS_X);
        useAxis(0, 1, ABS_Y);
        if (abs.Has(ABS_RX) || abs.Has(ABS_RY)) {
            useAxis(1, 0, ABS_RX);
            useAxis(1, 1, ABS_RY);
        }
        else {
            useAxis(1, 0, ABS_Z);
            useAxis(1, 1, ABS_RZ);
        }
        s.hasHat = abs.Has(ABS_HAT0X) && abs.Has(ABS_HAT0Y);
        if (s.hasHat) {
            input_absinfo info{};
            if (ioctl(s.fd, EVIOCGABS(ABS_HAT0X), &info) == 0) s.hatX = info.value;
            if (ioctl(s.fd, EVIOCGABS(ABS_HAT0Y), &info) == 0) s.hatY = info.value;
        }
        s.hasDpad = keys.Has(BTN_DPAD_UP) && keys.Has(BTN_DPAD_DOWN);
        // The buttons are numbered as the kernel's joystick driver does:
        // joystick and game pad buttons first, then the others
        for (int code = BTN_JOYSTICK; code <= KEY_MAX && s.buttonCodes.size() < 32; code++) {
            if (keys.Has(code) && !(s.hasDpad && code >= BTN_DPAD_UP && code <= BTN_DPAD_RIGHT)) {
                s.buttonCodes.push_back(code);
            }
        }
        for (int code = BTN_MISC; code < BTN_JOYSTICK && s.buttonCodes.size() < 32; code++) {
            if (keys.Has(code)) {
                s.buttonCodes.push_back(code);
            }
        }
        for (size_t i = 0; i < s.buttonCodes.size(); i++) {
            if (down.Has(s.buttonCodes[i])) {
                s.joystickButtons |= 1u << i;
            }
        }
        s.reportedButtons = s.joystickButtons;
        for (int cursor = 0; cursor < 2; cursor++) {
            s.positions[cursor][0] = s.Scale(cursor, 0);
            s.positions[cursor][1] = s.Scale(cursor, 1);
        }
        s.hat = State::Direction(s.hatX, s.hatY);
        s.capabilities = (s.axes[0][0].code >= 0 ? CursorX : 0) | (s.axes[0][1].code >= 0 ? CursorY : 0) |
                         (s.axes[1][0].code >= 0 ? Cursor2X : 0) | (s.axes[1][1].code >= 0 ? Cursor2Y : 0) |
                         (s.hasHat || s.hasDpad ? HatSwitch : 0) | (!s.buttonCodes.empty() ? Buttons : 0);
        fits = (types.Has(EV_ABS) && s.axes[0][0].code >= 0) || !s.buttonCodes.empty();
    }
    if (!fits) {
        ThrowArgument(deviceName + " is not " + KindName(kind));
    }

    input_id id{};
    if (ioctl(s.fd, EVIOCGID, &id) == 0) {
        s.vendorId = id.vendor;
        s.productId = id.product;
    }
    s.wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
    if (s.wake < 0) {
        ThrowSystemError("Cannot read " + deviceName);
    }
    s.thread = std::thread(Run, state);
}

Device::~Device() {
    state->stop = true;
    uint64_t one = 1;
    (void)!write(state->wake, &one, sizeof one);
    if (state->thread.get_id() == std::this_thread::get_id()) {
        state->thread.detach();  // disposed by its own event: the thread ends after it
    }
    else if (state->thread.joinable()) {
        state->thread.join();
    }
}

int Device::VendorId() const { return state->vendorId; }

int Device::ProductId() const { return state->productId; }

bool Device::Connected() const {
    std::lock_guard lock(state->mutex);
    return state->connected;
}

int Device::Capabilities() const { return state->capabilities; }

int Device::KeyState(int key) const {
    if (key < 0 || key > 255) {
        ThrowArgumentOutOfRange("The key must be a USB key code (0 to 255)");
    }
    std::lock_guard lock(state->mutex);
    return state->pressed[key] ? 1 : 2;
}

void Device::GetMouse(int& x, int& y, int& wheel, int& buttons) const {
    std::lock_guard lock(state->mutex);
    x = state->x;
    y = state->y;
    wheel = state->wheel;
    buttons = state->buttons;
}

void Device::GetJoystick(int cursor, int& x, int& y, int& hat, uint32_t& buttons) const {
    if (cursor < 0 || cursor > 1) {
        ThrowArgumentOutOfRange("The cursor must be Cursor1 or Cursor2");
    }
    std::lock_guard lock(state->mutex);
    x = state->positions[cursor][0];
    y = state->positions[cursor][1];
    hat = state->hat;
    buttons = state->reportedButtons;
}

void Device::SetCursorBounds(int cursor, int minX, int maxX, int minY, int maxY) {
    if (minX > maxX || minY > maxY) {
        ThrowArgument("The minimum must not be more than the maximum");
    }
    std::lock_guard lock(state->mutex);
    State& s = *state;
    if (s.kind == Mouse) {
        s.minX = minX;
        s.maxX = maxX;
        s.minY = minY;
        s.maxY = maxY;
        s.x = std::clamp(s.x, minX, maxX);
        s.y = std::clamp(s.y, minY, maxY);
    }
    else if (s.kind == Joystick) {
        if (cursor < 0 || cursor > 1) {
            ThrowArgumentOutOfRange("The cursor must be Cursor1 or Cursor2");
        }
        int values[4] = {minX, maxX, minY, maxY};
        std::copy(values, values + 4, s.bounds[cursor]);
        s.positions[cursor][0] = s.Scale(cursor, 0);
        s.positions[cursor][1] = s.Scale(cursor, 1);
    }
}

void Device::SetCursorPosition(int x, int y) {
    std::lock_guard lock(state->mutex);
    state->x = std::clamp(x, state->minX, state->maxX);
    state->y = std::clamp(y, state->minY, state->maxY);
}

void Device::SetScale(double scale) {
    std::lock_guard lock(state->mutex);
    state->scale = scale;
}

}  // namespace GHIElectronics::Endpoint::Internal::Input
