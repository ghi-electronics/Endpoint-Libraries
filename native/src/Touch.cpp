#include "Touch.h"

#include <dirent.h>
#include <fcntl.h>
#include <linux/input.h>
#include <linux/limits.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <array>
#include <cstring>
#include <fstream>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Touch {

namespace {

// Reads one hex-word-per-column bitmap from /sys (same shape as the one in
// UsbHost.cpp). Highest word is first in the file.
class Bits {
public:
    explicit Bits(const std::string& path) {
        std::ifstream file(path);
        std::string line;
        std::getline(file, line);
        std::istringstream words(line);
        std::string word;
        std::vector<unsigned long long> highFirst;
        while (words >> word) {
            highFirst.push_back(std::strtoull(word.c_str(), nullptr, 16));
        }
        values_.assign(highFirst.rbegin(), highFirst.rend());
    }

    bool Has(int bit) const {
        size_t word = bit / 64;
        return word < values_.size() && (values_[word] >> (bit % 64)) & 1;
    }

private:
    std::vector<unsigned long long> values_;
};

// Is /dev/input/eventN a touchscreen (ABS_X/Y + BTN_TOUCH, no joystick buttons)?
bool IsTouchDevice(const std::string& eventName) {
    std::string sys = "/sys/class/input/" + eventName + "/device";
    Bits ev(sys + "/capabilities/ev");
    Bits key(sys + "/capabilities/key");
    Bits abs(sys + "/capabilities/abs");
    if (!(ev.Has(EV_ABS) && abs.Has(ABS_X) && abs.Has(ABS_Y) && key.Has(BTN_TOUCH))) {
        return false;
    }
    // Joystick buttons 0x120-0x12f, gamepad buttons up to 0x13e
    for (int b = BTN_JOYSTICK; b <= 0x13e; ++b) {
        if (key.Has(b)) {
            return false;  // joystick/gamepad, not a touchscreen
        }
    }
    return true;
}

// True if the event device's parent device chain is under /sys/bus/usb (USB input).
bool IsUsbInput(const std::string& eventName) {
    std::string link = "/sys/class/input/" + eventName + "/device";
    char buf[PATH_MAX];
    ssize_t n = readlink(link.c_str(), buf, sizeof buf - 1);
    if (n <= 0) {
        return false;
    }
    buf[n] = '\0';
    return std::strstr(buf, "/usb") != nullptr;
}

// Reads the device's reported ABS axis range (code = ABS_X or ABS_Y). The input
// device's "name" file plus EVIOCGABS gives the resolution the kernel reports.
int ReadAbsMax(int fd, int code) {
    input_absinfo info{};
    if (ioctl(fd, EVIOCGABS(code), &info) < 0) {
        return 0;
    }
    return info.maximum;
}

}  // namespace

std::string FindDefaultDevice() {
    DIR* dir = opendir("/sys/class/input");
    if (dir == nullptr) {
        return "";
    }
    std::string result;
    while (dirent* entry = readdir(dir)) {
        std::string name = entry->d_name;
        if (name.rfind("event", 0) != 0) {
            continue;
        }
        if (IsUsbInput(name)) {
            continue;  // USB touchscreens come through UsbHostController, not GetDefault
        }
        if (IsTouchDevice(name)) {
            result = "/dev/input/" + name;
            break;
        }
    }
    closedir(dir);
    return result;
}

TouchImpl::TouchImpl(const std::string& deviceName, Callback callback, void* context)
    : deviceName_(deviceName), callback_(callback), context_(context) {
    fd_ = open(deviceName.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd_ < 0) {
        throw EndpointError(ErrorKind::IO, "Cannot open " + deviceName);
    }

    // Reject obviously-wrong devices: must be a touchscreen (ABS_X/Y + BTN_TOUCH).
    std::string eventName = deviceName.substr(deviceName.find_last_of('/') + 1);
    if (!IsTouchDevice(eventName)) {
        close(fd_);
        fd_ = -1;
        throw EndpointError(ErrorKind::Argument, deviceName + " is not a touchscreen");
    }

    // Vendor/product from the input device id
    input_id id{};
    if (ioctl(fd_, EVIOCGID, &id) == 0) {
        vendorId_ = id.vendor;
        productId_ = id.product;
    }
    // Reported coordinate range (kernel-level); maps to screen size later
    width_ = ReadAbsMax(fd_, 0 /* ABS_X */) + 1;
    height_ = ReadAbsMax(fd_, 1 /* ABS_Y */) + 1;

    wakeFd_ = eventfd(0, EFD_CLOEXEC);
    stateMutex_ = new std::mutex();
    threadHandle_ = new std::thread(&TouchImpl::Run, this);
}

