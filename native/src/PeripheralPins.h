#pragma once

#include <string>
#include <vector>

#include "Stm32Mp1Pins.h"

namespace GHIElectronics::Endpoint::Internal {

// One pin of a peripheral (I2C, SPI, PWM, UART...) and how to set it up
struct PinFunction {
    // The alternate value that means analog mode instead of a function
    static constexpr int AnalogFunction = -1;

    int pin;
    int alternate;
    Stm32Mp1Pins::Pull pull = Stm32Mp1Pins::Pull::None;
    Stm32Mp1Pins::OutputType type = Stm32Mp1Pins::OutputType::PushPull;
    Stm32Mp1Pins::Speed speed = Stm32Mp1Pins::Speed::Low;

    // An ADC input or DAC output pin: analog mode, no pull
    static PinFunction Analog(int pin) { return PinFunction{pin, AnalogFunction}; }
};

// Connects pins to a peripheral, and gives them back as they were found. The
// device tree leaves these pins alone (empty pinctrl), so the kernel does not
// change them behind our back. The same as the .NET library's PeripheralPins.
namespace PeripheralPins {

// Reserves the pins for owner ("I2C1", "UART4"...), all or none, and
// connects them. Nothing changes if the owner already has them: then it
// returns false.
bool Connect(const std::string& owner, const std::vector<PinFunction>& pins);

// Puts the owner's pins back as they were and frees them
void Disconnect(const std::string& owner);

bool IsConnected(const std::string& owner);

}  // namespace PeripheralPins

}  // namespace GHIElectronics::Endpoint::Internal
