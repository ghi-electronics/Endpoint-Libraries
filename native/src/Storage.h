#pragma once

// SD cards and USB drives: inserted and removed (events), mounted on
// /media/sd and /media/usb0, usb1, ... (the first free number, as Windows
// takes the first free drive letter), flushed and unmounted. The library
// listens to the kernel's device events (netlink, no polling). The system's
// own disk (the DK2's SD card, the EPM815's eMMC) is never reported or mounted.
//
// A drive has a handle (hdc) from when it is inserted until it is removed;
// inserted again, it gets a new one. Mounts are fast (FAT: data written when
// a file is closed): programs call Flush before the drive is pulled out. A
// drive that is pulled out is unmounted by the library.

#include <cstdint>
#include <functional>
#include <string>

namespace GHIElectronics::Endpoint::Internal::Storage {

enum class Kind : int { SdCard = 0, UsbHostMassStorage = 1 };
enum ConnectionStatus : int { Disconnected = 0, Connected = 1, Bad = 2 };

// The names of EPM815.StorageController
constexpr const char* SdCardName = "EPM815.StorageController.SdCard";
constexpr const char* UsbHostMassStorageName = "EPM815.StorageController.UsbHostMassStorage";

// The SD card slot, or the USB host (all its drives). After Enable, the
// callback gets every drive that is connected (first the ones already
// there) or disconnected, on the library's storage thread.
class Controller {
public:
    using Callback = std::function<void(intptr_t hdc, int status, const std::string& volumeLabel)>;

    Controller(const std::string& name, Callback callback);
    ~Controller();
    Controller(const Controller&) = delete;
    Controller& operator=(const Controller&) = delete;

    void Enable();
    void Disable();

    Kind GetKind() const { return kind; }
    void Notify(intptr_t hdc, int status, const std::string& volumeLabel) const { callback(hdc, status, volumeLabel); }

private:
    Kind kind;
    Callback callback;
};

// "/media/usb0/" (the drive's folder, already mounted if it is)
std::string Mount(intptr_t hdc);
void Flush(intptr_t hdc);
void Unmount(intptr_t hdc);

// What .NET's DriveInfo gives for a folder: its file system (the mount's
// type, "vfat"), DriveType (as .NET's enum), and the sizes in bytes
struct DriveInfoData {
    std::string format;
    int driveType = 0;
    bool isReady = false;
    int64_t totalSize = 0;
    int64_t totalFreeSpace = 0;
    int64_t availableFreeSpace = 0;
};
DriveInfoData GetDriveInfo(const std::string& path);

}  // namespace GHIElectronics::Endpoint::Internal::Storage
