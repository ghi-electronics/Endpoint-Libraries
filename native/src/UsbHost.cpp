#include "UsbHost.h"

#include <dirent.h>
#include <fnmatch.h>
#include <linux/netlink.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/utsname.h>
#include <unistd.h>

#include <algorithm>
#include <chrono>
#include <climits>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <map>
#include <mutex>
#include <optional>
#include <set>
#include <sstream>
#include <string_view>
#include <thread>
#include <vector>

#include "Errors.h"
#include "SystemCommands.h"

namespace GHIElectronics::Endpoint::Internal::UsbHost {

namespace {

using Clock = std::chrono::steady_clock;

// A new device is looked at a moment after the kernel reports it, when its
// interfaces are there and the built-in drivers have taken them
constexpr auto Settle = std::chrono::milliseconds(300);
// Something changed below a device (a driver took it, a port appeared)
constexpr auto Recheck = std::chrono::milliseconds(100);
// Some drivers load the device's firmware before they make its port: a
// device with a driver and nothing to show yet is Unknown only after this
constexpr auto DriverGrace = std::chrono::seconds(3);
// The longest wait for a driver that exists to take its device
constexpr auto LoadTimeout = std::chrono::seconds(10);

std::string ReadLine(const std::string& path) {
    std::ifstream file(path);
    std::string line;
    std::getline(file, line);
    return line;
}

bool Exists(const std::string& path) { return access(path.c_str(), F_OK) == 0; }

// The last part of where a link points: ".../drivers/r8152" gives "r8152"
std::string LinkName(const std::string& path) {
    char target[PATH_MAX];
    ssize_t length = readlink(path.c_str(), target, sizeof target - 1);
    if (length <= 0) {
        return {};
    }
    target[length] = '\0';
    const char* slash = std::strrchr(target, '/');
    return slash != nullptr ? slash + 1 : target;
}

// The folders in a folder (not links), sorted
std::vector<std::string> Folders(const std::string& folder) {
    std::vector<std::string> names;
    if (DIR* dir = opendir(folder.c_str())) {
        while (auto* entry = readdir(dir)) {
            if (entry->d_name[0] != '.' && entry->d_type == DT_DIR) {
                names.emplace_back(entry->d_name);
            }
        }
        closedir(dir);
    }
    std::sort(names.begin(), names.end());
    return names;
}

// "KEY=value" in a uevent file
std::string UeventValue(const std::string& folder, const std::string& key) {
    std::ifstream file(folder + "/uevent");
    std::string line;
    while (std::getline(file, line)) {
        if (line.size() > key.size() && line.compare(0, key.size(), key) == 0 && line[key.size()] == '=') {
            return line.substr(key.size() + 1);
        }
    }
    return {};
}

std::string DeviceNode(const std::string& folder) {
    auto name = UeventValue(folder, "DEVNAME");
    return name.empty() ? std::string() : "/dev/" + name;
}

int Hex(const std::string& text) { return static_cast<int>(std::strtol(text.c_str(), nullptr, 16)); }

// An input device's capabilities: hex words of the kernel's "long", the
// highest first ("120013", or "10000 0 0 e0b0ffdf ... fffffffe")
class Bits {
public:
    explicit Bits(const std::string& path) {
        std::istringstream words(ReadLine(path));
        std::string word;
        std::vector<unsigned long long> highFirst;
        while (words >> word) {
            highFirst.push_back(std::strtoull(word.c_str(), nullptr, 16));
        }
        values.assign(highFirst.rbegin(), highFirst.rend());
    }

    bool Has(int bit) const {
        size_t word = bit / WordBits();
        return word < values.size() && (values[word] >> (bit % WordBits())) & 1;
    }

private:
    // The kernel's long: 64 bits on a 64-bit kernel, also under a 32-bit program
    static int WordBits() {
        static const int bits = [] {
            utsname name{};
            uname(&name);
            std::string_view machine = name.machine;
            return machine == "aarch64" || machine == "x86_64" ? 64 : 32;
        }();
        return bits;
    }

