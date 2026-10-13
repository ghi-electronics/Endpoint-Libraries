// Board test of the C++ Mouse, Keyboard and Joystick (only <endpoint.h>,
// linked with libendpoint.a), with the virtual devices of
// tests/virtual_input.py: run it at the same time,
//
//     python3 virtual_input.py & ./cpp-input-test
//
// Every event is logged as one line and compared with what the feeder plays.

#include <endpoint.h>

#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

using namespace GHIElectronics::Endpoint::Core;
using namespace GHIElectronics::Endpoint::Devices::UsbHost;
using namespace std::chrono_literals;

static int failures = 0;

static void Check(bool ok, const std::string& what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what.c_str());
    if (!ok) failures++;
}

// The virtual device's event port, by its name ("endpoint feed mouse")
static std::string Find(const std::string& name) {
    std::error_code error;
    for (const auto& entry : std::filesystem::directory_iterator("/sys/class/input", error)) {
        auto folder = entry.path().filename().string();
        if (folder.rfind("event", 0) != 0) continue;
        std::string line;
        std::getline(std::ifstream(entry.path() / "device" / "name"), line);
        if (line == name) return "/dev/input/" + folder;
    }
    return {};
}

static const char* Name(Buttons b) { return b == Buttons::Left ? "Left" : b == Buttons::Right ? "Right" : "Other"; }
static const char* Name(ButtonState s) { return s == ButtonState::Pressed ? "Pressed" : "Released"; }
static const char* Name(Key k) { return k == Key::A ? "A" : k == Key::LeftShift ? "LeftShift" : "Other"; }
static const char* Name(HatSwitchDirection d) { return d == HatSwitchDirection::Right ? "Right" : "Other"; }

