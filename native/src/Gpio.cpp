#include "Gpio.h"

#include <fcntl.h>
#include <linux/gpio.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/ioctl.h>
#include <unistd.h>

#include <cerrno>
#include <chrono>
#include <cstring>

#include "Errors.h"
#include "GpioChips.h"
#include "PinReservations.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal {

namespace {

constexpr const char* Owner = "GPIO";

bool IsInput(int mode) { return mode != static_cast<int>(PinMode::Output); }

uint64_t ModeFlags(int mode) {
    switch (static_cast<PinMode>(mode)) {
        case PinMode::Output: return GPIO_V2_LINE_FLAG_OUTPUT;
        case PinMode::InputPullUp: return GPIO_V2_LINE_FLAG_INPUT | GPIO_V2_LINE_FLAG_BIAS_PULL_UP;
        case PinMode::InputPullDown: return GPIO_V2_LINE_FLAG_INPUT | GPIO_V2_LINE_FLAG_BIAS_PULL_DOWN;
        case PinMode::Input: return GPIO_V2_LINE_FLAG_INPUT | GPIO_V2_LINE_FLAG_BIAS_DISABLED;
    }
    ThrowArgumentOutOfRange("Unknown pin mode " + std::to_string(mode));
}

uint64_t EdgeFlags(int edges) {
    return ((edges & Rising) ? GPIO_V2_LINE_FLAG_EDGE_RISING : 0) | ((edges & Falling) ? GPIO_V2_LINE_FLAG_EDGE_FALLING : 0);
}

std::string PinName(int pin) { return "Pin " + Pins::Name(pin); }

}  // namespace

GpioControllerImpl::GpioControllerImpl() {
    wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
    if (wake < 0) {
        ThrowSystemError("Cannot create the GPIO controller");
    }
}

GpioControllerImpl::~GpioControllerImpl() {
    {
        std::lock_guard lock(gate);
        stopping = true;
    }
    Wake();
    if (watcher.joinable()) {
        watcher.join();
    }
    std::vector<int> pins;
    for (auto& [pin, line] : lines) {
        close(line.fd);
        pins.push_back(pin);
    }
    PinReservations::Release(pins);
    for (auto& [port, chip] : chips) {
        close(chip);
    }
    close(wake);
}

int GpioControllerImpl::PinCount() const { return Pins::Last + 1; }

GpioControllerImpl::Line& GpioControllerImpl::OpenLine(int pin) {
    auto chip = Pins::ToChip(pin);
    auto port = pin / 16;
    auto found = chips.find(port);
    if (found == chips.end()) {
        auto path = "/dev/gpiochip" + std::to_string(GpioChips::Find(port));
        auto fd = open(path.c_str(), O_RDWR | O_CLOEXEC);
        if (fd < 0) {
            ThrowSystemError("Cannot open " + path);
        }
        found = chips.emplace(port, fd).first;
    }

    gpio_v2_line_request request{};
    request.offsets[0] = chip.line;
    request.num_lines = 1;
    std::strncpy(request.consumer, "endpoint", sizeof request.consumer - 1);
    if (ioctl(found->second, GPIO_V2_GET_LINE_IOCTL, &request) < 0) {
        ThrowSystemError("Cannot open " + PinName(pin) + " (a system driver may use it)");
    }
    return lines[pin] = Line{request.fd, -1, None};
}

void GpioControllerImpl::Configure(int pin, Line& line, int mode, int initialValue) {
    gpio_v2_line_config config{};
    auto edges = IsInput(mode) ? WantedEdges(pin) : None;
    config.flags = ModeFlags(mode) | EdgeFlags(edges);
    if (!IsInput(mode)) {
        // Keep an output's level unless a new one is given (no glitch)
        if (initialValue < 0) {
            initialValue = 0;
            if (line.mode == static_cast<int>(PinMode::Output)) {
                gpio_v2_line_values values{0, 1};
                if (ioctl(line.fd, GPIO_V2_LINE_GET_VALUES_IOCTL, &values) == 0) {
                    initialValue = static_cast<int>(values.bits & 1);
                }
            }
        }
        config.num_attrs = 1;
        config.attrs[0].attr.id = GPIO_V2_LINE_ATTR_ID_OUTPUT_VALUES;
        config.attrs[0].attr.values = initialValue ? 1 : 0;
        config.attrs[0].mask = 1;
    }
    if (ioctl(line.fd, GPIO_V2_LINE_SET_CONFIG_IOCTL, &config) < 0) {
        ThrowSystemError("Cannot set the mode of " + PinName(pin));
    }
    line.mode = mode;
    line.edges = edges;
}