    std::vector<unsigned long long> values;
};

// What an input device is, as udev's input_id decides it. Others (a
// keyboard's media keys, a webcam's button, a gamepad's motion sensor): nothing.
std::optional<Type> InputType(const std::string& inputFolder) {
    constexpr int EV_KEY = 0x01, EV_REL = 0x02, EV_ABS = 0x03;
    constexpr int REL_X = 0x00, REL_Y = 0x01, ABS_X = 0x00, ABS_Y = 0x01;
    // Joystick buttons 0x120-0x12f, then gamepad buttons to 0x13e
    constexpr int BTN_MOUSE = 0x110, BTN_JOYSTICK = 0x120, BTN_GAMEPAD_LAST = 0x13e;
    // BTN_TOUCH identifies a touchscreen (as opposed to a mouse or joystick)
    constexpr int BTN_TOUCH = 0x14a;
    Bits ev(inputFolder + "/capabilities/ev");
    Bits key(inputFolder + "/capabilities/key");
    Bits rel(inputFolder + "/capabilities/rel");
    Bits abs(inputFolder + "/capabilities/abs");

    if (ev.Has(EV_ABS) && abs.Has(ABS_X) && abs.Has(ABS_Y)) {
        for (int button = BTN_JOYSTICK; button <= BTN_GAMEPAD_LAST; button++) {
            if (key.Has(button)) {
                return Joystick;
            }
        }
        // Touchscreens have ABS_X/Y and BTN_TOUCH but no joystick/gamepad buttons.
        // Multi-touch panels also emit ABS_MT_* events through this same device.
        if (key.Has(BTN_TOUCH)) {
            return Touch;
        }
    }
    if (ev.Has(EV_REL) && rel.Has(REL_X) && rel.Has(REL_Y) && key.Has(BTN_MOUSE)) {
        return Mouse;
    }
    // Escape, the numbers and Q to S (keys 1 to 31): a full keyboard
    if (ev.Has(EV_KEY)) {
        bool all = true;
        for (int k = 1; k <= 31 && all; k++) {
            all = key.Has(k);
        }
        if (all) {
            return Keyboard;
        }
    }
    return std::nullopt;
}

// The drivers the firmware has as modules, from modules.alias: whether one
// takes an interface (then it is worth waiting for it)
class ModuleAliases {
public:
    bool Match(const std::string& modalias) {
        if (!loaded) {
            Load();
        }
        for (const auto& pattern : patterns) {
            if (fnmatch(pattern.c_str(), modalias.c_str(), 0) == 0) {
                return true;
            }
        }
        return false;
    }

private:
    void Load() {
        loaded = true;
        utsname name{};
        uname(&name);
        std::ifstream file(std::string("/lib/modules/") + name.release + "/modules.alias");
        std::string line;
        while (std::getline(file, line)) {
            // "alias usb:v0BDAp8153d*dc*dsc*dp*icFFiscFFip00in* r8152"
            if (line.compare(0, 10, "alias usb:") == 0) {
                auto end = line.find(' ', 6);
                patterns.push_back(line.substr(6, end == std::string::npos ? std::string::npos : end - 6));
            }
        }
    }

    bool loaded = false;
    std::vector<std::string> patterns;
};

// One thing a device gives: a port, a network interface, an input device...
struct Function {
    std::string key;  // where it is in /sys
    int type = Unknown;
    std::string name;

    bool operator==(const Function& other) const = default;
};

struct Device {
    uint32_t id = 0;
    std::string devnum;  // another number at the same place: another device
    int vendorId = 0, productId = 0;
    std::string manufacturer, product, serialNumber;
    std::string busNode;  // "/dev/bus/usb/001/005"
    bool hub = false;
    Clock::time_point seen;
    std::vector<Function> functions;  // reported as connected
    std::set<std::string> loaded;     // drivers asked for (modprobe)
};

struct Event {
    const Controller* target = nullptr;  // nullptr: every enabled controller
    DeviceEvent data;
};

// What is below a device in /sys now
struct Found {
    std::vector<Function> functions;
    std::vector<std::string> toLoad;  // interfaces without a driver whose driver exists
    bool waiting = false;             // such an interface (its driver is loading)
    bool bound = false;               // some interface has a driver
};

// One for the program: the thread that listens to the kernel, the devices,
// the enabled controllers
class Watcher {
public:
    // Never destroyed: its thread runs until the program ends
    static Watcher& Instance() {
        static Watcher* instance = new Watcher();
        return *instance;
    }

