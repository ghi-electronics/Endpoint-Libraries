#include "Stm32Mp1Pins.h"

#include <string>

#include "Errors.h"
#include "PhysicalMemory.h"

namespace GHIElectronics::Endpoint::Internal::Stm32Mp1Pins {

namespace {

// Reading RCC_MP_AHBxENSETR gives the clocks that are on
constexpr uint64_t RccAhb4Enable = 0x50000A28;  // GPIOA..K clocks, bit = port
constexpr uint64_t RccAhb5Enable = 0x50000210;  // GPIOZ clock, bit 0
constexpr uint64_t GpioABase = 0x50002000;      // GPIOA; B..K follow every 0x1000
constexpr uint64_t GpioZBase = 0x54004000;

constexpr uint64_t Moder = 0x00, OutputTypes = 0x04, Speeds = 0x08, Pulls = 0x0C, AlternateLow = 0x20, AlternateHigh = 0x24;

uint64_t PortBase(int port, int line) {
    if (line < 0 || line > 15) {
        ThrowArgumentOutOfRange("Pins are 0 to 15 in a port");
    }
    if (port != PortZ && (port < 0 || port > 10)) {
        ThrowArgumentOutOfRange("Ports are A to K and Z");
    }

    auto clocks = port == PortZ ? RccAhb5Enable : RccAhb4Enable;
    auto bit = port == PortZ ? 0 : port;
    if ((PhysicalMemory::Read(clocks) & (1u << bit)) == 0) {
        auto name = port == PortZ ? 'Z' : static_cast<char>('A' + port);
        ThrowInvalidOperation(std::string("The clock of GPIO port ") + name +
                              " is off, so its pins cannot be set up: update the board's firmware");
    }
    return port == PortZ ? GpioZBase : GpioABase + static_cast<uint64_t>(port) * 0x1000;
}

}  // namespace

void SetMode(int port, int line, Mode mode) {
    PhysicalMemory::Modify(PortBase(port, line) + Moder, 3u << (line * 2), static_cast<uint32_t>(mode) << (line * 2));
}

void SetPull(int port, int line, Pull pull) {
    PhysicalMemory::Modify(PortBase(port, line) + Pulls, 3u << (line * 2), static_cast<uint32_t>(pull) << (line * 2));
}

void SetSpeed(int port, int line, Speed speed) {
    PhysicalMemory::Modify(PortBase(port, line) + Speeds, 3u << (line * 2), static_cast<uint32_t>(speed) << (line * 2));
}

void SetOutputType(int port, int line, OutputType type) {
    PhysicalMemory::Modify(PortBase(port, line) + OutputTypes, 1u << line, static_cast<uint32_t>(type) << line);
}

void SetAlternate(int port, int line, int function) {
    if (function < 0 || function > 15) {
        ThrowArgumentOutOfRange("Alternate functions are 0 to 15");
    }
    auto reg = line < 8 ? AlternateLow : AlternateHigh;
    auto shift = (line % 8) * 4;
    PhysicalMemory::Modify(PortBase(port, line) + reg, 0xFu << shift, static_cast<uint32_t>(function) << shift);
}

State Save(int port, int line) {
    auto b = PortBase(port, line);
    auto alternate = PhysicalMemory::Read(b + (line < 8 ? AlternateLow : AlternateHigh)) >> ((line % 8) * 4);
    return State{
        static_cast<Mode>((PhysicalMemory::Read(b + Moder) >> (line * 2)) & 3),
        static_cast<int>(alternate & 0xF),
        static_cast<Pull>((PhysicalMemory::Read(b + Pulls) >> (line * 2)) & 3),
        static_cast<OutputType>((PhysicalMemory::Read(b + OutputTypes) >> line) & 1),
        static_cast<Speed>((PhysicalMemory::Read(b + Speeds) >> (line * 2)) & 3),
    };
}

void Restore(int port, int line, const State& state) {
    SetAlternate(port, line, state.alternate);
    SetOutputType(port, line, state.type);
    SetPull(port, line, state.pull);
    SetSpeed(port, line, state.speed);
    SetMode(port, line, state.mode);
}

void SetFunction(int port, int line, int function, Pull pull, OutputType type, Speed speed) {
    SetAlternate(port, line, function);
    SetOutputType(port, line, type);
    SetPull(port, line, pull);
    SetSpeed(port, line, speed);
    SetMode(port, line, Mode::Alternate);
}

}  // namespace GHIElectronics::Endpoint::Internal::Stm32Mp1Pins
