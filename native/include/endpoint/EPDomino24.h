// EP-Domino24 Rev B: a board variant of the EPM815 module with a 40-pin
// header, two mikroBUS sockets and a handful of onboard components. Same
// chip and firmware as EPM815; this header is a thin re-naming layer
// that lists only the peripherals and pins actually bonded on the board,
// so application code reads what a user looking at the EP-Domino24
// silkscreen expects. Mirrors .NET's GHIElectronics.Endpoint.Core.EPDomino24
// and Python's EPDomino24 class.
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Gpio;
//
//   GpioController gpio;
//   gpio.OpenPin(EPDomino24::Led, PinMode::Output);
//   gpio.Write(EPDomino24::Led, PinValue::High);

#pragma once

#include "Core.h"

namespace GHIElectronics::Endpoint::Core::EPDomino24 {

// ---------------------------------------------------------------
// Onboard components
// ---------------------------------------------------------------

inline constexpr int Led      = EPM815::Gpio::Pin::PC0;  // User LED on PC0
inline constexpr int Buzzer   = EPM815::Gpio::Pin::PF9;  // Piezo buzzer on PF9 (also PWM-capable via TIM14 CH1)
inline constexpr int ButtonF1 = EPM815::Gpio::Pin::PF3;  // Function button F1 on PF3
inline constexpr int ButtonF2 = EPM815::Gpio::Pin::PH2;  // Function button F2 on PH2
inline constexpr int ButtonF3 = EPM815::Gpio::Pin::PH3;  // Function button F3 on PH3

// ---------------------------------------------------------------
// GPIO pins exposed on EP-Domino24's headers (40-pin header + two
// mikroBUS sockets + the five onboard component pins).
// ---------------------------------------------------------------

struct Gpio {
    // Pins bonded on EP-Domino24. Subset of EPM815::Gpio::Pin.
    struct Pin {
        static constexpr int PA0  = EPM815::Gpio::Pin::PA0;
        static constexpr int PA3  = EPM815::Gpio::Pin::PA3;
        static constexpr int PA4  = EPM815::Gpio::Pin::PA4;
        static constexpr int PA5  = EPM815::Gpio::Pin::PA5;
        static constexpr int PA6  = EPM815::Gpio::Pin::PA6;
        static constexpr int PA10 = EPM815::Gpio::Pin::PA10;
        static constexpr int PA12 = EPM815::Gpio::Pin::PA12;
        static constexpr int PA13 = EPM815::Gpio::Pin::PA13;
        static constexpr int PA14 = EPM815::Gpio::Pin::PA14;
        static constexpr int PA15 = EPM815::Gpio::Pin::PA15;
        static constexpr int PB6  = EPM815::Gpio::Pin::PB6;
        static constexpr int PB9  = EPM815::Gpio::Pin::PB9;
        static constexpr int PB12 = EPM815::Gpio::Pin::PB12;
        static constexpr int PB13 = EPM815::Gpio::Pin::PB13;
        static constexpr int PC0  = EPM815::Gpio::Pin::PC0;
        static constexpr int PC3  = EPM815::Gpio::Pin::PC3;
        static constexpr int PD0  = EPM815::Gpio::Pin::PD0;
        static constexpr int PD1  = EPM815::Gpio::Pin::PD1;
        static constexpr int PD3  = EPM815::Gpio::Pin::PD3;
        static constexpr int PD4  = EPM815::Gpio::Pin::PD4;
        static constexpr int PD6  = EPM815::Gpio::Pin::PD6;
        static constexpr int PD8  = EPM815::Gpio::Pin::PD8;
        static constexpr int PD9  = EPM815::Gpio::Pin::PD9;
        static constexpr int PD11 = EPM815::Gpio::Pin::PD11;
        static constexpr int PD12 = EPM815::Gpio::Pin::PD12;
        static constexpr int PD13 = EPM815::Gpio::Pin::PD13;
        static constexpr int PD15 = EPM815::Gpio::Pin::PD15;
        static constexpr int PE0  = EPM815::Gpio::Pin::PE0;
        static constexpr int PE1  = EPM815::Gpio::Pin::PE1;
        static constexpr int PE4  = EPM815::Gpio::Pin::PE4;
        static constexpr int PE5  = EPM815::Gpio::Pin::PE5;
        static constexpr int PE8  = EPM815::Gpio::Pin::PE8;
        static constexpr int PE9  = EPM815::Gpio::Pin::PE9;
        static constexpr int PE10 = EPM815::Gpio::Pin::PE10;
        static constexpr int PE12 = EPM815::Gpio::Pin::PE12;
        static constexpr int PE13 = EPM815::Gpio::Pin::PE13;
        static constexpr int PE14 = EPM815::Gpio::Pin::PE14;
        static constexpr int PF3  = EPM815::Gpio::Pin::PF3;
        static constexpr int PF4  = EPM815::Gpio::Pin::PF4;
        static constexpr int PF5  = EPM815::Gpio::Pin::PF5;
        static constexpr int PF6  = EPM815::Gpio::Pin::PF6;
        static constexpr int PF8  = EPM815::Gpio::Pin::PF8;
        static constexpr int PF9  = EPM815::Gpio::Pin::PF9;
        static constexpr int PF11 = EPM815::Gpio::Pin::PF11;
        static constexpr int PF12 = EPM815::Gpio::Pin::PF12;
        static constexpr int PF13 = EPM815::Gpio::Pin::PF13;
        static constexpr int PF14 = EPM815::Gpio::Pin::PF14;
        static constexpr int PF15 = EPM815::Gpio::Pin::PF15;
        static constexpr int PG0  = EPM815::Gpio::Pin::PG0;
        static constexpr int PG9  = EPM815::Gpio::Pin::PG9;
        static constexpr int PG10 = EPM815::Gpio::Pin::PG10;
        static constexpr int PH2  = EPM815::Gpio::Pin::PH2;
        static constexpr int PH3  = EPM815::Gpio::Pin::PH3;
        static constexpr int PH9  = EPM815::Gpio::Pin::PH9;
        static constexpr int PH10 = EPM815::Gpio::Pin::PH10;
        static constexpr int PH12 = EPM815::Gpio::Pin::PH12;
        static constexpr int PI4  = EPM815::Gpio::Pin::PI4;
        static constexpr int PZ0  = EPM815::Gpio::Pin::PZ0;
        static constexpr int PZ1  = EPM815::Gpio::Pin::PZ1;
        static constexpr int PZ2  = EPM815::Gpio::Pin::PZ2;
        static constexpr int PZ3  = EPM815::Gpio::Pin::PZ3;
        static constexpr int PZ6  = EPM815::Gpio::Pin::PZ6;
        static constexpr int PZ7  = EPM815::Gpio::Pin::PZ7;
    };
};

// ---------------------------------------------------------------
// Peripherals bonded on EP-Domino24. A mikroBUS slot is just a
// header: the peripheral it maps to is called out in the comment.
// ---------------------------------------------------------------

// I2C buses bonded on EP-Domino24.
struct I2c {
    static constexpr int I2c1 = EPM815::I2c::I2c1;  // PD12 (SCL), PD13 (SDA); 40-pin header
    static constexpr int I2c4 = EPM815::I2c::I2c4;  // PB6 (SCL), PF15 (SDA); mikroBUS #2
    static constexpr int I2c6 = EPM815::I2c::I2c6;  // PD1 (SCL), PD0 (SDA); mikroBUS #1
};

// SPI buses bonded on EP-Domino24.
struct Spi {
    static constexpr int Spi1 = EPM815::Spi::Spi1;  // PZ0 (SCK), PZ1 (MISO), PZ2 (MOSI); mikroBUS #2
    static constexpr int Spi4 = EPM815::Spi::Spi4;  // PE12 (SCK), PE13 (MISO), PE14 (MOSI); mikroBUS #1
};

// Serial ports bonded on EP-Domino24.
struct SerialPort {
    static constexpr const char* Uart1 = EPM815::SerialPort::Uart1;  // PZ7 (TX), PZ6 (RX), PA12 (RTS), PZ3 (CTS); mikroBUS #1 CS/INT/RST
    static constexpr const char* Uart2 = EPM815::SerialPort::Uart2;  // PF5 (TX), PD6 (RX), PD4 (RTS), PD3 (CTS); mikroBUS #1
    static constexpr const char* Uart3 = EPM815::SerialPort::Uart3;  // PD8 (TX), PD9 (RX), PD12 (RTS), PD11 (CTS); mikroBUS #2
    static constexpr const char* Uart4 = EPM815::SerialPort::Uart4;  // PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and CAN1)
    static constexpr const char* Uart5 = EPM815::SerialPort::Uart5;  // PB13 (TX), PB12 (RX); 40-pin header (shares pins with CAN2)
    static constexpr const char* Uart7 = EPM815::SerialPort::Uart7;  // PE8 (TX), PF6 (RX), PE9 (RTS), PE10 (CTS); 40-pin header
    static constexpr const char* Uart8 = EPM815::SerialPort::Uart8;  // PE1 (TX), PE0 (RX); 40-pin header
};

// CAN buses bonded on EP-Domino24.
struct Can {
    static constexpr const char* Can1 = EPM815::Can::Can1;  // FDCAN1: PD1 (TX), PD0 (RX); 40-pin header (shares pins with I2C6 and UART4)
    static constexpr const char* Can2 = EPM815::Can::Can2;  // FDCAN2: PB13 (TX), PB12 (RX); 40-pin header (shares pins with UART5)
};

// PWM-capable pins bonded on EP-Domino24.
struct Pwm {
    // PWM pins. Subset of EPM815::Pwm::Pin.
    struct Pin {
        static constexpr int PA0  = EPM815::Pwm::Pin::PA0;   // TIM5 channel 1 - mikroBUS #2 PWM
        static constexpr int PA3  = EPM815::Pwm::Pin::PA3;   // TIM2 channel 4 - mikroBUS #1 PWM
        static constexpr int PA6  = EPM815::Pwm::Pin::PA6;   // TIM13 channel 1
        static constexpr int PA10 = EPM815::Pwm::Pin::PA10;  // TIM1 channel 3
        static constexpr int PA15 = EPM815::Pwm::Pin::PA15;  // TIM2 channel 1
        static constexpr int PB9  = EPM815::Pwm::Pin::PB9;   // TIM17 channel 1
        static constexpr int PD12 = EPM815::Pwm::Pin::PD12;  // TIM4 channel 1
        static constexpr int PD15 = EPM815::Pwm::Pin::PD15;  // TIM4 channel 4
        static constexpr int PE5  = EPM815::Pwm::Pin::PE5;   // TIM15 channel 1
        static constexpr int PF9  = EPM815::Pwm::Pin::PF9;   // TIM14 channel 1 - the onboard buzzer
        static constexpr int PH9  = EPM815::Pwm::Pin::PH9;   // TIM12 channel 2
        static constexpr int PH12 = EPM815::Pwm::Pin::PH12;  // TIM5 channel 3
    };
};

// Analog-input pins bonded on EP-Domino24.
struct Adc {
    // ADC pins. Subset of EPM815::Adc::Pin.
    struct Pin {
        static constexpr int PA0  = EPM815::Adc::Pin::PA0;   // ADC1 channel 16
        static constexpr int PA3  = EPM815::Adc::Pin::PA3;   // ADC1 channel 15
        static constexpr int PA4  = EPM815::Adc::Pin::PA4;   // ADC1 channel 18 (shares pin with DAC channel 1)
        static constexpr int PA5  = EPM815::Adc::Pin::PA5;   // ADC1 channel 19 (shares pin with DAC channel 2)
        static constexpr int PA6  = EPM815::Adc::Pin::PA6;   // ADC1 channel 3
        static constexpr int PC3  = EPM815::Adc::Pin::PC3;   // ADC1 channel 13 - mikroBUS #1 ADC
        static constexpr int PF11 = EPM815::Adc::Pin::PF11;  // ADC1 channel 2
        static constexpr int PF12 = EPM815::Adc::Pin::PF12;  // ADC1 channel 6
        static constexpr int PF13 = EPM815::Adc::Pin::PF13;  // ADC2 channel 2
        static constexpr int PF14 = EPM815::Adc::Pin::PF14;  // ADC2 channel 6 - mikroBUS #2 ADC
    };
};

// DAC: two analog outputs on PA4 and PA5 (shared with ADC on the same pins).
struct Dac {
    static constexpr const char* Id = EPM815::Dac::Id;
    static constexpr int PA4 = EPM815::Dac::PA4;  // DAC channel 1, on PA4
    static constexpr int PA5 = EPM815::Dac::PA5;  // DAC channel 2, on PA5
};

// Watchdog - same as EPM815::Watchdog.
struct Watchdog {
    static constexpr const char* Id = EPM815::Watchdog::Id;
};

}  // namespace GHIElectronics::Endpoint::Core::EPDomino24
