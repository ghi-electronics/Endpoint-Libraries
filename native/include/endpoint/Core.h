// GHI Electronics Endpoint for C++: the EPM815's pins, buses and ports, and
// the errors the Endpoint classes throw. The same as .NET's
// GHIElectronics.Endpoint.Core:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   EPM815::I2c::Initialize(EPM815::I2c::I2c1);
#pragma once

#include <stdexcept>
#include <string>

#include "detail/CApi.h"

namespace GHIElectronics::Endpoint::Core {

// The errors: the same names as the .NET exceptions, and also the matching
// standard C++ ones (an ArgumentException is a std::invalid_argument)
class ArgumentException : public std::invalid_argument {
public:
    using std::invalid_argument::invalid_argument;
};
class ArgumentOutOfRangeException : public ArgumentException {
public:
    using ArgumentException::ArgumentException;
};
class InvalidOperationException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class IOException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class NotSupportedException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};
class ObjectDisposedException : public InvalidOperationException {
public:
    using InvalidOperationException::InvalidOperationException;
};
class NotImplementedException : public std::logic_error {
public:
    using std::logic_error::logic_error;
};
class TimeoutException : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};

namespace detail {

// Throws the error libendpoint reported (its kind and message)
[[noreturn]] inline void Throw(int kind) {
    std::string message = endpoint_last_error();
    switch (kind) {
        case 1: throw ArgumentException(message);
        case 2: throw ArgumentOutOfRangeException(message);
        case 3: throw InvalidOperationException(message);
        case 5: throw NotSupportedException(message);
        case 6: throw ObjectDisposedException(message);
        case 7: throw NotImplementedException(message);
        case 8: throw TimeoutException(message);
        default: throw IOException(message);
    }
}

inline void Check(int result) {
    if (result != 0) {
        Throw(result);
    }
}

}  // namespace detail

// The EPM815 (STM32MP15x) module
struct EPM815 {
    // GPIO pins, for Devices::Gpio::GpioController
    struct Gpio {
        // The EPM815's pins: port * 16 + line, ports A to I, then Z
        struct Pin {
            static constexpr int PA0 = 0, PA1 = 1, PA2 = 2, PA3 = 3, PA4 = 4, PA5 = 5, PA6 = 6, PA7 = 7;
            static constexpr int PA8 = 8, PA9 = 9, PA10 = 10, PA11 = 11, PA12 = 12, PA13 = 13, PA14 = 14, PA15 = 15;
            static constexpr int PB0 = 16, PB1 = 17, PB2 = 18, PB3 = 19, PB4 = 20, PB5 = 21, PB6 = 22, PB7 = 23;
            static constexpr int PB8 = 24, PB9 = 25, PB10 = 26, PB11 = 27, PB12 = 28, PB13 = 29, PB14 = 30, PB15 = 31;
            static constexpr int PC0 = 32, PC1 = 33, PC2 = 34, PC3 = 35, PC4 = 36, PC5 = 37, PC6 = 38, PC7 = 39;
            static constexpr int PC8 = 40, PC9 = 41, PC10 = 42, PC11 = 43, PC12 = 44, PC13 = 45, PC14 = 46, PC15 = 47;
            static constexpr int PD0 = 48, PD1 = 49, PD2 = 50, PD3 = 51, PD4 = 52, PD5 = 53, PD6 = 54, PD7 = 55;
            static constexpr int PD8 = 56, PD9 = 57, PD10 = 58, PD11 = 59, PD12 = 60, PD13 = 61, PD14 = 62, PD15 = 63;
            static constexpr int PE0 = 64, PE1 = 65, PE2 = 66, PE3 = 67, PE4 = 68, PE5 = 69, PE6 = 70, PE7 = 71;
            static constexpr int PE8 = 72, PE9 = 73, PE10 = 74, PE11 = 75, PE12 = 76, PE13 = 77, PE14 = 78, PE15 = 79;
            static constexpr int PF0 = 80, PF1 = 81, PF2 = 82, PF3 = 83, PF4 = 84, PF5 = 85, PF6 = 86, PF7 = 87;
            static constexpr int PF8 = 88, PF9 = 89, PF10 = 90, PF11 = 91, PF12 = 92, PF13 = 93, PF14 = 94, PF15 = 95;
            static constexpr int PG0 = 96, PG1 = 97, PG2 = 98, PG3 = 99, PG4 = 100, PG5 = 101, PG6 = 102, PG7 = 103;
            static constexpr int PG8 = 104, PG9 = 105, PG10 = 106, PG11 = 107, PG12 = 108, PG13 = 109, PG14 = 110, PG15 = 111;
            static constexpr int PH0 = 112, PH1 = 113, PH2 = 114, PH3 = 115, PH4 = 116, PH5 = 117, PH6 = 118, PH7 = 119;
            static constexpr int PH8 = 120, PH9 = 121, PH10 = 122, PH11 = 123, PH12 = 124, PH13 = 125, PH14 = 126, PH15 = 127;
            static constexpr int PI0 = 128, PI1 = 129, PI2 = 130, PI3 = 131, PI4 = 132, PI5 = 133, PI6 = 134, PI7 = 135;
            static constexpr int PI8 = 136, PI9 = 137, PI10 = 138, PI11 = 139, PI12 = 140, PI13 = 141, PI14 = 142, PI15 = 143;
            static constexpr int PZ0 = 144, PZ1 = 145, PZ2 = 146, PZ3 = 147, PZ4 = 148, PZ5 = 149, PZ6 = 150, PZ7 = 151;
            static constexpr int NONE = -1;  // no pin
        };

