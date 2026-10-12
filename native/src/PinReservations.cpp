#include "PinReservations.h"

#include <map>
#include <mutex>

#include "Errors.h"
#include "Pins.h"

namespace GHIElectronics::Endpoint::Internal::PinReservations {

namespace {
std::mutex gate;
std::map<int, std::string> owners;
}  // namespace

void Reserve(const std::string& owner, const std::vector<int>& pins) {
    std::lock_guard lock(gate);
    for (auto pin : pins) {
        auto current = owners.find(pin);
        if (current != owners.end()) {
            ThrowInvalidOperation("Pin " + Pins::Name(pin) + " is already in use (" + current->second + ")");
        }
    }
    for (auto pin : pins) {
        owners[pin] = owner;
    }
}

void Release(const std::vector<int>& pins) {
    std::lock_guard lock(gate);
    for (auto pin : pins) {
        owners.erase(pin);
    }
}

bool IsReserved(int pin) {
    std::lock_guard lock(gate);
    return owners.contains(pin);
}

}  // namespace GHIElectronics::Endpoint::Internal::PinReservations
