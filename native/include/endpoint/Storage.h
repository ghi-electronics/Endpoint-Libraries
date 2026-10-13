// SD cards and USB drives for Endpoint, with the same API as .NET's
// GHIElectronics.Endpoint.Devices.Storage:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Storage;
//
//   auto usb = StorageController::FromName(EPM815::StorageController::UsbHostMassStorage);
//   usb.OnConnectionChangedEvent += [](StorageController& sender, const DeviceConnectionEventArgs& e) {
//       if (e.DeviceStatus() == DeviceConnectionStatus::Connected) {
//           auto drive = FileSystem::Mount(e.Hdc());          // "/media/usb0/", usb1, ...
//           std::ofstream(drive.Name() + "log.txt") << "Hello";
//           FileSystem::Flush(e.Hdc());                       // before the drive is pulled out
//       }
//   };
//   usb.Enable();   // the drives already plugged in are reported too
//
// The events come on the library's storage thread. A drive that is pulled out
// is unmounted by the library. The SD card is /media/sd/; USB drives get the
// first free /media/usbN/, as Windows gives the first free drive letter.
#pragma once

#include <cstdint>
#include <cstdio>
#include <exception>
#include <filesystem>
#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Storage {

enum class DeviceConnectionStatus { Disconnected = 0, Connected = 1, Bad = 2 };  // Bad: no file system the board reads

class DeviceConnectionEventArgs {
public:
    DeviceConnectionEventArgs(DeviceConnectionStatus status, intptr_t hdc, std::string volumeLabel)
        : status_(status), hdc_(hdc), volumeLabel_(std::move(volumeLabel)) {}

    DeviceConnectionStatus DeviceStatus() const { return status_; }

    // The drive, for FileSystem::Mount, Flush and Unmount, until it is pulled out
    intptr_t Hdc() const { return hdc_; }

    // The drive's name ("LOGGER"), empty if it has none
    const std::string& VolumeLabel() const { return volumeLabel_; }

private:
    DeviceConnectionStatus status_;
    intptr_t hdc_;
    std::string volumeLabel_;
};

// The same values as .NET's System.IO.DriveType
enum class DriveType { Unknown = 0, NoRootDirectory = 1, Removable = 2, Fixed = 3, Network = 4, CDRom = 5, Ram = 6 };

// A drive's folder and its sizes, as .NET's System.IO.DriveInfo. The drive's
// label is DeviceConnectionEventArgs::VolumeLabel.
class DriveInfo {
public:
    explicit DriveInfo(std::string driveName) : name_(std::move(driveName)) {}

    // "/media/usb0/"
    const std::string& Name() const { return name_; }
    std::filesystem::path RootDirectory() const { return name_; }

    bool IsReady() const { return Get(false).isReady != 0; }
    std::string DriveFormat() const { return Get(true).format; }   // "vfat", "exfat", "ntfs", "ext4"
    Storage::DriveType DriveType() const { return static_cast<Storage::DriveType>(Get(false).driveType); }
    int64_t TotalSize() const { return Get(true).totalSize; }
    int64_t TotalFreeSpace() const { return Get(true).totalFreeSpace; }
    int64_t AvailableFreeSpace() const { return Get(true).availableFreeSpace; }

private:
    struct Data {
        char format[64];
        int driveType, isReady;
        int64_t totalSize, totalFreeSpace, availableFreeSpace;
    };

    Data Get(bool mustBeReady) const {
        Data data{};
        Core::detail::Check(endpoint_driveinfo_get(name_.c_str(), data.format, sizeof data.format, &data.driveType,
                                                   &data.isReady, &data.totalSize, &data.totalFreeSpace,
                                                   &data.availableFreeSpace));
        if (mustBeReady && !data.isReady) {
            throw Core::IOException("The drive " + name_ + " is not ready");
        }
        return data;
    }

    std::string name_;
};

class StorageController {
public:
    // EPM815::StorageController::SdCard or UsbHostMassStorage
    static StorageController FromName(const std::string& name) { return StorageController(name); }

    ~StorageController() { Dispose(); }
    StorageController(const StorageController&) = delete;
    StorageController& operator=(const StorageController&) = delete;

    const std::string& Name() const { return name_; }

    // A drive connected or disconnected. += gives an id for -=.
    Core::Event<StorageController&, const DeviceConnectionEventArgs&> OnConnectionChangedEvent;

    // Starts reporting drives: first the ones already plugged in
    void Enable() { Core::detail::Check(endpoint_storage_enable(handle_.Get("StorageController"))); }
    void Disable() { Core::detail::Check(endpoint_storage_disable(handle_.Get("StorageController"))); }

    // Stops the events (one being delivered finishes first)
    void Dispose() { handle_.Reset(); }

private:
    explicit StorageController(std::string name) : name_(std::move(name)) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_storage_create(name_.c_str(), &Changed, this, &handle));
        handle_ = Handle(handle);
    }

    // From the library's storage thread. A handler's error must not stop the
    // events: it is printed, as .NET prints an unhandled exception.
    static void Changed(void* context, intptr_t hdc, int status, const char* volumeLabel) {
        auto self = static_cast<StorageController*>(context);
        try {
            self->OnConnectionChangedEvent(*self, DeviceConnectionEventArgs(static_cast<DeviceConnectionStatus>(status),
                                                                           hdc, volumeLabel ? volumeLabel : ""));
        }
        catch (const std::exception& e) {
            std::fprintf(stderr, "Unhandled exception in OnConnectionChangedEvent: %s\n", e.what());
        }
    }

    using Handle = Core::detail::Handle<endpoint_storage_dispose>;

    std::string name_;
    Handle handle_;  // last: closed first, before the event goes away
};

class FileSystem {
public:
    // Mounts the drive (if it is not yet) and gives its folder
    static DriveInfo Mount(intptr_t hdc) {
        char name[256];
        Core::detail::Check(endpoint_storage_mount(hdc, name, sizeof name));
        return DriveInfo(name);
    }

    // Writes everything to the drive: call it before the drive is pulled out
    static void Flush(intptr_t hdc) { Core::detail::Check(endpoint_storage_flush(hdc)); }

    static void Unmount(intptr_t hdc) { Core::detail::Check(endpoint_storage_unmount(hdc)); }
};

}  // namespace GHIElectronics::Endpoint::Devices::Storage
