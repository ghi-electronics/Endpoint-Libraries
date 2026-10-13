#include "UsbAccess.h"

#include <fcntl.h>
#include <linux/hidraw.h>
#include <linux/usbdevice_fs.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/ioctl.h>
#include <sys/stat.h>
#include <sys/sysmacros.h>
#include <unistd.h>

#include <algorithm>
#include <atomic>
#include <cerrno>
#include <climits>
#include <cstdlib>
#include <fstream>
#include <mutex>
#include <thread>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::UsbAccess {

// Waits for a device's file to report that the device is gone (poll: HUP or
// ERR), then calls the callback once. Its own thread; stopped by Stop.
struct Watch {
    int fd = -1;
    int wake = -1;
    UnpluggedCallback callback;
    std::thread thread;
    std::atomic<bool> stop{false};
    std::atomic<bool> gone{false};

    ~Watch() {
        if (wake >= 0) {
            close(wake);
        }
    }

    static std::shared_ptr<Watch> Start(int fd, UnpluggedCallback callback) {
        auto watch = std::make_shared<Watch>();
        watch->fd = fd;
        watch->callback = std::move(callback);
        watch->wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
        if (watch->wake < 0) {
            ThrowSystemError("Cannot watch the device");
        }
        watch->thread = std::thread(Run, watch);
        return watch;
    }

    static void Run(std::shared_ptr<Watch> w) {
        while (!w->stop) {
            pollfd fds[] = {{w->fd, 0, 0}, {w->wake, POLLIN, 0}};
            if (poll(fds, 2, -1) < 0) {
                if (errno == EINTR) {
                    continue;
                }
                return;
            }
            if (w->stop) {
                return;
            }
            if (fds[0].revents & (POLLHUP | POLLERR | POLLNVAL)) {
                w->gone = true;
                try {
                    if (w->callback) {
                        w->callback();
                    }
                }
                catch (...) {
                    // a callback's error is not the watcher's
                }
                return;
            }
        }
    }

    // Afterwards the callback is not called any more (one running now finishes first)
    void Stop() {
        stop = true;
        uint64_t one = 1;
        (void)!write(wake, &one, sizeof one);
        if (thread.get_id() == std::this_thread::get_id()) {
            thread.detach();  // stopped by its own callback
        }
        else if (thread.joinable()) {
            thread.join();
        }
    }
};

namespace {

std::string ReadLine(const std::string& path) {
    std::ifstream file(path);
    std::string line;
    std::getline(file, line);
    return line;
}

int Hex(const std::string& text) { return static_cast<int>(std::strtol(text.c_str(), nullptr, 16)); }

int OpenDevice(const std::string& deviceName, int flags) {
    int fd = open(deviceName.c_str(), flags | O_CLOEXEC);
    if (fd < 0) {
        if (errno == ENOENT || errno == ENODEV || errno == ENXIO) {
            ThrowArgument("There is no device " + deviceName +
                          " (it was unplugged, or the name is not a DeviceName from UsbHostController)");
        }
        ThrowSystemError("Cannot open " + deviceName);
    }
    return fd;
}

// The device's folder in /sys, from its file's device number
std::string SysFolder(int fd) {
    struct stat info{};
    if (fstat(fd, &info) != 0 || !S_ISCHR(info.st_mode)) {
        return {};
    }
    char real[PATH_MAX];
    std::string link = "/sys/dev/char/" + std::to_string(major(info.st_rdev)) + ":" + std::to_string(minor(info.st_rdev));
    return realpath(link.c_str(), real) != nullptr ? real : "";
}

// A transfer's error as the .NET exception it becomes
[[noreturn]] void ThrowTransfer(const char* what) {
    switch (errno) {
        case ETIMEDOUT: ThrowTimeout();
        case ENODEV:
        case ESHUTDOWN: ThrowIO(std::string(what) + ": the device was unplugged");
        case EPIPE: ThrowIO(std::string(what) + ": the device refused it (stall)");
        default: ThrowSystemError(what);
    }
}

}  // namespace

