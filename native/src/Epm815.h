#pragma once

#include <string>

namespace GHIElectronics::Endpoint::Internal {

// The EPM815's buses and ports, the same as the .NET library's EPM815 class:
// Initialize connects the pins (and sets the speed); then the standard APIs
// use the bus. The constants are the system's bus numbers and port names,
// fixed by the firmware.
namespace Epm815 {

namespace I2c {
constexpr int I2c1 = 0, I2c5 = 1, I2c4 = 2, I2c6 = 3;
constexpr int DefaultFrequency = 400'000, MinFrequency = 10'000, MaxFrequency = 1'000'000;
void Initialize(int bus, int frequency = DefaultFrequency);
void UnInitialize(int bus);
int GetFrequency(int bus);
}  // namespace I2c

namespace Spi {
constexpr int Spi1 = 0, Spi4 = 1, Spi5 = 2;
constexpr int DefaultBufferSize = 4096;
void Initialize(int bus, int bufferSize = DefaultBufferSize);
void UnInitialize(int bus);
}  // namespace Spi

namespace Pwm {
void Initialize(int pin);
void UnInitialize(int pin);
int GetChipId(int pin);
int GetChannelId(int pin);
}  // namespace Pwm

namespace SerialPort {
// Uart1 = "/dev/ttySTM0" ... Uart8 = "/dev/ttySTM7" (no UART6)
void Initialize(const std::string& portName, bool enableHardwareFlowControl = false);
void UnInitialize(const std::string& portName);
}  // namespace SerialPort

namespace Can {
// Can1 = "can0", Can2 = "can1"
constexpr int MaxBitrate = 1'000'000;
void Initialize(const std::string& port, int bitrate);
void UnInitialize(const std::string& port);
}  // namespace Can

}  // namespace Epm815

}  // namespace GHIElectronics::Endpoint::Internal
