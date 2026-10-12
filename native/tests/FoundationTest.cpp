// Board test of libendpoint's foundation (run as root on the board):
// pin registers, pin setup and give-back, reservations, finding devices,
// system tools and drivers. The expectations are the .NET library's.

#include <cstdio>
#include <filesystem>
#include <functional>
#include <string>

#include "EndpointDevices.h"
#include "Errors.h"
#include "GpioChips.h"
#include "KernelModules.h"
#include "PeripheralPins.h"
#include "PhysicalMemory.h"
#include "PinReservations.h"
#include "Pins.h"
#include "Stm32Mp1Pins.h"
#include "SystemCommands.h"

using namespace GHIElectronics::Endpoint::Internal;

static int failures = 0;

static void Check(bool ok, const std::string& what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what.c_str());
    if (!ok) failures++;
}

static void Throws(ErrorKind kind, const std::function<void()>& action, const std::string& what) {
    try {
        action();
        Check(false, what + ": no error");
    }
    catch (const EndpointError& e) {
        Check(e.Kind() == kind, what + ": " + e.what() + (e.Kind() == kind ? "" : " (wrong kind)"));
    }
    catch (const std::exception& e) {
        Check(false, what + ": not an EndpointError: " + e.what());
    }
}

// Pin numbers as in .NET's EPM815.Gpio.Pin
constexpr int PA0 = 0, PA5 = 5, PD0 = 48, PD1 = 49, PC0 = 32, PZ7 = 151;
constexpr uint64_t GpioA = 0x50002000, GpioD = 0x50005000;

static int ModeOf(uint64_t port, int line) { return (PhysicalMemory::Read(port) >> (line * 2)) & 3; }

static int FunctionOf(uint64_t port, int line) {
    return (PhysicalMemory::Read(port + (line < 8 ? 0x20 : 0x24)) >> ((line % 8) * 4)) & 0xF;
}