// ---- RawDevice

RawDevice::RawDevice(const std::string& deviceName, UnpluggedCallback unplugged) {
    fd = OpenDevice(deviceName, O_RDWR);
    folder = SysFolder(fd);
    if (folder.empty() || ReadLine(folder + "/idVendor").empty()) {
        close(fd);
        ThrowArgument(deviceName + " is not a USB device (use the DeviceName of an Unknown device from UsbHostController)");
    }
    vendorId = Hex(ReadLine(folder + "/idVendor"));
    productId = Hex(ReadLine(folder + "/idProduct"));
    try {
        LoadEndpoints();
        watch = Watch::Start(fd, std::move(unplugged));
    }
    catch (...) {
        close(fd);
        throw;
    }
}

RawDevice::~RawDevice() {
    if (watch) {
        watch->Stop();
    }
    for (int interface : claimed) {
        unsigned int number = static_cast<unsigned int>(interface);
        ioctl(fd, USBDEVFS_RELEASEINTERFACE, &number);
    }
    // The interfaces taken from a kernel driver go back to it
    for (int interface : detached) {
        usbdevfs_ioctl command{interface, USBDEVFS_CONNECT, nullptr};
        ioctl(fd, USBDEVFS_IOCTL, &command);
    }
    close(fd);
}

bool RawDevice::Connected() const { return !watch->gone; }

// The endpoints of the active configuration and their interfaces (from the
// descriptors the kernel keeps: reading the file gives them)
void RawDevice::LoadEndpoints() {
    std::vector<uint8_t> data(65536);
    lseek(fd, 0, SEEK_SET);
    ssize_t length = read(fd, data.data(), data.size());
    if (length < 18) {
        ThrowSystemError("Cannot read the USB device's descriptors");
    }
    data.resize(static_cast<size_t>(length));
    int active = std::atoi(ReadLine(folder + "/bConfigurationValue").c_str());
    endpoints.clear();
    size_t offset = data[0];  // after the device descriptor
    while (offset + 9 <= data.size() && data[offset + 1] == 2) {
        size_t total = data[offset + 2] | (data[offset + 3] << 8);
        size_t end = std::min(data.size(), offset + std::max<size_t>(total, 9));
        if (data[offset + 5] == active) {
            int interface = -1;
            for (size_t at = offset; at + 2 <= end && data[at] >= 2; at += data[at]) {
                if (data[at + 1] == 4 && at + 9 <= end) {
                    interface = data[at + 2];
                }
                else if (data[at + 1] == 5 && at + 7 <= end && interface >= 0) {
                    int address = data[at + 2];
                    if (std::none_of(endpoints.begin(), endpoints.end(), [&](auto& e) { return e.first == address; })) {
                        endpoints.push_back({address, {interface, data[at + 3]}});
                    }
                }
            }
        }
        offset = end;
    }
}

// Claims an interface for this program, taking it from its kernel driver if it has one
void RawDevice::Claim(int interface) {
    if (std::find(claimed.begin(), claimed.end(), interface) != claimed.end()) {
        return;
    }
    unsigned int number = static_cast<unsigned int>(interface);
    if (ioctl(fd, USBDEVFS_CLAIMINTERFACE, &number) != 0) {
        if (errno != EBUSY) {
            ThrowTransfer("Cannot use the interface");
        }
        usbdevfs_getdriver driver{};
        driver.interface = number;
        bool kernelDriver = ioctl(fd, USBDEVFS_GETDRIVER, &driver) == 0 && std::string(driver.driver) != "usbfs";
        usbdevfs_ioctl command{interface, USBDEVFS_DISCONNECT, nullptr};
        if (!kernelDriver || ioctl(fd, USBDEVFS_IOCTL, &command) != 0 || ioctl(fd, USBDEVFS_CLAIMINTERFACE, &number) != 0) {
            if (kernelDriver) {
                usbdevfs_ioctl back{interface, USBDEVFS_CONNECT, nullptr};
                ioctl(fd, USBDEVFS_IOCTL, &back);
            }
            ThrowInvalidOperation("Interface " + std::to_string(interface) + " is in use by another program");
        }
        detached.push_back(interface);
    }
    claimed.push_back(interface);
}

