// Chip-neutral entry point for UART / serial-port setup. Call once before
// opening a serial port: it muxes the TX/RX pins (plus RTS/CTS when flow
// control is on) and makes /dev/ttyS* usable. Dispatches via
// BoardSupport::Current().
//
//   using namespace GHIElectronics::Endpoint::Core;
//   SerialPortController::Initialize(EPN256::SerialPort::Uart12);
//   int fd = open(EPN256::SerialPort::Uart12, O_RDWR | O_NOCTTY);
//   // ... read / write ...
//   SerialPortController::UnInitialize(EPN256::SerialPort::Uart12);
//
// Mirrors .NET's GHIElectronics.Endpoint.Core.SerialPortController.
#pragma once

#include <string>

#include "BoardSupport.h"

namespace GHIElectronics::Endpoint::Core {

struct SerialPortController {
    static void Initialize(const std::string& portName, bool enableHardwareFlowControl = false) {
        BoardSupport::Current().SerialPortInitialize(portName, enableHardwareFlowControl);
    }

    static void UnInitialize(const std::string& portName) {
        BoardSupport::Current().SerialPortUnInitialize(portName);
    }
};

}  // namespace GHIElectronics::Endpoint::Core