    void Enable(const Controller* controller) {
        std::lock_guard lock(gate);
        if (!started) {
            Start();
        }
        if (!enabled.insert(controller).second) {
            return;
        }
        // The devices already there (the ones not looked at yet come as they are)
        for (const auto& [path, device] : devices) {
            for (const auto& function : device.functions) {
                events.push_back({controller, Make(device, function, Connected)});
            }
        }
        Wake();
    }

    // Afterwards the controller's callback is not called any more (an event
    // being delivered now finishes first)
    void Disable(const Controller* controller) {
        {
            std::lock_guard lock(gate);
            enabled.erase(controller);
            std::erase_if(events, [&](const Event& e) { return e.target == controller; });
        }
        std::lock_guard wait(delivering);
    }

private:
    Watcher() = default;

    void Start() {
        sock = socket(AF_NETLINK, SOCK_DGRAM | SOCK_CLOEXEC, NETLINK_KOBJECT_UEVENT);
        if (sock < 0) {
            ThrowSystemError("Cannot watch for USB devices");
        }
        int size = 256 * 1024;
        setsockopt(sock, SOL_SOCKET, SO_RCVBUF, &size, sizeof size);
        sockaddr_nl address{};
        address.nl_family = AF_NETLINK;
        address.nl_groups = 1;  // the kernel's device events
        if (bind(sock, reinterpret_cast<sockaddr*>(&address), sizeof address) != 0) {
            int error = errno;
            close(sock);
            sock = -1;
            errno = error;
            ThrowSystemError("Cannot watch for USB devices");
        }
        wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);

        // The devices already there ("1-1", "1-1.4"; not the board's own
        // controllers "usb1" or interfaces "1-1:1.0"): looked at on the thread
        for (const auto& name : Links("/sys/bus/usb/devices")) {
            if (name.find('-') != std::string::npos && name.find(':') == std::string::npos) {
                char real[PATH_MAX];
                if (realpath(("/sys/bus/usb/devices/" + name).c_str(), real) != nullptr) {
                    Add(real, Clock::now());
                }
            }
        }
        std::thread(&Watcher::Run, this).detach();
        started = true;
    }

    // /sys/bus/usb/devices has links, sorted (a hub before what is behind it)
    static std::vector<std::string> Links(const std::string& folder) {
        std::vector<std::string> names;
        if (DIR* dir = opendir(folder.c_str())) {
            while (auto* entry = readdir(dir)) {
                if (entry->d_name[0] != '.') {
                    names.emplace_back(entry->d_name);
                }
            }
            closedir(dir);
        }
        std::sort(names.begin(), names.end());
        return names;
    }

    void Run() {
        while (true) {
            std::vector<std::pair<std::string, std::string>> toLoad;  // device, driver alias
            int timeout = -1;
            {
                std::lock_guard lock(gate);
                auto now = Clock::now();
                for (auto it = due.begin(); it != due.end();) {
                    if (it->second <= now) {
                        auto path = it->first;
                        it = due.erase(it);
                        for (const auto& alias : Scan(path, now)) {
                            toLoad.emplace_back(path, alias);
                        }
                    }
                    else {
                        ++it;
                    }
                }
                for (const auto& [path, time] : due) {
                    int ms = static_cast<int>(std::chrono::duration_cast<std::chrono::milliseconds>(time - now).count()) + 1;
                    timeout = timeout < 0 ? ms : std::min(timeout, ms);
                }
            }

            // Drivers for devices that have none yet, as udev would load them.
            // Not with the gate held: this takes a moment.
            if (!toLoad.empty()) {
                for (const auto& [path, alias] : toLoad) {
                    try {
                        SystemCommands::Run("modprobe", "Cannot load a USB driver", {"-q", alias}, 15000);
                    }
                    catch (...) {
                        // the device stays without its driver: Unknown
                    }
                }
                std::lock_guard lock(gate);
                for (const auto& [path, alias] : toLoad) {
                    due[path] = Clock::now();
                }
                continue;
            }

            Deliver();

            pollfd fds[] = {{sock, POLLIN, 0}, {wake, POLLIN, 0}};
            if (poll(fds, 2, timeout) < 0) {
                continue;
            }
            if (fds[1].revents & POLLIN) {
                uint64_t count;
                (void)!read(wake, &count, sizeof count);
            }
            if (fds[0].revents & POLLIN) {
                Receive();
            }
        }
    }

