// Board test of the public C++ API, written as a customer would use it: only
// <endpoint.h>, linked with libendpoint.a. Run as root on the board. Needs
// nothing connected (as tests/*.py): PF13 follows its pull, the DAC is
// measured by the ADC on the same pins, CAN runs in the controller's
// loopback mode, and the watchdog never runs out.

#include <endpoint.h>

#include <array>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <functional>
#include <mutex>
#include <string>
#include <thread>
#include <type_traits>
#include <vector>

using namespace GHIElectronics::Endpoint::Core;
using namespace GHIElectronics::Endpoint::Devices;
using namespace std::chrono_literals;

static int failures = 0;

static void Check(bool ok, const std::string& what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what.c_str());
    if (!ok) failures++;
}

template <typename E>
static void Throws(const std::function<void()>& action, const std::string& what) {
    try {
        action();
        Check(false, what + ": no exception");
    }
    catch (const E& e) {
        Check(true, what + ": " + e.what());
    }
    catch (const std::exception& e) {
        Check(false, what + ": wrong exception: " + e.what());
    }
}

static double Measure(int channel) {  // ADC1 channel, read directly (the DAC owns the pin)
    std::string folder;
    for (int i = 0; i < 8; i++) {
        std::ifstream name("/sys/bus/iio/devices/iio:device" + std::to_string(i) + "/name");
        std::string text;
        if (name >> text && text == "48003000.adc:adc@0") folder = "/sys/bus/iio/devices/iio:device" + std::to_string(i);
    }
    double scale = 0, raw = 0;
    std::ifstream(folder + "/in_voltage_scale") >> scale;
    std::ifstream(folder + "/in_voltage" + std::to_string(channel) + "_raw") >> raw;
    return raw * scale / 1000;
}