int GpioControllerImpl::WantedEdges(int pin) const {
    int edges = None;
    for (const auto& [id, c] : callbacks) {
        if (c.pin == pin) edges |= c.eventTypes;
    }
    for (const auto* w : waiters) {
        if (w->pin == pin) edges |= w->eventTypes;
    }
    return edges;
}

void GpioControllerImpl::UpdateEdges(int pin) {
    auto found = lines.find(pin);
    if (found == lines.end()) {
        return;
    }
    auto& line = found->second;
    auto wanted = WantedEdges(pin);
    if (wanted == line.edges) {
        return;
    }
    if (wanted != None && !IsInput(line.mode)) {
        ThrowInvalidOperation(PinName(pin) + " must be an input for pin change events");
    }
    Configure(pin, line, line.mode < 0 ? static_cast<int>(PinMode::Input) : line.mode, -1);
    Wake();
}

void GpioControllerImpl::OpenPin(int pin) {
    std::lock_guard lock(gate);
    if (lines.contains(pin)) {
        return;
    }
    PinReservations::Reserve(Owner, {Pins::Check(pin)});
    try {
        OpenLine(pin);
    }
    catch (...) {
        PinReservations::Release({pin});
        throw;
    }
}

void GpioControllerImpl::OpenPin(int pin, PinMode mode) {
    OpenPin(pin);
    SetPinMode(pin, mode);
}

void GpioControllerImpl::OpenPin(int pin, PinMode mode, int initialValue) {
    OpenPin(pin);
    SetPinMode(pin, mode, initialValue);
}

void GpioControllerImpl::ClosePin(int pin) {
    {
        std::lock_guard lock(gate);
        auto found = lines.find(pin);
        if (found == lines.end()) {
            return;
        }
        std::erase_if(callbacks, [pin](const auto& c) { return c.second.pin == pin; });
        close(found->second.fd);
        lines.erase(found);
        PinReservations::Release({pin});
    }
    Wake();
}

bool GpioControllerImpl::IsPinOpen(int pin) {
    std::lock_guard lock(gate);
    return lines.contains(pin);
}

void GpioControllerImpl::SetPinMode(int pin, PinMode mode) { SetPinMode(pin, mode, -1); }

void GpioControllerImpl::SetPinMode(int pin, PinMode mode, int initialValue) {
    std::lock_guard lock(gate);
    auto found = lines.find(pin);
    if (found == lines.end()) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    Configure(pin, found->second, static_cast<int>(mode), initialValue);
    Wake();
}

PinMode GpioControllerImpl::GetPinMode(int pin) {
    std::lock_guard lock(gate);
    auto found = lines.find(pin);
    if (found == lines.end()) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    if (found->second.mode >= 0) {
        return static_cast<PinMode>(found->second.mode);
    }

    // Not set yet: what the line is now
    gpio_v2_line_info info{};
    info.offset = Pins::ToChip(pin).line;
    if (ioctl(chips.at(pin / 16), GPIO_V2_GET_LINEINFO_IOCTL, &info) < 0) {
        ThrowSystemError("Cannot read the mode of " + PinName(pin));
    }
    if (info.flags & GPIO_V2_LINE_FLAG_OUTPUT) return PinMode::Output;
    if (info.flags & GPIO_V2_LINE_FLAG_BIAS_PULL_UP) return PinMode::InputPullUp;
    if (info.flags & GPIO_V2_LINE_FLAG_BIAS_PULL_DOWN) return PinMode::InputPullDown;
    return PinMode::Input;
}

bool GpioControllerImpl::IsPinModeSupported(int pin, PinMode mode) const {
    return Pins::IsValid(pin) && static_cast<int>(mode) >= 0 && static_cast<int>(mode) <= 3;
}

int GpioControllerImpl::Read(int pin) {
    std::lock_guard lock(gate);
    auto found = lines.find(pin);
    if (found == lines.end()) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    gpio_v2_line_values values{0, 1};
    if (ioctl(found->second.fd, GPIO_V2_LINE_GET_VALUES_IOCTL, &values) < 0) {
        ThrowSystemError("Cannot read " + PinName(pin));
    }
    return static_cast<int>(values.bits & 1);
}

void GpioControllerImpl::Write(int pin, int value) {
    std::lock_guard lock(gate);
    auto found = lines.find(pin);
    if (found == lines.end()) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    if (found->second.mode != static_cast<int>(PinMode::Output)) {
        ThrowInvalidOperation(PinName(pin) + " is not an output: set its mode to Output first");
    }
    gpio_v2_line_values values{value ? 1ull : 0ull, 1};
    if (ioctl(found->second.fd, GPIO_V2_LINE_SET_VALUES_IOCTL, &values) < 0) {
        ThrowSystemError("Cannot write " + PinName(pin));
    }
}