int main() {
    std::printf("=== Pins\n");
    Check(Pins::Name(PC0) == "PC0" && Pins::Name(PZ7) == "PZ7" && Pins::Name(200) == "#200", "names PC0, PZ7, #200");
    Check(Pins::ToChip(PC0).port == 2 && Pins::ToChip(PC0).line == 0 && Pins::ToChip(PZ7).port == Stm32Mp1Pins::PortZ,
          "PC0 is port C line 0, PZ7 is port Z");
    Throws(ErrorKind::ArgumentOutOfRange, [] { Pins::Check(152); }, "pin 152");

    std::printf("=== Registers\n");
    auto moder = PhysicalMemory::Read(GpioA);
    Check(moder != 0, "GPIOA MODER reads 0x" + std::to_string(moder));
    Throws(ErrorKind::Argument, [] { PhysicalMemory::Read(GpioA + 2); }, "an unaligned address");

    std::printf("=== Pin setup and give-back\n");
    auto pa0 = Pins::ToChip(PA0);
    auto before = Stm32Mp1Pins::Save(pa0.port, pa0.line);
    Stm32Mp1Pins::SetFunction(pa0.port, pa0.line, 2, Stm32Mp1Pins::Pull::Up, Stm32Mp1Pins::OutputType::OpenDrain,
                              Stm32Mp1Pins::Speed::High);
    auto changed = Stm32Mp1Pins::Save(pa0.port, pa0.line);
    Check(changed.mode == Stm32Mp1Pins::Mode::Alternate && changed.alternate == 2 && changed.pull == Stm32Mp1Pins::Pull::Up &&
              changed.type == Stm32Mp1Pins::OutputType::OpenDrain && changed.speed == Stm32Mp1Pins::Speed::High,
          "PA0 set to AF2, pull-up, open drain, high speed");
    Stm32Mp1Pins::Restore(pa0.port, pa0.line, before);
    Check(Stm32Mp1Pins::Save(pa0.port, pa0.line) == before, "PA0 restored exactly");
    Throws(ErrorKind::ArgumentOutOfRange, [&] { Stm32Mp1Pins::SetAlternate(pa0.port, pa0.line, 16); }, "alternate function 16");

    std::printf("=== Reservations\n");
    PinReservations::Reserve("GPIO", {PA0});
    Throws(ErrorKind::InvalidOperation, [] { PinReservations::Reserve("ADC PA0", {PA5, PA0}); }, "PA0 twice");
    Check(!PinReservations::IsReserved(PA5), "all or none: PA5 was not reserved");
    PinReservations::Release({PA0});
    Check(!PinReservations::IsReserved(PA0), "PA0 released");

    std::printf("=== Peripheral pins\n");
    auto d0 = Stm32Mp1Pins::Save(3, 0);
    auto d1 = Stm32Mp1Pins::Save(3, 1);
    Check(PeripheralPins::Connect("CAN1", {{PD1, 9, Stm32Mp1Pins::Pull::None, Stm32Mp1Pins::OutputType::PushPull,
                                            Stm32Mp1Pins::Speed::Medium},
                                           {PD0, 9}}),
          "CAN1 connects PD1/PD0");
    Check(ModeOf(GpioD, 1) == 2 && FunctionOf(GpioD, 1) == 9 && ModeOf(GpioD, 0) == 2 && FunctionOf(GpioD, 0) == 9,
          "PD1 and PD0 are AF9");
    Check(!PeripheralPins::Connect("CAN1", {{PD1, 9}, {PD0, 9}}), "connecting CAN1 again changes nothing");
    Throws(ErrorKind::InvalidOperation, [] { PeripheralPins::Connect("UART", {{PD0, 8}}); }, "PD0 for another owner");
    PeripheralPins::Disconnect("CAN1");
    Check(Stm32Mp1Pins::Save(3, 0) == d0 && Stm32Mp1Pins::Save(3, 1) == d1 && !PinReservations::IsReserved(PD0),
          "PD0/PD1 back as before and free");
    auto a5 = Stm32Mp1Pins::Save(0, 5);
    Stm32Mp1Pins::SetMode(0, 5, Stm32Mp1Pins::Mode::Input);
    PeripheralPins::Connect("DAC channel 1 (PA5)", {PinFunction::Analog(PA5)});
    Check(ModeOf(GpioA, 5) == 3, "PA5 analog");
    PeripheralPins::Disconnect("DAC channel 1 (PA5)");
    Check(ModeOf(GpioA, 5) == 0, "PA5 an input again");
    Stm32Mp1Pins::Restore(0, 5, a5);

    std::printf("=== Devices\n");
    Check(EndpointDevices::IsAt("net", "can0", 0x4400E000) && !EndpointDevices::IsAt("net", "can0", 0x4400F000),
          "can0 is FDCAN1 (0x4400E000), not FDCAN2");
    auto pwm = EndpointDevices::Find("pwm", "pwmchip", 0x44000000);
    Check(pwm >= 0, "TIM1's PWM chip is pwmchip" + std::to_string(pwm));
    Check(EndpointDevices::Find("pwm", "pwmchip", 0x12345000) == -1, "no PWM chip at 0x12345000");
    auto adc = EndpointDevices::FindIio("48003000.adc:adc@0");
    Check(adc.has_value(), "ADC1 is " + adc.value_or("missing"));
    Check(!EndpointDevices::FindIio("no such device"), "no IIO device named \"no such device\"");

    std::printf("=== System tools and drivers\n");
    SystemCommands::Run("ip", "Cannot list can0", {"link", "show", "can0"});
    Check(true, "ip link show can0");
    Throws(ErrorKind::IO, [] { SystemCommands::Run("ip", "Cannot list can9", {"link", "show", "can9"}); }, "ip on can9");
    Throws(ErrorKind::IO, [] { SystemCommands::Run("no-such-tool", "Cannot run it", {}); }, "a missing tool");
    Throws(ErrorKind::IO, [] { SystemCommands::Run("sleep", "Waiting", {"5"}, 300); }, "a tool that takes too long");
    KernelModules::Load("stm32-dac-core");
    KernelModules::Load("stm32-dac");
    KernelModules::WaitFor("/sys/module/stm32_dac");
    Check(std::filesystem::exists("/sys/module/stm32_dac"), "the DAC driver loaded");
    Throws(ErrorKind::IO, [] { KernelModules::Load("no-such-driver"); }, "a missing driver");

    std::printf("=== GPIO chips\n");
    Check(GpioChips::Find(0) >= 0 && GpioChips::Find(Pins::PortZ) >= 0,
          "GPIOA is gpiochip" + std::to_string(GpioChips::Find(0)) + ", GPIOZ is gpiochip" +
              std::to_string(GpioChips::Find(Pins::PortZ)));

    std::printf("\n%s\n", failures == 0 ? "ALL PASSED" : (std::to_string(failures) + " FAILED").c_str());
    return failures == 0 ? 0 : 1;
}
