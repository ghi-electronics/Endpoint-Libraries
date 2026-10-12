#pragma once

#include <condition_variable>
#include <functional>
#include <map>
#include <mutex>
#include <thread>
#include <vector>

namespace GHIElectronics::Endpoint::Internal {

// The same values as .NET's System.Device.Gpio enums
enum class PinMode : int { Input = 0, Output = 1, InputPullDown = 2, InputPullUp = 3 };
enum PinEventTypes : int { None = 0, Rising = 1, Falling = 2 };

struct WaitForEventResult {
    int eventTypes;  // PinEventTypes of the event, None if timed out
    bool timedOut;
};

// GPIO as .NET's GpioController with the EPM815 driver: the EPM815's pin
// numbers on every port, pins reserved while open (owner "GPIO"), and every
// pin mode, pull-up/down and pin change event. The pins go through the
// system's GPIO driver (the kernel's GPIO character devices, uAPI v2).
class GpioControllerImpl {
public:
    using Callback = std::function<void(int pin, int changeType)>;

    GpioControllerImpl();
    ~GpioControllerImpl();

    GpioControllerImpl(const GpioControllerImpl&) = delete;
    GpioControllerImpl& operator=(const GpioControllerImpl&) = delete;

    int PinCount() const;

    void OpenPin(int pin);
    void OpenPin(int pin, PinMode mode);
    void OpenPin(int pin, PinMode mode, int initialValue);
    void ClosePin(int pin);
    bool IsPinOpen(int pin);

    void SetPinMode(int pin, PinMode mode);
    void SetPinMode(int pin, PinMode mode, int initialValue);
    PinMode GetPinMode(int pin);
    bool IsPinModeSupported(int pin, PinMode mode) const;

    int Read(int pin);
    void Write(int pin, int value);

    WaitForEventResult WaitForEvent(int pin, int eventTypes, int timeoutMs);

    // Returns an id for RemoveCallback
    int AddCallback(int pin, int eventTypes, Callback callback);
    void RemoveCallback(int id);

private:
    struct Line {
        int fd = -1;
        int mode = -1;   // a PinMode, or -1 before SetPinMode
        int edges = 0;   // PinEventTypes the line reports now
    };

    struct Registered {
        int pin;
        int eventTypes;
        Callback callback;
    };

    struct Waiter {
        int pin;
        int eventTypes;
        int happened = None;
    };

    Line& OpenLine(int pin);  // gate held
    void Configure(int pin, Line& line, int mode, int initialValue);  // gate held
    void UpdateEdges(int pin);  // gate held
    int WantedEdges(int pin) const;  // gate held
    void Wake();
    void Watch();

    mutable std::mutex gate;
    std::condition_variable eventArrived;
    std::map<int, int> chips;   // port -> chip file
    std::map<int, Line> lines;  // pin -> line
    std::map<int, Registered> callbacks;
    std::vector<Waiter*> waiters;
    int nextId = 1;
    int wake = -1;  // eventfd that interrupts the watcher
    bool stopping = false;
    std::thread watcher;
};

}  // namespace GHIElectronics::Endpoint::Internal