int RawDevice::Control(int requestType, int request, int value, int index, uint8_t* data, int length, int timeoutMs) {
    if (length < 0 || length > 0xFFFF || (length > 0 && data == nullptr)) {
        ThrowArgumentOutOfRange("A control transfer has 0 to 65535 bytes");
    }
    if (timeoutMs < 0) {
        timeoutMs = 0;
    }
    // Choosing a configuration or an alternate setting goes through the kernel
    if (requestType == 0x00 && request == 0x09) {
        unsigned int configuration = static_cast<unsigned int>(value);
        if (ioctl(fd, USBDEVFS_SETCONFIGURATION, &configuration) != 0) {
            if (errno == EBUSY) {
                ThrowInvalidOperation("The configuration cannot change while kernel drivers use the device");
            }
            ThrowTransfer("SET_CONFIGURATION failed");
        }
        LoadEndpoints();
        return 0;
    }
    if (requestType == 0x01 && request == 0x0B) {
        Claim(index);
        usbdevfs_setinterface setting{static_cast<unsigned int>(index), static_cast<unsigned int>(value)};
        if (ioctl(fd, USBDEVFS_SETINTERFACE, &setting) != 0) {
            ThrowTransfer("SET_INTERFACE failed");
        }
        return 0;
    }
    // A request to an interface or an endpoint needs its interface
    int recipient = requestType & 0x1F;
    if (recipient == 1) {
        Claim(index & 0xFF);
    }
    else if (recipient == 2) {
        for (const auto& [address, info] : endpoints) {
            if (address == (index & 0xFF)) {
                Claim(info.interface);
            }
        }
    }
    usbdevfs_ctrltransfer transfer{};
    transfer.bRequestType = static_cast<uint8_t>(requestType);
    transfer.bRequest = static_cast<uint8_t>(request);
    transfer.wValue = static_cast<uint16_t>(value);
    transfer.wIndex = static_cast<uint16_t>(index);
    transfer.wLength = static_cast<uint16_t>(length);
    transfer.timeout = static_cast<uint32_t>(timeoutMs);
    transfer.data = data;
    int result = ioctl(fd, USBDEVFS_CONTROL, &transfer);
    if (result < 0) {
        ThrowTransfer("The control transfer failed");
    }
    return result;
}

int RawDevice::Transfer(int endpoint, uint8_t* data, int length, int timeoutMs) {
    if (length < 0 || (length > 0 && data == nullptr)) {
        ThrowArgumentOutOfRange("The length must not be negative");
    }
    auto it = std::find_if(endpoints.begin(), endpoints.end(), [&](auto& e) { return e.first == endpoint; });
    if (it == endpoints.end()) {
        ThrowArgument("The device has no endpoint 0x" + [&] {
            char hex[8];
            std::snprintf(hex, sizeof hex, "%02X", endpoint & 0xFF);
            return std::string(hex);
        }() + " in its active configuration");
    }
    int kind = it->second.attributes & 0x03;
    if (kind == 1) {
        ThrowNotSupported("Isochronous endpoints are not supported");
    }
    Claim(it->second.interface);
    usbdevfs_bulktransfer transfer{};
    transfer.ep = static_cast<unsigned int>(endpoint);
    transfer.len = static_cast<unsigned int>(length);
    transfer.timeout = static_cast<unsigned int>(std::max(timeoutMs, 0));
    transfer.data = data;
    int result = ioctl(fd, USBDEVFS_BULK, &transfer);
    if (result < 0) {
        ThrowTransfer("The transfer failed");
    }
    return result;
}

// ---- HidDevice