    // The kernel's messages: "add@/devices/...\0ACTION=add\0DEVPATH=/devices/...\0SUBSYSTEM=usb\0DEVTYPE=usb_device\0..."
    void Receive() {
        char buffer[8192];
        while (true) {
            ssize_t length = recv(sock, buffer, sizeof buffer - 1, MSG_DONTWAIT);
            if (length <= 0) {
                return;
            }
            buffer[length] = '\0';
            std::string_view action, devpath, subsystem, devtype;
            for (char* field = buffer; field < buffer + length; field += std::strlen(field) + 1) {
                std::string_view text(field);
                if (text.starts_with("ACTION=")) {
                    action = text.substr(7);
                }
                else if (text.starts_with("DEVPATH=")) {
                    devpath = text.substr(8);
                }
                else if (text.starts_with("SUBSYSTEM=")) {
                    subsystem = text.substr(10);
                }
                else if (text.starts_with("DEVTYPE=")) {
                    devtype = text.substr(8);
                }
            }
            if (devpath.empty()) {
                continue;
            }
            std::string path = "/sys" + std::string(devpath);
            std::lock_guard lock(gate);
            if (subsystem == "usb" && devtype == "usb_device") {
                if (action == "add") {
                    Add(path, Clock::now() + Settle);
                }
                else if (action == "remove") {
                    Remove(path);
                }
                continue;
            }
            // Something below a device: an interface, its driver, a port...
            if (auto owner = Owner(path)) {
                auto when = Clock::now() + Recheck;
                auto [it, added] = due.emplace(*owner, when);
                if (!added && it->second > when) {
                    it->second = when;
                }
            }
        }
    }

    // The device that path is in (the deepest: a hub's devices are in the hub's folder)
    std::optional<std::string> Owner(const std::string& path) {
        std::optional<std::string> owner;
        for (const auto& [device, data] : devices) {
            if (path.size() > device.size() && path.compare(0, device.size(), device) == 0 && path[device.size()] == '/' &&
                (!owner || device.size() > owner->size())) {
                owner = device;
            }
        }
        return owner;
    }

    // A device plugged in: its handle, and when to look at it
    void Add(const std::string& path, Clock::time_point when) {
        auto devnum = ReadLine(path + "/devnum");
        auto it = devices.find(path);
        if (it != devices.end()) {
            if (it->second.devnum == devnum) {
                return;
            }
            Remove(path);  // its removal was missed
        }
        auto busnum = ReadLine(path + "/busnum");
        if (devnum.empty() || busnum.empty()) {
            return;  // gone already
        }
        Device device;
        device.id = nextId++;
        device.devnum = devnum;
        device.vendorId = Hex(ReadLine(path + "/idVendor"));
        device.productId = Hex(ReadLine(path + "/idProduct"));
        device.manufacturer = ReadLine(path + "/manufacturer");
        device.product = ReadLine(path + "/product");
        device.serialNumber = ReadLine(path + "/serial");
        char node[64];
        std::snprintf(node, sizeof node, "/dev/bus/usb/%03d/%03d", std::atoi(busnum.c_str()), std::atoi(devnum.c_str()));
        device.busNode = node;
        device.hub = ReadLine(path + "/bDeviceClass") == "09";
        device.seen = Clock::now();
        devices.emplace(path, std::move(device));
        due[path] = when;
    }

    // A device pulled out: all it gave is disconnected, the last first
    void Remove(const std::string& path) {
        due.erase(path);
        auto it = devices.find(path);
        if (it == devices.end()) {
            return;
        }
        for (auto f = it->second.functions.rbegin(); f != it->second.functions.rend(); ++f) {
            events.push_back({nullptr, Make(it->second, *f, Disconnected)});
        }
        devices.erase(it);
    }