int main() {
    using Pin = EPM815::Gpio::Pin;

    std::printf("=== GPIO\n");
    {
        using namespace Gpio;
        GpioController gpio;
        Check(gpio.PinCount() == 152, "PinCount 152");
        auto out = gpio.OpenPin(Pin::PF14, PinMode::Output, PinValue::High);
        Check(gpio.Read(Pin::PF14) == PinValue::High, "PF14 opened as an output, High");
        out.Write(PinValue::Low);
        Check(out.Read() == PinValue::Low, "GpioPin Write/Read Low");
        gpio.Toggle(Pin::PF14);
        Check(gpio.Read(Pin::PF14) == PinValue::High, "Toggle gives High");
        gpio.OpenPin(Pin::PF13, PinMode::InputPullDown);
        std::this_thread::sleep_for(10ms);
        Check(gpio.Read(Pin::PF13) == PinValue::Low, "PF13 with pull-down reads Low");
        Throws<InvalidOperationException>([&] { gpio.Write(Pin::PF13, PinValue::High); }, "Write to an input");
        Throws<std::invalid_argument>([&] { gpio.OpenPin(200); }, "pin 200 (also a std::invalid_argument)");

        std::vector<std::pair<int, PinEventTypes>> changes;
        auto id = gpio.RegisterCallbackForPinValueChangedEvent(
            Pin::PF13, PinEventTypes::Rising | PinEventTypes::Falling,
            [&](GpioController&, const PinValueChangedEventArgs& e) { changes.push_back({e.PinNumber, e.ChangeType}); });
        gpio.SetPinMode(Pin::PF13, PinMode::InputPullUp);
        std::this_thread::sleep_for(100ms);
        gpio.SetPinMode(Pin::PF13, PinMode::InputPullDown);
        std::this_thread::sleep_for(100ms);
        Check(changes.size() >= 2 && changes[0].second == PinEventTypes::Rising && changes[1].second == PinEventTypes::Falling,
              "callback: Rising, then Falling (" + std::to_string(changes.size()) + " calls)");
        gpio.UnregisterCallbackForPinValueChangedEvent(Pin::PF13, id);
        std::thread later([&] { std::this_thread::sleep_for(200ms); gpio.SetPinMode(Pin::PF13, PinMode::InputPullUp); });
        auto result = gpio.WaitForEvent(Pin::PF13, PinEventTypes::Rising, 2s);
        later.join();
        Check(result.EventTypes == PinEventTypes::Rising && !result.TimedOut, "WaitForEvent gets Rising");
        Check(gpio.WaitForEvent(Pin::PF13, PinEventTypes::Falling, 300ms).TimedOut, "WaitForEvent times out");
    }

    std::printf("=== ADC and DAC\n");
    {
        Adc::AdcController adc;
        Check(std::abs(adc.VoltageReference().Volts() - 3.3) < 0.01 && adc.PinCount() == 14, "VoltageReference 3.3 V, 14 inputs");
        {
            auto pa0 = adc.OpenPin(EPM815::Adc::Pin::PA0);
            auto volts = pa0.ReadVoltage();
            Check(pa0.AdcResolutionBits() == 16 && volts.Volts() >= 0 && volts.Volts() <= 3.31,
                  "PA0 reads " + std::to_string(volts.Millivolts()) + " mV");
            Throws<InvalidOperationException>([&] { adc.OpenPin(EPM815::Adc::Pin::PA0); }, "PA0 twice");
        }
        Throws<NotSupportedException>([&] { adc.OpenPin(Pin::PB1); }, "PB1");

        auto dac = Dac::DacController::FromName(EPM815::Dac::Id);
        auto pa4 = dac.OpenChannel(EPM815::Dac::PA4);
        auto pa5 = dac.OpenChannel(EPM815::Dac::PA5);
        pa4.WriteValue(0.25);  // a double: part of the range
        pa5.WriteValue(3072);  // an int: raw
        std::this_thread::sleep_for(10ms);
        Check(std::abs(Measure(18) - 0.825) < 0.1 && std::abs(Measure(19) - 2.475) < 0.1,
              "PA4 = 0.25 measures " + std::to_string(Measure(18)) + " V, PA5 = 3072 measures " + std::to_string(Measure(19)) + " V");
        Throws<ArgumentOutOfRangeException>([&] { pa4.WriteValue(5000); }, "5000");
        Throws<ArgumentException>([] { Dac::DacController::FromName("DAC9"); }, "an unknown DAC name");
    }

    std::printf("=== PWM, SPI, I2C\n");
    {
        EPM815::Pwm::Initialize(EPM815::Pwm::Pin::PA0);
        auto pwm = Pwm::PwmChannel::Create(EPM815::Pwm::GetChipId(EPM815::Pwm::Pin::PA0),
                                           EPM815::Pwm::GetChannelId(EPM815::Pwm::Pin::PA0), 1000, 0.25);
        pwm.Start();
        pwm.DutyCycle(0.75);
        pwm.Frequency(2000);
        Check(pwm.Frequency() == 2000 && pwm.DutyCycle() == 0.75, "PwmChannel Frequency(2000), DutyCycle(0.75)");
        pwm.Dispose();
        EPM815::Pwm::UnInitialize(EPM815::Pwm::Pin::PA0);

        EPM815::Spi::Initialize(EPM815::Spi::Spi4);
        Spi::SpiConnectionSettings settings(EPM815::Spi::Spi4);
        settings.Mode = Spi::SpiMode::Mode3;
        auto spi = Spi::SpiDevice::Create(settings);
        std::array<uint8_t, 4> received{};
        spi.TransferFullDuplex(std::array<uint8_t, 4>{1, 2, 3, 4}, received);
        Check(true, "SPI4 TransferFullDuplex");
        Throws<ArgumentException>([&] { spi.TransferFullDuplex(std::array<uint8_t, 4>{}, std::span<uint8_t>(received.data(), 3)); },
                                  "different lengths");
        spi.Dispose();
        Throws<ObjectDisposedException>([&] { spi.WriteByte(1); }, "used after Dispose");
        EPM815::Spi::UnInitialize(EPM815::Spi::Spi4);

        EPM815::I2c::Initialize(EPM815::I2c::I2c1);
        auto device = I2c::I2cDevice::Create(I2c::I2cConnectionSettings(EPM815::I2c::I2c1, 0x50));
        Throws<IOException>([&] { device.WriteByte(0); }, "no device at 0x50");
        EPM815::I2c::UnInitialize(EPM815::I2c::I2c1);
    }

    std::printf("=== Serial port (nothing connected)\n");
    {
        EPM815::SerialPort::Initialize(EPM815::SerialPort::Uart4);
        Ports::SerialPort port(EPM815::SerialPort::Uart4, 115200);
        Throws<InvalidOperationException>([&] { port.ReadByte(); }, "read before Open");
        port.Open();
        Check(port.IsOpen() && port.BytesToRead() == 0, "Open at 115200, nothing to read");
        port.WriteLine("Hello from C++");
        port.ReadTimeout(200);
        auto start = std::chrono::steady_clock::now();
        Throws<TimeoutException>([&] { port.ReadLine(); }, "ReadLine with nothing connected");
        auto waited = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();
        Check(waited >= 190 && waited < 400, "it waited ReadTimeout (" + std::to_string(waited) + " ms)");
        Throws<InvalidOperationException>([&] { port.Parity(Ports::Parity::Even); }, "Parity while open");
        port.BaudRate(9600);
        Check(port.BaudRate() == 9600, "BaudRate changed while open");
        port.Close();
        EPM815::SerialPort::UnInitialize(EPM815::SerialPort::Uart4);
        Ports::SerialPort missing("/dev/ttyNONE");
        Throws<IOException>([&] { missing.Open(); }, "a port that does not exist");
    }

    std::printf("=== CAN in the controller's loopback mode\n");
    {
        EPM815::Can::Initialize(EPM815::Can::Can1, 500'000);
        std::system("ip link set can0 down && ip link set can0 type can bitrate 500000 loopback on && ip link set can0 up");
        Can::CanRaw receiver(EPM815::Can::Can1);
        Can::CanRaw sender(EPM815::Can::Can1);
        sender.WriteFrame(std::array<uint8_t, 3>{1, 2, 3}, Can::CanId{.Standard = 0x123});
        std::array<uint8_t, 8> data{};
        int length = 0;
        Can::CanId id;
        Check(receiver.TryReadFrame(data, length, id) && id.Standard == 0x123 && !id.ExtendedFrameFormat && length == 3 &&
                  data[2] == 3,
              "a standard frame 0x123 [1 2 3]");
        sender.WriteFrame(std::array<uint8_t, 1>{9}, Can::CanId{.Extended = 0x1ABCDE01, .ExtendedFrameFormat = true});
        do {
            receiver.TryReadFrame(data, length, id);
        } while (!id.ExtendedFrameFormat);
        Check(id.Extended == 0x1ABCDE01 && data[0] == 9, "an extended frame 0x1ABCDE01");
        Throws<ArgumentException>([&] { sender.WriteFrame(std::array<uint8_t, 9>{}, Can::CanId{.Standard = 1}); }, "9 bytes");
        EPM815::Can::UnInitialize(EPM815::Can::Can1);
        std::system("ip link set can0 type can bitrate 500000 loopback off");
    }

    std::printf("=== Watchdog and RTC\n");
    {
        auto watchdog = Watchdog::WatchdogController::FromName(EPM815::Watchdog::Id);
        Check(watchdog.MaxTimeout() == 131s, "MaxTimeout 131 s");
        watchdog.Enable(3s);
        for (int i = 0; i < 4; i++) {
            std::this_thread::sleep_for(1s);
            watchdog.Reset();
        }
        watchdog.Disable();
        Check(!watchdog.IsEnabled(), "enabled 3 s, reset for 4 s, disabled");

        Rtc::RtcController rtc;
        auto now = std::chrono::system_clock::now();
        rtc.DateTime(now);
        auto read = rtc.DateTime();
        Check(std::chrono::abs(read - now) < 2s && rtc.IsValid(), "DateTime set and read back, IsValid");
        rtc.SetChargeMode(Rtc::BatteryChargeMode::None);
        Throws<NotImplementedException>([&] { rtc.BackupMemorySize(); }, "BackupMemorySize (TODO)");
    }

    std::printf("=== Storage (with a USB drive plugged in, it is mounted and written too)\n");
    {
        Throws<ArgumentException>([] { Storage::StorageController::FromName("EPM815.StorageController.Floppy"); },
                                  "an unknown storage controller");
        Throws<InvalidOperationException>([] { Storage::FileSystem::Mount(12345); }, "a handle that is not a drive's");

        auto usb = Storage::StorageController::FromName(EPM815::StorageController::UsbHostMassStorage);
        std::mutex lock;
        std::vector<intptr_t> connected;
        int calls = 0;
        int first = (usb.OnConnectionChangedEvent += [&](Storage::StorageController& sender, const Storage::DeviceConnectionEventArgs& e) {
            std::lock_guard guard(lock);
            calls++;
            if (e.DeviceStatus() == Storage::DeviceConnectionStatus::Connected && &sender == &usb) {
                connected.push_back(e.Hdc());
            }
        });
        int second = (usb.OnConnectionChangedEvent += [&](Storage::StorageController&, const Storage::DeviceConnectionEventArgs&) {
            std::lock_guard guard(lock);
            calls++;
        });
        usb.OnConnectionChangedEvent -= second;
        Check(first != second, "+= gives an id for -=");
        usb.Enable();
        std::this_thread::sleep_for(1s);

        std::vector<intptr_t> drives;
        {
            std::lock_guard guard(lock);
            drives = connected;
            Check(calls == static_cast<int>(connected.size()), "a handler removed with -= is not called");
        }
        if (drives.empty()) {
            std::printf("      (no USB drive plugged in: nothing mounted)\n");
        }
        for (auto hdc : drives) {
            auto drive = Storage::FileSystem::Mount(hdc);
            Check(drive.Name().starts_with("/media/usb") && drive.Name().ends_with("/") && drive.IsReady(),
                  "mounted on " + drive.Name() + " (" + drive.DriveFormat() + ", " +
                      std::to_string(drive.AvailableFreeSpace() >> 20) + " MB free)");
            Check(Storage::FileSystem::Mount(hdc).Name() == drive.Name(), "a second Mount gives the same folder");
            std::string text = "Written by the Endpoint C++ test";
            std::ofstream(drive.RootDirectory() / "endpoint-cpp-test.txt") << text;
            Storage::FileSystem::Flush(hdc);
            std::string back;
            std::getline(std::ifstream(drive.RootDirectory() / "endpoint-cpp-test.txt"), back);
            Check(back == text, "written, flushed and read back");
            Check(drive.TotalSize() > 0 && drive.DriveType() == Storage::DriveType::Fixed, "TotalSize and DriveType");
        }
        Storage::DriveInfo data("/data");
        Check(data.DriveFormat() == "ext4" && data.IsReady() && data.TotalSize() > 0, "DriveInfo(\"/data\"): ext4");
        Check(!Storage::DriveInfo("/no/such/folder").IsReady(), "a folder that does not exist is not ready");
        usb.Dispose();
        Throws<ObjectDisposedException>([&] { usb.Enable(); }, "Enable after Dispose");
        // Core's status is the one Storage uses
        static_assert(std::is_same_v<Storage::DeviceConnectionStatus, DeviceConnectionStatus>);
    }

    std::printf("=== USB host (lists what is plugged in)\n");
    {
        using namespace UsbHost;
        auto host = UsbHostController::GetDefault();
        std::mutex mutex;
        std::vector<UsbDeviceConnectionEventArgs> things;
        host.OnConnectionChangedEvent += [&](UsbHostController&, const UsbDeviceConnectionEventArgs& e) {
            std::lock_guard lock(mutex);
            things.push_back(e);
        };
        host.Enable();
        std::this_thread::sleep_for(1s);
        std::lock_guard lock(mutex);
        bool allConnected = !things.empty(), networksThere = true;
        for (const auto& e : things) {
            std::printf("      #%u type %d %-22s %04X:%04X '%s' '%s'\n", e.Id(), static_cast<int>(e.Type()),
                        e.DeviceName().c_str(), e.VendorId(), e.ProductId(), e.Manufacturer().c_str(),
                        e.Product().c_str());
            allConnected = allConnected && e.DeviceStatus() == DeviceConnectionStatus::Connected && e.Id() > 0;
            if (e.Type() == DeviceType::Network) {
                networksThere = networksThere && std::filesystem::exists("/sys/class/net/" + e.DeviceName());
            }
        }
        Check(allConnected, std::to_string(things.size()) + " USB things connected, each with an Id");
        Check(networksThere, "network adapters are interfaces");
        host.Dispose();
        Throws<ObjectDisposedException>([&] { host.Enable(); }, "Enable after Dispose");
    }

    std::printf("\n%s\n", failures == 0 ? "ALL PASSED" : (std::to_string(failures) + " FAILED").c_str());
    return failures == 0 ? 0 : 1;
}
