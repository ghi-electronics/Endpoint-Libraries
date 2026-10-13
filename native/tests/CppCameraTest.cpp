// Board test of the C++ Camera (only <endpoint.h>, linked with libendpoint.a)
// with a real USB webcam, found the customer way (UsbHostController). Run as
// root on the board:
//
//     ./cpp-camera-test

#include <endpoint.h>

#include <atomic>
#include <chrono>
#include <cstdio>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

using namespace GHIElectronics::Endpoint::Core;
using namespace GHIElectronics::Endpoint::Devices;
using namespace std::chrono_literals;

static int failures = 0;

static void Check(bool ok, const std::string& what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what.c_str());
    if (!ok) failures++;
}

static bool Valid(std::span<const uint8_t> jpeg) {
    return jpeg.size() > 4 && jpeg[0] == 0xFF && jpeg[1] == 0xD8 && jpeg[jpeg.size() - 2] == 0xFF && jpeg[jpeg.size() - 1] == 0xD9;
}

int main() {
    std::string webcam;
    {
        std::mutex mutex;
        auto usb = UsbHost::UsbHostController::GetDefault();
        usb.OnConnectionChangedEvent += [&](UsbHost::UsbHostController&, const UsbHost::UsbDeviceConnectionEventArgs& e) {
            std::lock_guard lock(mutex);
            if (e.Type() == UsbHost::DeviceType::Webcam && e.DeviceStatus() == DeviceConnectionStatus::Connected) {
                webcam = e.DeviceName();
            }
        };
        usb.Enable();
        std::this_thread::sleep_for(1s);
    }
    if (webcam.empty()) {
        std::printf("FAIL  no webcam plugged in\n");
        return 1;
    }

    auto camera = std::make_unique<Media::Camera>(webcam);
    Check(!camera->SupportedFormats().empty(), "Camera " + camera->DeviceName() + " '" + camera->Name() + "': " +
                                                   std::to_string(camera->SupportedFormats().size()) + " formats");
    auto t = std::chrono::steady_clock::now();
    auto jpeg = camera->Capture();
    double took = std::chrono::duration<double>(std::chrono::steady_clock::now() - t).count();
    Check(Valid(jpeg) && took > 0.5, "first picture: " + std::to_string(jpeg.size()) + " bytes in " + std::to_string(took) + " s");
    camera->SetResolution(1280, 720);
    jpeg = camera->Capture();
    Check(Valid(jpeg) && camera->Width() == 1280, "1280x720: " + std::to_string(jpeg.size()) + " bytes");
    try {
        camera->SetResolution(1000, 700);
        Check(false, "1000x700: no exception");
    }
    catch (const ArgumentException& e) {
        Check(true, std::string("1000x700: ") + e.what());
    }

    camera->SetResolution(640, 480);
    std::atomic<int> frames = 0;
    std::atomic<bool> good = true;
    camera->FrameReceived += [&](Media::Camera&, const Media::FrameReceivedEventArgs& e) {
        frames++;
        if (!Valid(e.Jpeg()) || e.Width() != 640) good = false;
    };
    camera->StartStreaming();
    std::this_thread::sleep_for(3s);
    camera->StopStreaming();
    Check(frames > 40 && good, "streaming: " + std::to_string(frames) + " frames in 3 s, each a whole JPEG");

    auto brightness = camera->GetControl(Media::CameraControl::Brightness);
    camera->SetControl(Media::CameraControl::Brightness, brightness.Maximum / 2);
    Check(camera->GetControl(Media::CameraControl::Brightness).Value == brightness.Maximum / 2,
          "Brightness " + std::to_string(brightness.Value) + " -> " + std::to_string(brightness.Maximum / 2));
    camera->SetControl(Media::CameraControl::Brightness, brightness.Value);
    Check(!camera->SupportedControls().empty(), std::to_string(camera->SupportedControls().size()) + " controls");
    camera->Dispose();
    try {
        camera->Capture();
        Check(false, "Capture after Dispose: no exception");
    }
    catch (const ObjectDisposedException&) {
        Check(true, "Capture after Dispose: ObjectDisposedException");
    }

    std::printf("\n%s\n", failures == 0 ? "ALL PASSED" : (std::to_string(failures) + " FAILED").c_str());
    return failures == 0 ? 0 : 1;
}