    // Looks at a device now: what it gives that is new is connected, what it
    // gave that is gone is disconnected. Gives the drivers to load for it.
    std::vector<std::string> Scan(const std::string& path, Clock::time_point now) {
        auto it = devices.find(path);
        if (it == devices.end()) {
            return {};
        }
        Device& device = it->second;
        if (ReadLine(path + "/devnum") != device.devnum) {
            // Pulled out (or another device came at its place): the kernel's message comes too
            Remove(path);
            if (Exists(path + "/devnum")) {
                Add(path, now + Settle);
            }
            return {};
        }

        std::vector<Function> want;
        std::vector<std::string> load;
        if (device.hub) {
            want.push_back({path, Hub, device.busNode});
        }
        else {
            Found found = Look(path);
            for (const auto& alias : found.toLoad) {
                if (device.loaded.insert(alias).second) {
                    load.push_back(alias);
                }
            }
            want = std::move(found.functions);
            if (want.empty() && load.empty()) {
                // Nothing to show: Unknown, unless a driver may still make something
                auto age = now - device.seen;
                if (found.waiting && age < LoadTimeout) {
                    Due(path, device.seen + LoadTimeout);
                }
                else if (found.bound && age < DriverGrace) {
                    Due(path, device.seen + DriverGrace);
                }
                else {
                    want.push_back({path, Unknown, device.busNode});
                }
            }
        }

        for (auto f = device.functions.rbegin(); f != device.functions.rend(); ++f) {
            if (std::find(want.begin(), want.end(), *f) == want.end()) {
                events.push_back({nullptr, Make(device, *f, Disconnected)});
            }
        }
        for (const auto& f : want) {
            if (std::find(device.functions.begin(), device.functions.end(), f) == device.functions.end()) {
                events.push_back({nullptr, Make(device, f, Connected)});
            }
        }
        device.functions = std::move(want);
        return load;
    }

    void Due(const std::string& path, Clock::time_point when) {
        auto [it, added] = due.emplace(path, when);
        if (!added && it->second > when) {
            it->second = when;
        }
    }

    // What a device gives now: its interfaces ("1-1.2:1.0") and what their drivers made
    Found Look(const std::string& path) {
        Found found;
        HidInputs hid;
        auto base = path.substr(path.rfind('/') + 1);
        for (const auto& name : Folders(path)) {
            if (name.compare(0, base.size() + 1, base + ":") != 0) {
                continue;
            }
            std::string interface = path + "/" + name;
            auto driver = LinkName(interface + "/driver");
            if (driver.empty()) {
                auto alias = ReadLine(interface + "/modalias");
                if (!alias.empty() && aliases.Match(alias)) {
                    found.toLoad.push_back(alias);
                    found.waiting = true;
                }
                continue;
            }
            found.bound = true;
            if (driver == "usb-storage" || driver == "uas") {
                found.functions.push_back({interface, MassStorage, ""});
                continue;
            }
            Walk(interface, 0, "", found.functions, hid);
        }
        // A HID device that is no keyboard, mouse or joystick (no input
        // device at all): the program talks to it through hidraw
        for (const auto& [hidDevice, raw] : hid.raw) {
            if (!hid.withInput.count(hidDevice)) {
                found.functions.push_back(raw);
            }
        }
        return found;
    }

    // HID devices ("0003:046D:C52B.0001") with input devices, and their hidraw ports
    struct HidInputs {
        std::set<std::string> withInput;
        std::vector<std::pair<std::string, Function>> raw;
    };

