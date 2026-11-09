// Chip-neutral entry point for UART / serial-port setup. Call once before
// opening a serial port: it muxes the TX/RX pins (plus RTS/CTS when flow
// control is on) and makes /dev/ttyS* usable. Dispatches to the right
// per-chip helper based on the board detected at startup.
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

#include "Core.h"
#include "EPN256.h"
#include "EPN128.h"

namespace GHIElectronics::Endpoint::Core {

struct SerialPortController {
    // Muxes the port's pins. When enableHardwareFlowControl is set, RTS
    // and (where bonded out) CTS are muxed too.
    static void Initialize(const std::string& portName, bool enableHardwareFlowControl = false) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::SerialPort::Initialize(portName, enableHardwareFlowControl); break;
            case Chip::Epn256: EPN256::SerialPort::Initialize(portName, enableHardwareFlowControl); break;
            default:           EPM815::SerialPort::Initialize(portName, enableHardwareFlowControl); break;
        }
    }

    // Releases the port's pins back to free GPIO.
    static void UnInitialize(const std::string& portName) {
        switch (ChipDetect::Current()) {
            case Chip::Epn128: EPN128::SerialPort::UnInitialize(portName); break;
            case Chip::Epn256: EPN256::SerialPort::UnInitialize(portName); break;
            default:           EPM815::SerialPort::UnInitialize(portName); break;
        }
    }
};

}  // namespace GHIElectronics::Endpoint::Core