HidDevice::HidDevice(const std::string& deviceName, UnpluggedCallback unplugged) {
    fd = OpenDevice(deviceName, O_RDWR | O_NONBLOCK);
    hidraw_devinfo info{};
    if (ioctl(fd, HIDIOCGRAWINFO, &info) != 0) {
        close(fd);
        ThrowArgument(deviceName + " is not a HID device (use the DeviceName of a HID device from UsbHostController)");
    }
    vendorId = static_cast<uint16_t>(info.vendor);
    productId = static_cast<uint16_t>(info.product);
    char text[256] = {};
    if (ioctl(fd, HIDIOCGRAWNAME(sizeof text - 1), text) >= 0) {
        name = text;
    }
    try {
        watch = Watch::Start(fd, std::move(unplugged));
    }
    catch (...) {
        close(fd);
        throw;
    }
}

HidDevice::~HidDevice() {
    if (watch) {
        watch->Stop();
    }
    close(fd);
}

bool HidDevice::Connected() const { return !watch->gone; }

std::vector<uint8_t> HidDevice::ReportDescriptor() const {
    int size = 0;
    if (ioctl(fd, HIDIOCGRDESCSIZE, &size) != 0) {
        ThrowTransfer("Cannot read the report descriptor");
    }
    hidraw_report_descriptor descriptor{};
    descriptor.size = static_cast<uint32_t>(std::clamp(size, 0, HID_MAX_DESCRIPTOR_SIZE));
    if (ioctl(fd, HIDIOCGRDESC, &descriptor) != 0) {
        ThrowTransfer("Cannot read the report descriptor");
    }
    return {descriptor.value, descriptor.value + descriptor.size};
}

int HidDevice::Write(const uint8_t* data, int length) {
    if (length < 1 || data == nullptr) {
        ThrowArgument("A report has at least its report ID (0 if the device has none)");
    }
    while (true) {
        ssize_t result = write(fd, data, static_cast<size_t>(length));
        if (result >= 0) {
            return static_cast<int>(result);
        }
        if (errno == EAGAIN) {
            pollfd out{fd, POLLOUT, 0};
            poll(&out, 1, 1000);
            continue;
        }
        if (errno != EINTR) {
            ThrowTransfer("Cannot write the report");
        }
    }
}

int HidDevice::Read(uint8_t* buffer, int size, int timeoutMs) {
    if (size < 1 || buffer == nullptr) {
        ThrowArgument("The buffer is empty");
    }
    while (true) {
        ssize_t result = read(fd, buffer, static_cast<size_t>(size));
        if (result >= 0) {
            return static_cast<int>(result);
        }
        if (errno == EINTR) {
            continue;
        }
        if (errno != EAGAIN) {
            ThrowTransfer("Cannot read a report");
        }
        pollfd in{fd, POLLIN, 0};
        int ready = poll(&in, 1, timeoutMs < 0 ? -1 : timeoutMs);
        if (ready == 0) {
            return 0;
        }
        if (ready > 0 && (in.revents & (POLLHUP | POLLERR)) && !(in.revents & POLLIN)) {
            errno = ENODEV;
            ThrowTransfer("Cannot read a report");
        }
    }
}

int HidDevice::GetFeatureReport(uint8_t* buffer, int size) {
    if (size < 1 || buffer == nullptr) {
        ThrowArgument("The buffer must hold at least the report ID");
    }
    int result = ioctl(fd, HIDIOCGFEATURE(size), buffer);
    if (result < 0) {
        ThrowTransfer("Cannot get the feature report");
    }
    return result;
}

void HidDevice::SendFeatureReport(const uint8_t* data, int length) {
    if (length < 1 || data == nullptr) {
        ThrowArgument("A report has at least its report ID (0 if the device has none)");
    }
    if (ioctl(fd, HIDIOCSFEATURE(length), data) < 0) {
        ThrowTransfer("Cannot send the feature report");
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::UsbAccess
