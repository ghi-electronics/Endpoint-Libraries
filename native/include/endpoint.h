// GHI Electronics Endpoint for C++: the board's hardware with the same API
// as .NET (System.Device.Gpio, I2c, Spi, Pwm, Analog; System.IO.Ports;
// Iot.Device.SocketCan; .NET IoT's RtcBase; TinyCLR's DAC and watchdog;
// SD cards and USB drives; USB devices; .NET IoT's SoundDevice).
//
//   #include <endpoint.h>
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Gpio;
//
//   GpioController gpio;
//   gpio.OpenPin(EPDomino24::Gpio::Led, PinMode::Output);
//   gpio.Write(EPDomino24::Gpio::Led, PinValue::High);
//
// Link the program with libendpoint.a. Errors are exceptions with the .NET
// names (Core::InvalidOperationException...), which are also the standard
// C++ ones (std::runtime_error...).
#pragma once

#include "endpoint/Adc.h"
#include "endpoint/Camera.h"
#include "endpoint/Can.h"
#include "endpoint/CanController.h"
#include "endpoint/Core.h"
#include "endpoint/Dac.h"
#include "endpoint/Display.h"
#include "endpoint/Drawing.h"
#include "endpoint/Gpio.h"
#include "endpoint/I2c.h"
#include "endpoint/I2cController.h"
#include "endpoint/Network.h"
// Pin constants and bus tables for every Endpoint board (EPM815,
// EPDomino24, EPN256, EPN128). Pulled in by default so sample code can
// write EPN256::StorageController::UsbHostMassStorage without an extra
// include. Mirrors .NET's GHIElectronics.Endpoint.Pins and Python's
// GHIElectronics.Endpoint.Pins.
#include "endpoint/Pins.h"
#include "endpoint/Ports.h"
#include "endpoint/Pwm.h"
#include "endpoint/PwmController.h"
#include "endpoint/Rtc.h"
#include "endpoint/SerialPortController.h"
#include "endpoint/Sound.h"
#include "endpoint/Spi.h"
#include "endpoint/SpiController.h"
#include "endpoint/Storage.h"
#include "endpoint/Touch.h"
#include "endpoint/UI.h"
#include "endpoint/UsbHost.h"
#include "endpoint/Version.h"
#include "endpoint/Watchdog.h"