int main() {
    std::string mouseName, keyboardName, gamepadName;
    for (int i = 0; i < 100 && (mouseName.empty() || keyboardName.empty() || gamepadName.empty()); i++) {
        mouseName = Find("endpoint feed mouse");
        keyboardName = Find("endpoint feed keyboard");
        gamepadName = Find("endpoint feed gamepad");
        std::this_thread::sleep_for(50ms);
    }
    if (mouseName.empty() || keyboardName.empty() || gamepadName.empty()) {
        std::printf("FAIL  no virtual devices: run python3 virtual_input.py at the same time\n");
        return 1;
    }

    std::mutex mutex;
    std::condition_variable changed;
    std::map<std::string, std::vector<std::string>> log;
    int unplugged = 0;
    auto add = [&](const std::string& device, const std::string& line) {
        std::lock_guard lock(mutex);
        log[device].push_back(line);
        if (line == "Disconnected") unplugged++;
        changed.notify_all();
    };
    auto text = [](const char* format, auto... values) {
        char buffer[160];
        std::snprintf(buffer, sizeof buffer, format, values...);
        return std::string(buffer);
    };

    try {
        Keyboard wrong(mouseName);
        Check(false, "a mouse opened as a Keyboard: no exception");
    }
    catch (const ArgumentException& e) {
        Check(true, std::string("a mouse opened as a Keyboard: ") + e.what());
    }

    auto mouse = std::make_unique<Mouse>(mouseName);
    mouse->SetCursorBounds({0, 0}, {799, 479});
    mouse->CursorMoved += [&](Mouse&, const Mouse::CursorMovedEventArgs& e) {
        add("mouse", text("CursorMoved %d,%d delta %d,%d", e.NewPosition().X, e.NewPosition().Y, e.Delta().X, e.Delta().Y));
    };
    mouse->ButtonChanged += [&](Mouse&, const Mouse::ButtonChangedEventArgs& e) {
        add("mouse", text("ButtonChanged %s %s", Name(e.Which()), Name(e.State())));
    };
    mouse->WheelMoved += [&](Mouse&, const Mouse::WheelMovedEventArgs& e) {
        add("mouse", text("WheelMoved %d delta %d", e.NewPosition(), e.Delta()));
    };
    mouse->Disconnected += [&](BaseDevice&) { add("mouse", "Disconnected"); };
    Check(mouse->VendorId() == 0x1234 && mouse->ProductId() == 0x5678 && mouse->Connected() && mouse->Type() == DeviceType::Mouse,
          "Mouse " + mouse->DeviceName() + ": 1234:5678, Connected");

    auto keyboard = std::make_unique<Keyboard>(keyboardName);
    auto keyLine = [&](const char* what, const Keyboard::KeyboardEventArgs& e) {
        add("keyboard", text("%s %s %d", what, Name(e.Which()), static_cast<int>(e.ASCII())));
    };
    keyboard->KeyDown += [&](Keyboard&, const Keyboard::KeyboardEventArgs& e) { keyLine("KeyDown", e); };
    keyboard->KeyUp += [&](Keyboard&, const Keyboard::KeyboardEventArgs& e) { keyLine("KeyUp", e); };
    keyboard->CharDown += [&](Keyboard&, const Keyboard::KeyboardEventArgs& e) { keyLine("CharDown", e); };
    keyboard->CharUp += [&](Keyboard&, const Keyboard::KeyboardEventArgs& e) { keyLine("CharUp", e); };
    keyboard->Disconnected += [&](BaseDevice&) { add("keyboard", "Disconnected"); };

    auto joystick = std::make_unique<Joystick>(gamepadName);
    joystick->CursorMoved += [&](Joystick&, const Joystick::CursorMovedEventArgs& e) {
        add("gamepad", text("CursorMoved Cursor%d %d,%d delta %d,%d", static_cast<int>(e.Which()) + 1, e.NewPosition().X,
                            e.NewPosition().Y, e.Delta().X, e.Delta().Y));
    };
    joystick->ButtonChanged += [&](Joystick&, const Joystick::ButtonChangedEventArgs& e) {
        add("gamepad", text("ButtonChanged %d %s", e.Which(), Name(e.State())));
    };
    joystick->HatSwitchPressed += [&](Joystick&, const Joystick::HatSwitchPressedEventArgs& e) {
        add("gamepad", text("HatSwitchPressed %s", Name(e.Direction())));
    };
    joystick->Disconnected += [&](BaseDevice&) { add("gamepad", "Disconnected"); };
    Check(joystick->Capabilities() == 0x3F && (joystick->Capabilities() & Joystick::HatSwitch), "Joystick Capabilities 0x3F");

    {
        std::unique_lock lock(mutex);
        Check(changed.wait_for(lock, 15s, [&] { return unplugged == 3; }), "all three unplugged (Disconnected)");
    }
    Check(mouse->CursorPosition() == Position{10, 5} && mouse->WheelPosition() == 1 &&
              mouse->LeftButtonState() == ButtonState::Released, "Mouse state: 10, 5, wheel 1");
    Check(keyboard->IsKeyPressed(Key::A) == KeyState::Released, "IsKeyPressed(A): Released");
    Check(joystick->CursorPosition().X == 512 && joystick->CurrentHatSwitchDirection() == HatSwitchDirection::Right &&
              joystick->GetButtonState(0) == ButtonState::Released, "Joystick state: 512, hat Right");
    Check(!mouse->Connected(), "Connected is false");

    std::map<std::string, std::vector<std::string>> expected = {
        {"mouse", {"CursorMoved 10,5 delta 10,5", "ButtonChanged Left Pressed", "ButtonChanged Left Released", "WheelMoved 1 delta 1", "Disconnected"}},
        {"keyboard", {"KeyDown LeftShift 0", "KeyDown A 65", "CharDown A 65", "KeyUp A 65", "CharUp A 65", "KeyUp LeftShift 0", "Disconnected"}},
        {"gamepad", {"CursorMoved Cursor1 512,-512 delta 1024,0", "ButtonChanged 0 Pressed", "ButtonChanged 0 Released", "HatSwitchPressed Right", "Disconnected"}},
    };
    for (const auto& [device, wanted] : expected) {
        std::string got;
        for (const auto& line : log[device]) got += (got.empty() ? "" : " | ") + line;
        Check(log[device] == wanted, device + ": " + got);
    }
    mouse->Dispose();
    try {
        mouse->CursorPosition();
        Check(false, "CursorPosition after Dispose: no exception");
    }
    catch (const ObjectDisposedException& e) {
        Check(true, std::string("CursorPosition after Dispose: ") + e.what());
    }
    std::printf("\n%s\n", failures == 0 ? "ALL PASSED" : (std::to_string(failures) + " FAILED").c_str());
    return failures == 0 ? 0 : 1;
}
