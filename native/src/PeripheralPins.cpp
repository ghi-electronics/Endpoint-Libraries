#include "PeripheralPins.h"

#include <map>
#include <mutex>

#include "PinReservations.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal::PeripheralPins {

namespace {

struct Saved {
    int pin;
    Pins::ChipPin chip;
    Stm32Mp1Pins::State state;
};

std::mutex gate;
std::map<std::string, std::vector<Saved>> connected;

void Restore(const std::vector<Saved>& saved) {
    for (const auto& s : saved) {
        Stm32Mp1Pins::Restore(s.chip.port, s.chip.line, s.state);
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
            auto chip = Pins::ToChip(p.pin);
            saved.push_back(Saved{p.pin, chip, Stm32Mp1Pins::Save(chip.port, chip.line)});
            if (p.alternate == PinFunction::AnalogFunction) {
                Stm32Mp1Pins::SetPull(chip.port, chip.line, Stm32Mp1Pins::Pull::None);
                Stm32Mp1Pins::SetMode(chip.port, chip.line, Stm32Mp1Pins::Mode::Analog);
            }
            else {
                Stm32Mp1Pins::SetFunction(chip.port, chip.line, p.alternate, p.pull, p.type, p.speed);
            }
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