        // The pin's name, for example "PC0"
        static std::string Name(int pin) {
            if (pin < 0 || pin > Pin::PZ7) {
                return "#" + std::to_string(pin);
            }
            int port = pin / 16;
            return std::string("P") + (port == 9 ? 'Z' : static_cast<char>('A' + port)) + std::to_string(pin % 16);
        }
    };

    // The I2C buses, for Devices::I2c:
    //   EPM815::I2c::Initialize(EPM815::I2c::I2c1);
    //   auto device = I2cDevice::Create(I2cConnectionSettings(EPM815::I2c::I2c1, 0x50));
    struct I2c {
        static constexpr int I2c1 = 0;  // PD12 (SCL), PD13 (SDA)
        static constexpr int I2c5 = 1;  // PZ4 (SCL), PZ5 (SDA)
        static constexpr int I2c4 = 2;  // PB6 (SCL), PF15 (SDA)
        static constexpr int I2c6 = 3;  // PD1 (SCL), PD0 (SDA)

        static constexpr int DefaultFrequency = 400'000;  // fast mode
        static constexpr int MinFrequency = 10'000;
        static constexpr int MaxFrequency = 1'000'000;    // fast mode plus

        // Connects the bus's pins (open drain, internal pull-ups on) and sets its speed
        static void Initialize(int bus, int frequency = DefaultFrequency) { detail::Check(endpoint_i2c_initialize(bus, frequency)); }
        // Gives the bus's pins back, as they were before Initialize
        static void UnInitialize(int bus) { detail::Check(endpoint_i2c_uninitialize(bus)); }
        // The bus speed in use, in Hz
        static int GetFrequency(int bus) {
            int frequency = 0;
            detail::Check(endpoint_i2c_get_frequency(bus, &frequency));
            return frequency;
        }
    };

    // The SPI buses, for Devices::Spi. Chip select is not driven by the bus: use any GPIO pin.
    struct Spi {
        static constexpr int Spi1 = 0;  // PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI)
        static constexpr int Spi4 = 1;  // PE12 (SCK), PE13 (MISO), PE14 (MOSI)
        static constexpr int Spi5 = 2;  // PF7 (SCK), PF8 (MISO), PF9 (MOSI)

        static constexpr int DefaultBufferSize = 4096;  // the largest transfer when none is given

        // Connects the bus's pins and loads the SPI driver if needed. bufferSize
        // is the largest transfer: the first Initialize after boot decides it.
        static void Initialize(int bus, int bufferSize = DefaultBufferSize) { detail::Check(endpoint_spi_initialize(bus, bufferSize)); }
        static void UnInitialize(int bus) { detail::Check(endpoint_spi_uninitialize(bus)); }
    };

    // PWM pins, for Devices::Pwm::PwmChannel. Channels of one timer share its frequency.
    struct Pwm {
        struct Pin {
            static constexpr int PA8 = 8, PE11 = 75, PA10 = 10, PA11 = 11;  // TIM1 channels 1-4
            static constexpr int PA15 = 15, PB3 = 19, PA3 = 3;              // TIM2 channels 1, 2, 4
            static constexpr int PC6 = 38, PB5 = 21, PB0 = 16;              // TIM3 channels 1-3
            static constexpr int PD12 = 60, PB7 = 23, PD14 = 62, PD15 = 63; // TIM4 channels 1-4
            static constexpr int PA0 = 0, PH11 = 123, PH12 = 124, PI0 = 128;// TIM5 channels 1-4
            static constexpr int PI5 = 133, PI6 = 134, PI7 = 135, PI2 = 130;// TIM8 channels 1-4
            static constexpr int PH6 = 118, PH9 = 121;                      // TIM12 channels 1-2
            static constexpr int PA6 = 6;                                   // TIM13 channel 1
            static constexpr int PF9 = 89;                                  // TIM14 channel 1
            static constexpr int PE5 = 69, PE6 = 70;                        // TIM15 channels 1-2
            static constexpr int PB8 = 24;                                  // TIM16 channel 1
            static constexpr int PB9 = 25;                                  // TIM17 channel 1
        };

