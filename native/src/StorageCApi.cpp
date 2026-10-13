// The hidden C layer (see CApi.h) for SD cards and USB drives

#include <cstdint>
#include <cstring>

#include "CApi.h"
#include "Storage.h"

using namespace GHIElectronics::Endpoint::Internal;

typedef void (*endpoint_storage_callback)(void* context, intptr_t hdc, int status, const char* volumeLabel);

namespace {

Storage::Controller* Controller(void* handle) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, "The StorageController is disposed");
    }
    return static_cast<Storage::Controller*>(handle);
}

void CopyText(const std::string& text, char* buffer, int size) {
    if (buffer == nullptr || size <= static_cast<int>(text.size())) {
        ThrowArgument("The buffer is too small");
    }
    std::memcpy(buffer, text.c_str(), text.size() + 1);
}

}  // namespace

// name: EPM815.StorageController.SdCard or .UsbHostMassStorage. The callback
// runs on the library's storage thread, for every drive connected (also those
// already there when the controller is enabled) or disconnected.
ENDPOINT_API int endpoint_storage_create(const char* name, endpoint_storage_callback callback, void* context,
                                         void** controller) {
    return Call([&] {
        *controller = new Storage::Controller(name != nullptr ? name : "",
                                              [callback, context](intptr_t hdc, int status, const std::string& label) {
                                                  if (callback != nullptr) {
                                                      callback(context, hdc, status, label.c_str());
                                                  }
                                              });
    });
}

ENDPOINT_API int endpoint_storage_dispose(void* controller) {
    return Call([&] { delete Controller(controller); });
}

ENDPOINT_API int endpoint_storage_enable(void* controller) {
    return Call([&] { Controller(controller)->Enable(); });
}

ENDPOINT_API int endpoint_storage_disable(void* controller) {
    return Call([&] { Controller(controller)->Disable(); });
}

// The drive's folder, "/media/usb0/"
ENDPOINT_API int endpoint_storage_mount(intptr_t hdc, char* name, int nameSize) {
    return Call([&] { CopyText(Storage::Mount(hdc), name, nameSize); });
}

ENDPOINT_API int endpoint_storage_flush(intptr_t hdc) {
    return Call([&] { Storage::Flush(hdc); });
}

ENDPOINT_API int endpoint_storage_unmount(intptr_t hdc) {
    return Call([&] { Storage::Unmount(hdc); });
}

// What .NET's DriveInfo gives for a folder (driveType: .NET's DriveType values)
ENDPOINT_API int endpoint_driveinfo_get(const char* path, char* format, int formatSize, int* driveType, int* isReady,
                                        int64_t* totalSize, int64_t* totalFreeSpace, int64_t* availableFreeSpace) {
    return Call([&] {
        auto info = Storage::GetDriveInfo(path != nullptr ? path : "");
        CopyText(info.format, format, formatSize);
        *driveType = info.driveType;
        *isReady = info.isReady;
        *totalSize = info.totalSize;
        *totalFreeSpace = info.totalFreeSpace;
        *availableFreeSpace = info.availableFreeSpace;
    });
}
