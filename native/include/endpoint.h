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
//   gpio.OpenPin(EPM815::Gpio::Pin::PC0, PinMode::Output);
//   gpio.Write(EPM815::Gpio::Pin::PC0, PinValue::High);
//
// Link the program with libendpoint.a. Errors are exceptions with the .NET
// names (Core::InvalidOperationException...), which are also the standard
// C++ ones (std::runtime_error...).
#pragma once

#include "endpoint/Adc.h"
#include "endpoint/Can.h"
#include "endpoint/Core.h"
#include "endpoint/Dac.h"
#include "endpoint/Display.h"
// EPN256 pin names and the pay-for-use SerialPort / Spi / I2c / Pwm / Can /
// Adc / StorageController helpers — pulled in by default so sample code can
// write EPN256::StorageController::UsbHostMassStorage without an extra
// include. (Mirrors the .NET and Python APIs, where EPN256 is reachable
// through the top-level Core import.)
#include "endpoint/EPN256.h"
#include "endpoint/Gpio.h"
#include "endpoint/I2c.h"
#include "endpoint/Media.h"
#include "endpoint/Network.h"
#include "endpoint/Ports.h"
#include "endpoint/Pwm.h"
#include "endpoint/Rtc.h"
#include "endpoint/Spi.h"
#include "endpoint/Storage.h"
#include "endpoint/Touch.h"
#include "endpoint/UI.h"
#include "endpoint/UsbHost.h"
#include "endpoint/Version.h"
#include "endpoint/Watchdog.h"