        // Connects the pin to its timer
        static void Initialize(int pin) { detail::Check(endpoint_pwm_initialize(pin)); }
        static void UnInitialize(int pin) { detail::Check(endpoint_pwm_uninitialize(pin)); }
        // The PWM chip of the pin's timer, for PwmChannel::Create
        static int GetChipId(int pin) {
            int chip = 0;
            detail::Check(endpoint_pwm_get_chip_id(pin, &chip));
            return chip;
        }
        // The pin's channel in its PWM chip, for PwmChannel::Create
        static int GetChannelId(int pin) {
            int channel = 0;
            detail::Check(endpoint_pwm_get_channel_id(pin, &channel));
            return channel;
        }
    };

    // The serial ports (UARTs), for Devices::Ports::SerialPort
    struct SerialPort {
        static constexpr const char* Uart1 = "/dev/ttySTM0";  // PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS)
        static constexpr const char* Uart2 = "/dev/ttySTM1";  // PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS)
        static constexpr const char* Uart3 = "/dev/ttySTM2";  // PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS)
        static constexpr const char* Uart4 = "/dev/ttySTM3";  // PD1 (TX), PD0 (RX)
        static constexpr const char* Uart5 = "/dev/ttySTM4";  // PB13 (TX), PB12 (RX)
        static constexpr const char* Uart7 = "/dev/ttySTM6";  // PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS)
        static constexpr const char* Uart8 = "/dev/ttySTM7";  // PE1 (TX), PE0 (RX)

        // Connects the port's TX and RX pins, and RTS and CTS for hardware flow control
        static void Initialize(const std::string& portName, bool enableHardwareFlowControl = false) {
            detail::Check(endpoint_serialport_initialize(portName.c_str(), enableHardwareFlowControl ? 1 : 0));
        }
        static void UnInitialize(const std::string& portName) { detail::Check(endpoint_serialport_uninitialize(portName.c_str())); }
    };

    // The CAN ports, for Devices::Can::CanRaw. A port that goes bus-off after
    // too many errors restarts by itself after 100 ms.
    struct Can {
        static constexpr const char* Can1 = "can0";  // FDCAN1: PD1 (TX), PD0 (RX)
        static constexpr const char* Can2 = "can1";  // FDCAN2: PB13 (TX), PB12 (RX)
        static constexpr int MaxBitrate = 1'000'000;

        // Connects the port's pins, sets its bitrate and starts it
        static void Initialize(const std::string& port, int bitrate) { detail::Check(endpoint_can_initialize(port.c_str(), bitrate)); }
        // Stops the port and gives its pins back
        static void UnInitialize(const std::string& port) { detail::Check(endpoint_can_uninitialize(port.c_str())); }
    };

    // The analog inputs, for Devices::Adc::AdcController
    struct Adc {
        struct Pin {
            static constexpr int ANA0 = 0x1000, ANA1 = 0x1001;  // dedicated analog inputs, ADC1 channels 0 and 1
            static constexpr int PF11 = 91, PA6 = 6, PF12 = 92, PB0 = 16, PC0 = 32, PC3 = 35;  // ADC1 channels 2, 3, 6, 9, 10, 13
            static constexpr int PA3 = 3, PA0 = 0, PA4 = 4, PA5 = 5;                          // ADC1 channels 15, 16, 18, 19
            static constexpr int PF13 = 93, PF14 = 94;                                         // ADC2 channels 2 and 6
        };
    };

    // The analog outputs, for Devices::Dac (the same API as TinyCLR)
    struct Dac {
        static constexpr const char* Id = "EPM815.Dac";
        static constexpr int PA4 = 0;  // DAC channel 1
        static constexpr int PA5 = 1;  // DAC channel 2
    };

    // The watchdog, for Devices::Watchdog (the same API as TinyCLR)
    struct Watchdog {
        static constexpr const char* Id = "EPM815.Watchdog";
    };
};

}  // namespace GHIElectronics::Endpoint::Core
