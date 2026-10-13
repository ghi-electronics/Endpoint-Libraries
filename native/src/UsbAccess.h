#pragma once

// A program's own USB devices, for .NET's RawDevice and HidDevice (Python
// uses pyusb and hid, C/C++ libusb and hidapi):
// - RawDevice: the device's USB file ("/dev/bus/usb/001/005", the DeviceName
//   of an Unknown device): control transfers, and bulk and interrupt
//   transfers on an endpoint. An interface that a kernel driver has is taken
//   from it while the program uses it, and given back after.
// - HidDevice: a HID device's hidraw file ("/dev/hidraw0", the DeviceName of
//   a HID device): reports out and in, feature reports, report descriptor.
// Both report when the device is unplugged (a thread waits for it).

#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <vector>

namespace GHIElectronics::Endpoint::Internal::UsbAccess {

using UnpluggedCallback = std::function<void()>;

struct Watch;

// Waits (on its own thread) for a device's file to report that the device is
// gone, then calls unplugged once. StopWatch: not called any more (one running
// now finishes first).
std::shared_ptr<Watch> WatchUnplug(int fd, UnpluggedCallback unplugged);
void StopWatch(const std::shared_ptr<Watch>& watch);
bool IsGone(const std::shared_ptr<Watch>& watch);

class RawDevice {
public:
    RawDevice(const std::string& deviceName, UnpluggedCallback unplugged);
    // Gives the interfaces back to their drivers
    ~RawDevice();
    RawDevice(const RawDevice&) = delete;
    RawDevice& operator=(const RawDevice&) = delete;

    int VendorId() const { return vendorId; }
    int ProductId() const { return productId; }
    bool Connected() const;

    // A control transfer (setup packet and its data): the bytes transferred.
    // timeoutMs 0: no limit.
    int Control(int requestType, int request, int value, int index, uint8_t* data, int length, int timeoutMs);
    // A bulk or interrupt transfer on an endpoint (0x81: IN 1, 0x02: OUT 2): the bytes transferred
    int Transfer(int endpoint, uint8_t* data, int length, int timeoutMs);

private:
    struct EndpointInfo {
        int interface;
        int attributes;
    };
    void LoadEndpoints();
    void Claim(int interface);

    int fd = -1;
    std::string folder;  // in /sys
    int vendorId = 0, productId = 0;
    std::vector<std::pair<int, EndpointInfo>> endpoints;  // by address
    std::vector<int> claimed;
    std::vector<int> detached;  // taken from their kernel driver
    std::shared_ptr<Watch> watch;
};

class HidDevice {
public:
    HidDevice(const std::string& deviceName, UnpluggedCallback unplugged);
    ~HidDevice();
    HidDevice(const HidDevice&) = delete;
    HidDevice& operator=(const HidDevice&) = delete;

    int VendorId() const { return vendorId; }
    int ProductId() const { return productId; }
    const std::string& Name() const { return name; }
    bool Connected() const;

    std::vector<uint8_t> ReportDescriptor() const;
    // An output report: its first byte is the report ID (0 if the device has none)
    int Write(const uint8_t* data, int length);
    // An input report (the first byte is its report ID if the device numbers
    // its reports): its length, 0 if none came in timeoutMs (-1: no limit)
    int Read(uint8_t* buffer, int size, int timeoutMs);
    // buffer[0]: the report ID; gives the report's length (with its ID)
    int GetFeatureReport(uint8_t* buffer, int size);
    void SendFeatureReport(const uint8_t* data, int length);

private:
    int fd = -1;
    int vendorId = 0, productId = 0;
    std::string name;
    std::shared_ptr<Watch> watch;
};

// A USB printer ("/dev/usb/lp0", the DeviceName of a Printer): the bytes the
// printer understands (ESC/POS, ZPL, PCL, PostScript...) go to it as they are
class Printer {
public:
    // Printer status flags (0: ready)
    enum : int { PaperOut = 0x01, Offline = 0x02, Error = 0x04 };

    Printer(const std::string& deviceName, UnpluggedCallback unplugged);
    ~Printer();
    Printer(const Printer&) = delete;
    Printer& operator=(const Printer&) = delete;

    int VendorId() const { return vendorId; }
    int ProductId() const { return productId; }
    bool Connected() const;

    // The printer's IEEE 1284 Device ID: "MFG:EPSON;CMD:ESC/POS;MDL:TM-T20II;CLS:PRINTER;"
    std::string DeviceId() const;
    // PaperOut, Offline and Error, as the printer reports them
    int Status() const;
    // Sends all the bytes; a TimeoutException if the printer does not take them
    // within timeoutMs (-1: no limit)
    int Write(const uint8_t* data, int length, int timeoutMs);
    // What a printer answers (status requests): its length, 0 if nothing came in timeoutMs
    int Read(uint8_t* buffer, int size, int timeoutMs);

private:
    int fd = -1;
    int vendorId = 0, productId = 0;
    std::shared_ptr<Watch> watch;
};

}  // namespace GHIElectronics::Endpoint::Internal::UsbAccess
