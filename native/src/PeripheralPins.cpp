#include "PeripheralPins.h"

#include <map>
#include <mutex>

#include <endpoint/Core.h>

#include "Ma35dPins.h"
#include "PinReservations.h"
#include "Stm32Mp1Pins.h"

namespace GHIElectronics::Endpoint::Internal::PeripheralPins {

namespace {

// The two chips pack a pin's state differently, so a snapshot taken on one is
// meaningless on the other. Keep both packings in the saved state and only
// ever read back the one the running chip wrote.
struct Saved {
    int pin;
    int port;
    int line;
    uint32_t packed;
};

bool IsMa35() { return Core::ChipDetect::IsMa35(); }

std::mutex gate;
std::map<std::string, std::vector<Saved>> connected;

Saved Save(int pin) {
    if (IsMa35()) {
        auto chip = Ma35dPins::FromPin(pin);
        return Saved{pin, chip.port, chip.line, Ma35dPins::Save(chip.port, chip.line).packed};
    }
    auto chip = Stm32Mp1Pins::FromPin(pin);
    return Saved{pin, chip.port, chip.line, Stm32Mp1Pins::Save(chip.port, chip.line).packed};
}

void Apply(const Saved& s, const PinFunction& p) {
    if (IsMa35()) {
        // MA35 has no analog mode: ADC pins select via MFP like any other
        // peripheral function, so the board's pin table carries the MFP value
        // and PinFunction::Analog never reaches here on Nuvoton.
        Ma35dPins::SetFunction(s.port, s.line, p.alternate, p.pull, p.type, p.speed);
        return;
    }
    if (p.alternate == PinFunction::AnalogFunction) {
        Stm32Mp1Pins::SetPull(s.port, s.line, Stm32Mp1Pins::Pull::None);
        Stm32Mp1Pins::SetMode(s.port, s.line, Stm32Mp1Pins::Mode::Analog);
        return;
    }
    Stm32Mp1Pins::SetFunction(s.port, s.line, p.alternate, p.pull, p.type, p.speed);
}

void Restore(const std::vector<Saved>& saved) {
    for (const auto& s : saved) {
        if (IsMa35()) {
            Ma35dPins::Restore(s.port, s.line, Ma35dPins::State{s.packed});
        }
        else {
            Stm32Mp1Pins::Restore(s.port, s.line, Stm32Mp1Pins::State{s.packed});
        }
    }
}

std::vector<int> PinsOf(const std::vector<PinFunction>& pins) {
    std::vector<int> numbers;
    for (const auto& p : pins) {
        numbers.push_back(p.pin);
    }
    return numbers;
}

}  // namespace

bool Connect(const std::string& owner, const std::vector<PinFunction>& pins) {
    std::lock_guard lock(gate);
    if (connected.contains(owner)) {
        return false;
    }

    PinReservations::Reserve(owner, PinsOf(pins));
    std::vector<Saved> saved;
    try {
        for (const auto& p : pins) {
            saved.push_back(Save(p.pin));
            Apply(saved.back(), p);
        }
    }
    catch (...) {
        Restore(saved);
        PinReservations::Release(PinsOf(pins));
        throw;
    }
    connected[owner] = std::move(saved);
    return true;
}

void Disconnect(const std::string& owner) {
    std::lock_guard lock(gate);
    auto found = connected.find(owner);
    if (found == connected.end()) {
        return;
    }
    Restore(found->second);
    std::vector<int> pins;
    for (const auto& s : found->second) {
        pins.push_back(s.pin);
    }
    PinReservations::Release(pins);
    connected.erase(found);
}

bool IsConnected(const std::string& owner) {
    std::lock_guard lock(gate);
    return connected.contains(owner);
}

}  // namespace GHIElectronics::Endpoint::Internal::PeripheralPins