WaitForEventResult GpioControllerImpl::WaitForEvent(int pin, int eventTypes, int timeoutMs) {
    if ((eventTypes & (Rising | Falling)) == 0) {
        ThrowArgument("Wait for Rising, Falling or both");
    }
    Waiter waiter{pin, eventTypes & (Rising | Falling)};
    std::unique_lock lock(gate);
    if (!lines.contains(pin)) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    waiters.push_back(&waiter);
    try {
        UpdateEdges(pin);
        if (!watcher.joinable()) {
            watcher = std::thread(&GpioControllerImpl::Watch, this);
        }
    }
    catch (...) {
        std::erase(waiters, &waiter);
        throw;
    }

    auto done = [&] { return waiter.happened != None; };
    if (timeoutMs < 0) {
        eventArrived.wait(lock, done);
    }
    else {
        eventArrived.wait_for(lock, std::chrono::milliseconds(timeoutMs), done);
    }
    std::erase(waiters, &waiter);
    try {
        UpdateEdges(pin);
    }
    catch (...) {
    }
    return WaitForEventResult{waiter.happened, waiter.happened == None};
}

int GpioControllerImpl::AddCallback(int pin, int eventTypes, Callback callback) {
    if ((eventTypes & (Rising | Falling)) == 0) {
        ThrowArgument("Register for Rising, Falling or both");
    }
    std::lock_guard lock(gate);
    if (!lines.contains(pin)) {
        ThrowInvalidOperation(PinName(pin) + " is not open");
    }
    auto id = nextId++;
    callbacks[id] = Registered{pin, eventTypes & (Rising | Falling), std::move(callback)};
    try {
        UpdateEdges(pin);
    }
    catch (...) {
        callbacks.erase(id);
        throw;
    }
    if (!watcher.joinable()) {
        watcher = std::thread(&GpioControllerImpl::Watch, this);
    }
    return id;
}

void GpioControllerImpl::RemoveCallback(int id) {
    std::lock_guard lock(gate);
    auto found = callbacks.find(id);
    if (found == callbacks.end()) {
        return;
    }
    auto pin = found->second.pin;
    callbacks.erase(found);
    UpdateEdges(pin);
}

void GpioControllerImpl::Wake() {
    uint64_t one = 1;
    (void)!write(wake, &one, sizeof one);
}

// Reports pin changes to the callbacks and waiters, on its own thread
void GpioControllerImpl::Watch() {
    while (true) {
        std::vector<pollfd> fds{{wake, POLLIN, 0}};
        {
            std::lock_guard lock(gate);
            if (stopping) {
                return;
            }
            for (const auto& [pin, line] : lines) {
                if (line.edges != None) {
                    fds.push_back({line.fd, POLLIN, 0});
                }
            }
        }

        if (poll(fds.data(), fds.size(), -1) < 0) {
            continue;
        }
        if (fds[0].revents & POLLIN) {
            uint64_t count;
            (void)!read(wake, &count, sizeof count);
        }

        std::vector<std::pair<Callback, std::pair<int, int>>> calls;
        {
            std::lock_guard lock(gate);
            for (size_t i = 1; i < fds.size(); i++) {
                if (!(fds[i].revents & POLLIN)) {
                    continue;
                }
                // The line may have closed meanwhile: only read lines still open
                int pin = -1;
                for (const auto& [p, line] : lines) {
                    if (line.fd == fds[i].fd && line.edges != None) {
                        pin = p;
                    }
                }
                if (pin < 0) {
                    continue;
                }
                gpio_v2_line_event event{};
                if (read(fds[i].fd, &event, sizeof event) != sizeof event) {
                    continue;
                }
                auto change = event.id == GPIO_V2_LINE_EVENT_RISING_EDGE ? Rising : Falling;
                for (const auto& [id, c] : callbacks) {
                    if (c.pin == pin && (c.eventTypes & change)) {
                        calls.push_back({c.callback, {pin, change}});
                    }
                }
                for (auto* w : waiters) {
                    if (w->pin == pin && (w->eventTypes & change) && w->happened == None) {
                        w->happened = change;
                    }
                }
            }
        }
        eventArrived.notify_all();
        for (auto& [callback, args] : calls) {
            callback(args.first, args.second);
        }
    }
}

}  // namespace GHIElectronics::Endpoint::Internal
