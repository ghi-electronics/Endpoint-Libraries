// GHI Electronics Endpoint pin constants and bus tables for every
// Endpoint board (EPM815, EPDomino24, EPN256, EPN128). One header pulls
// in the four board headers so chip-neutral code can write
//
//   using namespace GHIElectronics::Endpoint::Core;
//   I2cController::Initialize(EPDomino24::I2c::I2c1);
//
// and change the EPDomino24 prefix to EPN256 / EPN128 without touching
// anything else. Mirrors .NET's GHIElectronics.Endpoint.Pins package and
// Python's GHIElectronics.Endpoint.Pins module.
//
// Raspberry Pi Zero 2 W is not an Endpoint board; its pins live under
// GHIElectronics::Endpoint::Drivers::RaspberryPi instead.
#pragma once

#include "EPM815.h"
#include "EPDomino24.h"
#include "EPN256.h"
#include "EPN128.h"
