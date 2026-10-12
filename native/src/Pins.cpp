#include "Pins.h"

#include "Errors.h"
#include "Stm32Mp1Pins.h"

namespace GHIElectronics::Endpoint::Internal::Pins {

bool IsValid(int pin) { return pin >= 0 && pin <= Last; }

int Check(int pin) {
    if (!IsValid(pin)) {
        ThrowArgumentOutOfRange("Not an EPM815 pin: " + std::to_string(pin));
    }
    return pin;
}

std::string Name(int pin) {
    if (!IsValid(pin)) {
        return "#" + std::to_string(pin);
    }
    auto port = pin / 16;
    return std::string("P") + (port == PortZ ? 'Z' : static_cast<char>('A' + port)) + std::to_string(pin % 16);
}

ChipPin ToChip(int pin) {
    Check(pin);
    auto port = pin / 16;
    return ChipPin{port == PortZ ? Stm32Mp1Pins::PortZ : port, pin % 16};
}

}  // namespace GHIElectronics::Endpoint::Internal::Pins