TouchImpl::~TouchImpl() {
    stop_ = true;
    if (wakeFd_ >= 0) {
        uint64_t one = 1;
        ssize_t ignored = write(wakeFd_, &one, sizeof one);
        (void)ignored;
    }
    if (threadHandle_ != nullptr) {
        auto* thread = static_cast<std::thread*>(threadHandle_);
        if (thread->joinable()) {
            thread->join();
        }
        delete thread;
    }
    if (wakeFd_ >= 0) {
        close(wakeFd_);
    }
    if (fd_ >= 0) {
        close(fd_);
    }
    delete static_cast<std::mutex*>(stateMutex_);
}

int TouchImpl::CurrentPoints(int* ids, int* xs, int* ys, int max) const {
    (void)ids;
    (void)xs;
    (void)ys;
    (void)max;
    // Snapshot of the slot state is held by the reader; this is a convenience
    // for polling callers. For brevity in this iteration, polling returns 0;
    // event-driven use is the main path. The reader fires Pressed/Moved/Released
    // with full point sets, so UI code should not need polling.
    return 0;
}

void TouchImpl::Run() {
    // Multi-touch type B state: per-slot {trackingId, x, y}. A slot gets a
    // trackingId >= 0 when a finger lands; it goes to -1 when the finger
    // lifts. Position updates are per the current slot. SYN_REPORT ends a
    // frame; that is when we fire the aggregated event. All event codes
    // (EV_SYN, SYN_REPORT, BTN_TOUCH, ABS_X/Y, ABS_MT_*) come from <linux/input.h>.
    std::array<Point, MaxPoints> slots{};
    int slot = 0;
    // Single-touch fallback (devices without MT): track one virtual point
    int stX = 0, stY = 0;
    bool stDown = false;
    bool haveMtEventsSeen = false;

    int previousPointCount = 0;

    pollfd fds[2]{};
    fds[0].fd = fd_;
    fds[0].events = POLLIN;
    fds[1].fd = wakeFd_;
    fds[1].events = POLLIN;

    while (!stop_) {
        int ready = poll(fds, 2, -1);
        if (ready <= 0 || stop_) {
            continue;
        }
        if (fds[0].revents & (POLLHUP | POLLERR)) {
            connected_ = false;
            if (callback_) {
                callback_(context_, EventType::Released, 0, nullptr);
            }
            break;
        }
        if (!(fds[0].revents & POLLIN)) {
            continue;
        }

        input_event events[64];
        ssize_t got = read(fd_, events, sizeof events);
        if (got <= 0) {
            if (got < 0 && errno == ENODEV) {
                connected_ = false;
                if (callback_) {
                    callback_(context_, EventType::Released, 0, nullptr);
                }
                break;
            }
            continue;
        }

        bool somethingChanged = false;
        for (ssize_t i = 0; i < got / static_cast<ssize_t>(sizeof(input_event)); ++i) {
            const input_event& e = events[i];
            if (e.type == EV_ABS) {
                if (e.code == ABS_MT_SLOT) {
                    haveMtEventsSeen = true;
                    if (e.value >= 0 && e.value < MaxPoints) {
                        slot = e.value;
                    }
                } else if (e.code == ABS_MT_TRACKING_ID) {
                    haveMtEventsSeen = true;
                    if (e.value < 0) {
                        slots[slot].id = -1;
                    } else {
                        slots[slot].id = e.value;
                    }
                    somethingChanged = true;
                } else if (e.code == ABS_MT_POSITION_X) {
                    haveMtEventsSeen = true;
                    slots[slot].x = e.value;
                    somethingChanged = true;
                } else if (e.code == ABS_MT_POSITION_Y) {
                    haveMtEventsSeen = true;
                    slots[slot].y = e.value;
                    somethingChanged = true;
                } else if (e.code == ABS_X && !haveMtEventsSeen) {
                    stX = e.value;
                } else if (e.code == ABS_Y && !haveMtEventsSeen) {
                    stY = e.value;
                }
            } else if (e.type == EV_KEY && e.code == BTN_TOUCH && !haveMtEventsSeen) {
                stDown = (e.value != 0);
                somethingChanged = true;
            } else if (e.type == EV_SYN && e.code == SYN_REPORT) {
                if (!somethingChanged) {
                    continue;
                }
                somethingChanged = false;

                // Build the current point list (slots with id >= 0; or ST point if active)
                Point active[MaxPoints];
                int count = 0;
                if (haveMtEventsSeen) {
                    for (int s = 0; s < MaxPoints; ++s) {
                        if (slots[s].id >= 0) {
                            active[count++] = slots[s];
                        }
                    }
                } else if (stDown) {
                    active[0] = {0, stX, stY};
                    count = 1;
                }

                EventType eventType;
                if (count > 0 && previousPointCount == 0) {
                    eventType = EventType::Pressed;
                } else if (count == 0 && previousPointCount > 0) {
                    eventType = EventType::Released;
                } else {
                    eventType = EventType::Moved;
                }
                previousPointCount = count;
                if (callback_) {
                    callback_(context_, eventType, count, active);
                }
            }
        }
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::Touch
