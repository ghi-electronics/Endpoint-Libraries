#include "Storage.h"

#include <dirent.h>
#include <fcntl.h>
#include <linux/netlink.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/mount.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/statvfs.h>
#include <sys/wait.h>
#include <unistd.h>

#include <algorithm>
#include <cerrno>
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

namespace GHIElectronics::Endpoint::Internal::Storage {

namespace {

using Clock = std::chrono::steady_clock;

// Platform SD-card host prefixes seen in sysfs.
//  - "/58005000.mmc/"     EPM815 (STM32MP1)  SDMMC1
//  - "/40180000.sdhci/"   EPN256 (MA35D1)    SDHC0 (user's SD slot)
// A disk whose realpath contains any of these is "the SD card slot".
constexpr const char* SdCardHost1 = "/58005000.mmc/";
constexpr const char* SdCardHost2 = "/40180000.sdhci/";

// A new disk is looked at a moment after the kernel reports it, when its
// partitions are there too
constexpr auto Settle = std::chrono::milliseconds(300);

std::string ReadLine(const std::string& path) {
    std::ifstream file(path);
    std::string line;
    std::getline(file, line);
    return line;
}

std::string RealPath(const std::string& path) {
    char real[PATH_MAX];
    return realpath(path.c_str(), real) != nullptr ? real : "";
}

// The mount points and their file systems, from /proc/self/mountinfo
struct MountEntry {
    std::string majorMinor, mountPoint, type, source;
};

std::vector<MountEntry> Mounts() {
    std::vector<MountEntry> mounts;
    std::ifstream file("/proc/self/mountinfo");
    std::string line;
    while (std::getline(file, line)) {
        std::istringstream fields(line);
        std::string id, parent, majorMinor, root, mountPoint;
        fields >> id >> parent >> majorMinor >> root >> mountPoint;
        auto separator = line.find(" - ");
        std::string type, source;
        if (separator != std::string::npos) {
            std::istringstream(line.substr(separator + 3)) >> type >> source;
        }
        // Spaces and the like are written as \040
        std::string unescaped;
        for (size_t i = 0; i < mountPoint.size(); i++) {
            if (mountPoint[i] == '\\' && i + 3 < mountPoint.size()) {
                unescaped += static_cast<char>(std::strtol(mountPoint.substr(i + 1, 3).c_str(), nullptr, 8));
                i += 3;
            }
            else {
                unescaped += mountPoint[i];
            }
        }
        mounts.push_back({majorMinor, unescaped, type, source});
    }
    return mounts;
}

// The disks the system itself is on (the root file system, /data, ...):
// every disk with something mounted outside /media
std::set<std::string> SystemDisks() {
    std::set<std::string> disks;
    for (const auto& mount : Mounts()) {
        if (mount.mountPoint.rfind("/media/", 0) == 0) {
            continue;
        }
        auto path = RealPath("/sys/dev/block/" + mount.majorMinor);
        if (path.empty()) {
            continue;
        }
        // A partition's folder is inside its disk's
        if (access((path + "/partition").c_str(), F_OK) == 0) {
            path = path.substr(0, path.rfind('/'));
        }
        disks.insert(path.substr(path.rfind('/') + 1));
    }
    return disks;
}

// The SD card slot or a USB drive; anything else (the eMMC, its boot areas) is not ours
std::optional<Kind> KindOf(const std::string& disk) {
    auto path = RealPath("/sys/block/" + disk);
    if (disk.rfind("sd", 0) == 0 && path.find("/usb") != std::string::npos) {
        return Kind::UsbHostMassStorage;
    }
    if (disk.rfind("mmcblk", 0) == 0 && disk.find("boot") == std::string::npos &&
        disk.find("rpmb") == std::string::npos &&
        (path.find(SdCardHost1) != std::string::npos ||
         path.find(SdCardHost2) != std::string::npos)) {
        return Kind::SdCard;
    }
    return std::nullopt;
}

// ---------------------------------------------------------------- file systems

struct Volume {
    std::string device;  // "/dev/sda1"
    std::string type;    // for mount: "vfat", "exfat", "ntfs" or "ext4"
    std::string label;
};

uint16_t U16(const std::vector<uint8_t>& data, size_t at) { return data[at] | data[at + 1] << 8; }
uint32_t U32(const std::vector<uint8_t>& data, size_t at) { return U16(data, at) | uint32_t(U16(data, at + 2)) << 16; }

std::vector<uint8_t> ReadAt(int fd, uint64_t offset, size_t size) {
    std::vector<uint8_t> data(size);
    ssize_t read = pread(fd, data.data(), size, static_cast<off_t>(offset));
    data.resize(read > 0 ? read : 0);
    return data;
}

std::string TrimEnd(std::string text) {
    while (!text.empty() && (text.back() == ' ' || text.back() == '\0')) {
        text.pop_back();
    }
    return text;
}

void AppendUtf8(std::string& text, uint32_t c) {
    if (c < 0x80) {
        text += static_cast<char>(c);
    }
    else if (c < 0x800) {
        text += static_cast<char>(0xC0 | c >> 6);
        text += static_cast<char>(0x80 | (c & 0x3F));
    }
    else {
        text += static_cast<char>(0xE0 | c >> 12);
        text += static_cast<char>(0x80 | (c >> 6 & 0x3F));
        text += static_cast<char>(0x80 | (c & 0x3F));
    }
}

// FAT: the label is the root folder's volume entry (what Windows shows);
// older tools write it only in the boot sector
std::string FatLabel(int fd, const std::vector<uint8_t>& boot, bool fat32) {
    unsigned bytesPerSector = U16(boot, 0x0B), sectorsPerCluster = boot[0x0D], reserved = U16(boot, 0x0E), fats = boot[0x10];
    if (bytesPerSector >= 512 && sectorsPerCluster > 0) {
        uint64_t offset;
        size_t size;
        if (fat32) {
            uint64_t firstData = reserved + uint64_t(fats) * U32(boot, 0x24);
            uint32_t rootCluster = std::max<uint32_t>(U32(boot, 0x2C), 2);
            offset = (firstData + uint64_t(rootCluster - 2) * sectorsPerCluster) * bytesPerSector;
            size = size_t(sectorsPerCluster) * bytesPerSector;
        }
        else {
            offset = (reserved + uint64_t(fats) * U16(boot, 0x16)) * bytesPerSector;
            size = size_t(U16(boot, 0x11)) * 32;
        }
        auto folder = ReadAt(fd, offset, std::min<size_t>(size, 65536));
        for (size_t at = 0; at + 32 <= folder.size(); at += 32) {
            uint8_t first = folder[at], attributes = folder[at + 11];
            if (first == 0) {
                break;
            }
            if (first != 0xE5 && attributes != 0x0F && (attributes & 0x18) == 0x08) {
                return TrimEnd(std::string(folder.begin() + at, folder.begin() + at + 11));
            }
        }
    }
    size_t at = fat32 ? 0x47 : 0x2B;
    auto label = TrimEnd(std::string(boot.begin() + at, boot.begin() + at + 11));
    return label == "NO NAME" ? "" : label;
}

// exFAT: the label is an entry of the root folder (UTF-16)
std::string ExfatLabel(int fd, const std::vector<uint8_t>& boot) {
    unsigned sectorShift = boot[0x6C], clusterShift = boot[0x6D];
    uint32_t heap = U32(boot, 0x58), root = U32(boot, 0x60);
    if (sectorShift < 9 || sectorShift > 12 || clusterShift > 25 || root < 2) {
        return "";
    }
    uint64_t offset = (uint64_t(heap) + (uint64_t(root - 2) << clusterShift)) << sectorShift;
    auto folder = ReadAt(fd, offset, std::min<uint64_t>(uint64_t(1) << (sectorShift + clusterShift), 65536));
    for (size_t at = 0; at + 32 <= folder.size(); at += 32) {
        if (folder[at] == 0) {
            break;
        }
        if (folder[at] == 0x83) {
            std::string label;
            for (int i = 0; i < std::min<int>(folder[at + 1], 11); i++) {
                AppendUtf8(label, U16(folder, at + 2 + 2 * i));
            }
            return label;
        }
    }
    return "";
}

// The file system on a partition (or a disk without partitions), if the board can mount it
std::optional<Volume> Probe(const std::string& device) {
    int fd = open(device.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd < 0) {
        return std::nullopt;
    }
    struct Closer {
        int fd;
        ~Closer() { close(fd); }
    } closer{fd};

    auto boot = ReadAt(fd, 0, 512);
    if (boot.size() < 512) {
        return std::nullopt;
    }
    auto has = [&](size_t at, std::string_view text) { return std::memcmp(boot.data() + at, text.data(), text.size()) == 0; };
    bool signature = boot[510] == 0x55 && boot[511] == 0xAA;
    if (has(3, "NTFS    ")) {
        return Volume{device, "ntfs", ""};
    }
    if (has(3, "EXFAT   ")) {
        return Volume{device, "exfat", ExfatLabel(fd, boot)};
    }
    if (signature && has(0x52, "FAT32")) {
        return Volume{device, "vfat", FatLabel(fd, boot, true)};
    }
    if (signature && has(0x36, "FAT")) {
        return Volume{device, "vfat", FatLabel(fd, boot, false)};
    }
    auto super = ReadAt(fd, 1024, 1024);
    if (super.size() == 1024 && U16(super, 0x38) == 0xEF53) {
        std::string label(reinterpret_cast<const char*>(super.data()) + 0x78, 16);
        return Volume{device, "ext4", label.substr(0, label.find('\0'))};
    }
    return std::nullopt;
}

// The disk's partitions in order ("sda1", "sda2"; "mmcblk0p1")
std::vector<std::string> Partitions(const std::string& disk) {
    std::vector<std::pair<int, std::string>> found;
    if (DIR* folder = opendir(("/sys/block/" + disk).c_str())) {
        while (auto* entry = readdir(folder)) {
            std::string name = entry->d_name;
            if (name.rfind(disk, 0) == 0 && name != disk) {
                auto number = ReadLine("/sys/block/" + disk + "/" + name + "/partition");
                if (!number.empty()) {
                    found.emplace_back(std::atoi(number.c_str()), name);
                }
            }
        }
        closedir(folder);
    }
    std::sort(found.begin(), found.end());
    std::vector<std::string> names;
    for (auto& [number, name] : found) {
        names.push_back(name);
    }
    return names;
}

// The first partition the board can mount, or the whole disk if it has no partitions
std::optional<Volume> FindVolume(const std::string& disk) {
    auto partitions = Partitions(disk);
    if (partitions.empty()) {
        return Probe("/dev/" + disk);
    }
    for (const auto& partition : partitions) {
        if (auto volume = Probe("/dev/" + partition)) {
            return volume;
        }
    }
    return std::nullopt;
}

// The folders the library mounts drives on: /media/sd and /media/usb0, usb1, ...
bool IsOurs(const std::string& point) {
    if (point == "/media/sd") {
        return true;
    }
    constexpr std::string_view usb = "/media/usb";
    return point.size() > usb.size() && point.starts_with(usb) &&
           point.find_first_not_of("0123456789", usb.size()) == std::string::npos;
}

// ---------------------------------------------------------------- the watcher

struct Drive {
    intptr_t hdc;
    Kind kind;
    std::string label;
    std::string mountPoint;  // "/media/usb0", empty when not mounted
};

struct Event {
    const Controller* target;  // nullptr: every enabled controller of the kind
    Kind kind;
    intptr_t hdc;
    int status;
    std::string label;
};

// One for the program: the thread that listens to the kernel, the drives,
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
        // The drives already there
        for (const auto& [disk, drive] : drives) {
            if (drive.kind == controller->GetKind()) {
                events.push_back({controller, drive.kind, drive.hdc, StatusOf(disk), drive.label});
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

    std::string Mount(intptr_t hdc) {
        std::lock_guard lock(gate);
        std::string disk;
        Drive& drive = Find(hdc, &disk);
        if (!drive.mountPoint.empty()) {
            return drive.mountPoint + "/";
        }
        auto volume = FindVolume(disk);
        if (!volume) {
            ThrowNotSupported("The drive has no file system the board can read: format it as FAT32 or exFAT");
        }

        // Mounted by an earlier program (mounts stay, as drives do on Windows): the same folder
        for (const auto& mount : Mounts()) {
            if (mount.source == volume->device && IsOurs(mount.mountPoint)) {
                drive.mountPoint = mount.mountPoint;
                return drive.mountPoint + "/";
            }
        }

        // The SD card is /media/sd; a USB drive gets the first free /media/usbN
        std::string point = "/media/sd";
        if (drive.kind == Kind::UsbHostMassStorage) {
            std::set<std::string> used;
            for (const auto& mount : Mounts()) {
                used.insert(mount.mountPoint);
            }
            for (const auto& [name, other] : drives) {
                used.insert(other.mountPoint);
            }
            for (int number = 0;; number++) {
                point = "/media/usb" + std::to_string(number);
                if (!used.count(point)) {
                    break;
                }
            }
        }
        mkdir("/media", 0755);
        if (mkdir(point.c_str(), 0755) != 0 && errno != EEXIST) {
            ThrowSystemError("Cannot make " + point);
        }

        // Fast, and FAT writes a file's data when it is closed ("flush"). Everyone
        // may read, the owner (root) may write, as on other Linux systems.
        unsigned long flags = MS_NOSUID | MS_NODEV | MS_NOATIME;
        std::string options;
        if (volume->type == "vfat") {
            options = "utf8,shortname=mixed,flush,errors=remount-ro,umask=0022";
        }
        else if (volume->type == "exfat") {
            options = "iocharset=utf8,errors=remount-ro,umask=0022";
        }
        else if (volume->type == "ntfs") {
            flags |= MS_RDONLY;  // the kernel's NTFS driver only reads
            options = "nls=utf8";
        }
        int result = mount(volume->device.c_str(), point.c_str(), volume->type.c_str(), flags, options.c_str());
        if (result != 0 && (errno == EROFS || errno == EACCES) && !(flags & MS_RDONLY)) {
            // A write-protected card: read only
            result = mount(volume->device.c_str(), point.c_str(), volume->type.c_str(), flags | MS_RDONLY, options.c_str());
        }
        if (result != 0) {
            int error = errno;
            rmdir(point.c_str());
            errno = error;
            ThrowSystemError("Cannot mount the drive (" + volume->device + ", " + volume->type + ")");
        }
        drive.mountPoint = point;
        return point + "/";
    }

    void Flush(intptr_t hdc) {
        std::string point;
        {
            std::lock_guard lock(gate);
            point = Find(hdc).mountPoint;
        }
        if (point.empty()) {
            ThrowInvalidOperation("The drive is not mounted: call FileSystem.Mount first");
        }
        int fd = open(point.c_str(), O_RDONLY | O_DIRECTORY | O_CLOEXEC);
        if (fd < 0) {
            ThrowSystemError("Cannot flush " + point);
        }
        int result = syncfs(fd);
        int error = errno;
        close(fd);
        if (result != 0) {
            errno = error;
            ThrowSystemError("Cannot flush " + point);
        }
    }

    void Unmount(intptr_t hdc) {
        std::lock_guard lock(gate);
        Drive& drive = Find(hdc);
        if (drive.mountPoint.empty()) {
            return;
        }
        int fd = open(drive.mountPoint.c_str(), O_RDONLY | O_DIRECTORY | O_CLOEXEC);
        if (fd >= 0) {
            syncfs(fd);
            close(fd);
        }
        // Files still open: the mount goes away from the folder now, and ends when they are closed
        if (umount2(drive.mountPoint.c_str(), 0) != 0 && errno == EBUSY) {
            umount2(drive.mountPoint.c_str(), MNT_DETACH);
        }
        rmdir(drive.mountPoint.c_str());
        drive.mountPoint.clear();
    }

private:
    Watcher() = default;

    // Load the USB mass-storage driver stack on demand. Pay-for-use rule
    // ([[feedback-boot-time-pay-for-use]]): the SCSI + USB_STORAGE + SD
    // modules are not loaded at boot, so a program that uses only the SD
    // card slot pays nothing. The first time any StorageController is
    // enabled we probe them here (quietly; -q so a chip-family we do not
    // have in the firmware does not spam the console). modprobe
    // auto-resolves dependencies, so pulling in usb_storage brings in
    // scsi_mod and usbcore too.
    void LoadUsbStorageDrivers() {
        static const char* const mods[] = {
            "sd_mod",         // SCSI disk
            "usb_storage",    // USB mass storage class
            "uas",            // USB attached SCSI (faster path on USB 3.x)
            "exfat",          // exFAT (external drives > 32 GB)
            "ntfs3",          // NTFS (Windows-formatted drives)
            "iso9660",        // CD-ROM mode USB drives
            "udf",            // UDF-formatted drives
        };
        for (const char* mod : mods) {
            pid_t child = fork();
            if (child == 0) {
                char* argv[] = {const_cast<char*>("modprobe"),
                                const_cast<char*>("-q"),
                                const_cast<char*>(mod), nullptr};
                execv("/sbin/modprobe", argv);
                _exit(127);
            }
            if (child > 0) {
                while (waitpid(child, nullptr, 0) < 0 && errno == EINTR) {}
            }
        }
    }

    void Start() {
        LoadUsbStorageDrivers();
        sock = socket(AF_NETLINK, SOCK_DGRAM | SOCK_CLOEXEC, NETLINK_KOBJECT_UEVENT);
        if (sock < 0) {
            ThrowSystemError("Cannot watch for drives");
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
            ThrowSystemError("Cannot watch for drives");
        }
        wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
        systemDisks = SystemDisks();

        // A drive pulled out while no program watched leaves its mount behind: remove it
        for (const auto& mount : Mounts()) {
            if (IsOurs(mount.mountPoint) && access(mount.source.c_str(), F_OK) != 0) {
                umount2(mount.mountPoint.c_str(), MNT_DETACH);
                rmdir(mount.mountPoint.c_str());
            }
        }

        // The drives already there, without events (Enable reports them to
        // its controller). One that comes meanwhile is looked at again: no harm.
        if (DIR* folder = opendir("/sys/block")) {
            while (auto* entry = readdir(folder)) {
                if (entry->d_name[0] != '.') {
                    Check(entry->d_name, false);
                }
            }
            closedir(folder);
        }
        std::thread(&Watcher::Run, this).detach();
        started = true;
    }

    void Run() {
        while (true) {
            int timeout = -1;
            {
                std::lock_guard lock(gate);
                auto now = Clock::now();
                for (auto it = due.begin(); it != due.end();) {
                    if (it->second <= now) {
                        auto disk = it->first;
                        it = due.erase(it);
                        Check(disk, true);
                    }
                    else {
                        int ms = static_cast<int>(std::chrono::duration_cast<std::chrono::milliseconds>(it->second - now).count()) + 1;
                        timeout = timeout < 0 ? ms : std::min(timeout, ms);
                        ++it;
                    }
                }
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

    // The kernel's messages: "add@/devices/...\0ACTION=add\0SUBSYSTEM=block\0DEVNAME=sda\0DEVTYPE=disk\0..."
    void Receive() {
        char buffer[8192];
        while (true) {
            ssize_t length = recv(sock, buffer, sizeof buffer - 1, MSG_DONTWAIT);
            if (length <= 0) {
                return;
            }
            buffer[length] = '\0';
            std::string_view action, subsystem, name, type;
            for (char* field = buffer; field < buffer + length; field += std::strlen(field) + 1) {
                std::string_view text(field);
                if (text.starts_with("ACTION=")) {
                    action = text.substr(7);
                }
                else if (text.starts_with("SUBSYSTEM=")) {
                    subsystem = text.substr(10);
                }
                else if (text.starts_with("DEVNAME=")) {
                    name = text.substr(8);
                }
                else if (text.starts_with("DEVTYPE=")) {
                    type = text.substr(8);
                }
            }
            if (subsystem != "block" || type != "disk" || name.empty() || name.find('/') != std::string_view::npos) {
                continue;
            }
            std::lock_guard lock(gate);
            if (action == "remove") {
                due.erase(std::string(name));
                Check(std::string(name), true);
            }
            else if (action == "add" || action == "change") {
                // "change": a card reader got or lost its card
                due[std::string(name)] = Clock::now() + Settle;
            }
        }
    }

    // Is the disk one of ours, and is it there (a card reader without a card
    // has size 0)? A new one gets a handle, a gone one is unmounted.
    void Check(const std::string& disk, bool announce) {
        std::optional<Kind> kind;
        bool present = false;
        if (!systemDisks.count(disk) && (kind = KindOf(disk))) {
            present = std::atoll(ReadLine("/sys/block/" + disk + "/size").c_str()) > 0;
        }
        auto it = drives.find(disk);
        if (present && it == drives.end()) {
            auto volume = FindVolume(disk);
            Drive drive{nextHdc++, *kind, volume ? volume->label : "", ""};
            drives.emplace(disk, drive);
            if (announce) {
                events.push_back({nullptr, drive.kind, drive.hdc, volume ? Connected : Bad, drive.label});
            }
        }
        else if (!present && it != drives.end()) {
            if (!it->second.mountPoint.empty()) {
                umount2(it->second.mountPoint.c_str(), MNT_DETACH);
                rmdir(it->second.mountPoint.c_str());
            }
            events.push_back({nullptr, it->second.kind, it->second.hdc, Disconnected, it->second.label});
            drives.erase(it);
        }
    }

    // Connected, or Bad: there, but with no file system the board can mount
    int StatusOf(const std::string& disk) { return FindVolume(disk) ? Connected : Bad; }

    // Calls the callbacks, on this thread, without the gate held (a callback
    // may Mount, Flush, Unmount or Disable)
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
                    if ((event.target == nullptr || event.target == controller) && controller->GetKind() == event.kind) {
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
                    controller->Notify(event.hdc, event.status, event.label);
                }
                catch (...) {
                    // a callback's error is not the watcher's
                }
            }
        }
    }

    Drive& Find(intptr_t hdc, std::string* disk = nullptr) {
        for (auto& [name, drive] : drives) {
            if (drive.hdc == hdc) {
                if (disk != nullptr) {
                    *disk = name;
                }
                return drive;
            }
        }
        ThrowInvalidOperation("The drive is not connected: it was removed, or the handle is not a drive's");
    }

    void Wake() {
        uint64_t one = 1;
        (void)!write(wake, &one, sizeof one);
    }

    std::mutex gate;
    std::recursive_mutex delivering;  // held while callbacks run: Disable waits for them
    std::set<const Controller*> enabled;
    std::map<std::string, Drive> drives;  // by disk ("sda")
    std::set<std::string> systemDisks;
    std::map<std::string, Clock::time_point> due;  // disks to look at, and when
    std::vector<Event> events;
    intptr_t nextHdc = 1;
    int sock = -1;
    int wake = -1;
    bool started = false;
};

// .NET's DriveType for a file system type, as .NET gives it
int DriveTypeOf(const std::string& type) {
    enum { Unknown = 0, Fixed = 3, Network = 4, CDRom = 5, Ram = 6 };
    static const std::set<std::string> fixed{"ext2", "ext3", "ext4", "vfat", "msdos", "exfat", "ntfs", "ntfs3",
                                             "btrfs", "xfs", "f2fs", "jfs", "squashfs", "ubifs", "jffs2"};
    static const std::set<std::string> ram{"tmpfs", "ramfs", "devtmpfs", "proc", "sysfs", "devpts", "configfs",
                                           "debugfs", "securityfs", "cgroup", "cgroup2", "mqueue", "pstore", "tracefs"};
    static const std::set<std::string> network{"nfs", "nfs4", "cifs", "smb3", "smbfs", "9p"};
    if (fixed.count(type)) {
        return Fixed;
    }
    if (ram.count(type)) {
        return Ram;
    }
    if (network.count(type)) {
        return Network;
    }
    if (type == "iso9660" || type == "udf") {
        return CDRom;
    }
    return Unknown;
}

}  // namespace

Controller::Controller(const std::string& name, Callback callback) : callback(std::move(callback)) {
    if (name == SdCardName || name == SdCardName2) {
        kind = Kind::SdCard;
    }
    else if (name == UsbHostMassStorageName || name == UsbHostMassStorageName2) {
        kind = Kind::UsbHostMassStorage;
    }
    else {
        ThrowArgument("There is no storage controller named \"" + name +
                      "\": use EPN256.StorageController.SdCard or "
                      "EPN256.StorageController.UsbHostMassStorage");
    }
}

Controller::~Controller() { Disable(); }

void Controller::Enable() { Watcher::Instance().Enable(this); }

void Controller::Disable() { Watcher::Instance().Disable(this); }

std::string Mount(intptr_t hdc) { return Watcher::Instance().Mount(hdc); }

void Flush(intptr_t hdc) { Watcher::Instance().Flush(hdc); }

void Unmount(intptr_t hdc) { Watcher::Instance().Unmount(hdc); }

DriveInfoData GetDriveInfo(const std::string& path) {
    DriveInfoData info;
    struct statvfs space;
    if (statvfs(path.c_str(), &space) != 0) {
        return info;
    }
    info.isReady = true;
    info.totalSize = int64_t(space.f_blocks) * space.f_frsize;
    info.totalFreeSpace = int64_t(space.f_bfree) * space.f_frsize;
    info.availableFreeSpace = int64_t(space.f_bavail) * space.f_frsize;

    // The mount the folder is in: the longest mount point that contains it
    auto full = RealPath(path);
    size_t longest = 0;
    for (const auto& mount : Mounts()) {
        const auto& point = mount.mountPoint;
        bool inside = full == point || point == "/" || full.rfind(point + "/", 0) == 0;
        if (inside && point.size() >= longest) {
            longest = point.size();
            info.format = mount.type;
        }
    }
    info.driveType = DriveTypeOf(info.format);
    return info;
}

}  // namespace GHIElectronics::Endpoint::Internal::Storage