    void Walk(const std::string& folder, int depth, const std::string& hidDevice, std::vector<Function>& functions,
              HidInputs& hid) {
        if (depth > 8) {
            return;
        }
        for (const auto& name : Folders(folder)) {
            std::string path = folder + "/" + name;
            if (Exists(path + "/idVendor")) {
                continue;  // another USB device (behind a hub)
            }
            auto subsystem = LinkName(path + "/subsystem");
            std::string hidHere = hidDevice;
            if (subsystem == "hid") {
                hidHere = path;
            }
            else if (subsystem == "net") {
                functions.push_back({path, Network, name});
                continue;
            }
            else if (subsystem == "tty") {
                functions.push_back({path, Serial, DeviceNode(path)});
                continue;
            }
            else if (subsystem == "input") {
                if (name.compare(0, 5, "input") == 0) {
                    if (!hidDevice.empty()) {
                        hid.withInput.insert(hidDevice);
                    }
                }
                else if (name.compare(0, 5, "event") == 0) {
                    if (auto type = InputType(folder)) {
                        functions.push_back({path, *type, DeviceNode(path)});
                    }
                    continue;
                }
                else {
                    continue;  // mouse0, js0: the event port is the one
                }
            }
            else if (subsystem == "hidraw") {
                if (!hidDevice.empty()) {
                    hid.raw.push_back({hidDevice, {path, Hid, DeviceNode(path)}});
                }
                continue;
            }
            else if (subsystem == "video4linux") {
                // A webcam has two: the pictures (index 0) and their metadata
                if (ReadLine(path + "/index") == "0") {
                    functions.push_back({path, Webcam, DeviceNode(path)});
                }
                continue;
            }
            else if (subsystem == "sound") {
                if (name.compare(0, 4, "card") == 0) {
                    functions.push_back({path, Audio, "hw:" + name.substr(4)});
                }
                continue;
            }
            else if (subsystem == "usbmisc") {
                if (name.compare(0, 2, "lp") == 0) {
                    functions.push_back({path, Printer, DeviceNode(path)});
                }
                continue;
            }
            else if (subsystem == "bluetooth") {
                if (name.compare(0, 3, "hci") == 0) {
                    functions.push_back({path, Bluetooth, name});
                }
                continue;
            }
            else if (name == "power" || name == "driver" || name == "subsystem") {
                continue;
            }
            Walk(path, depth + 1, hidHere, functions, hid);
        }
    }

    DeviceEvent Make(const Device& device, const Function& function, int status) const {
        DeviceEvent event;
        event.status = status;
        event.id = device.id;
        event.type = function.type;
        event.vendorId = device.vendorId;
        event.productId = device.productId;
        event.deviceName = function.name;
        event.manufacturer = device.manufacturer;
        event.product = device.product;
        event.serialNumber = device.serialNumber;
        return event;
    }

    // Calls the callbacks, on this thread, without the gate held (a callback may Disable)
    void Deliver() {
        std::lock_guard calling(delivering);
        while (true) {
            Event event;
            std::vector<const Controller*> targets;
            {
                std::lock_guard lock(gate);
                if (events.empty()) {
                    return;
                }
                event = std::move(events.front());
                events.erase(events.begin());
                for (const auto* controller : enabled) {
                    if (event.target == nullptr || event.target == controller) {
                        targets.push_back(controller);
                    }
                }
            }
            for (const auto* controller : targets) {
                {
                    std::lock_guard lock(gate);
                    if (!enabled.count(controller)) {
                        continue;  // disabled by an earlier callback
                    }
                }
                try {
                    controller->Notify(event.data);
                }
                catch (...) {
                    // a callback's error is not the watcher's
                }
            }
        }
    }

    void Wake() {
        uint64_t one = 1;
        (void)!write(wake, &one, sizeof one);
    }

    std::mutex gate;
    std::recursive_mutex delivering;  // held while callbacks run: Disable waits for them
    std::set<const Controller*> enabled;
    std::map<std::string, Device> devices;  // by their folder in /sys
    std::map<std::string, Clock::time_point> due;  // devices to look at, and when
    std::vector<Event> events;
    ModuleAliases aliases;
    uint32_t nextId = 1;
    int sock = -1;
    int wake = -1;
    bool started = false;
};

}  // namespace

Controller::Controller(Callback callback) : callback(std::move(callback)) {}

Controller::~Controller() { Disable(); }

void Controller::Enable() { Watcher::Instance().Enable(this); }

void Controller::Disable() { Watcher::Instance().Disable(this); }

}  // namespace GHIElectronics::Endpoint::Internal::UsbHost
