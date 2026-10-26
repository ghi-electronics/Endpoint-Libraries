#include "Pins.h"

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Pins {

bool IsValid(int pin) { return pin >= 0 && pin <= Last; }

int Check(int pin) {
    if (!IsValid(pin)) {
        ThrowArgumentOutOfRange("Not an EPN256 pin: " + std::to_string(pin));
    }
    return pin;
}

std::string Name(int pin) {
    if (!IsValid(pin)) {
        return "#" + std::to_string(pin);
    }
    auto port = pin / 16;
    return std::string("P") + static_cast<char>('A' + port) + std::to_string(pin % 16);
}

ChipPin ToChip(int pin) {
    Check(pin);
    return ChipPin{pin / 16, pin % 16};
}

}  // namespace GHIElectronics::Endpoint::Internal::Pins
