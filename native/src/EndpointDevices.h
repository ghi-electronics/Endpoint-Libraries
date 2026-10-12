#pragma once

#include <cstdint>
#include <optional>
#include <string>

namespace GHIElectronics::Endpoint::Internal {

// Finds the system's device for a peripheral by the hardware it belongs to:
// numbers like pwmchip12 or i2c-1 depend on the order the drivers started in.
// The same as the .NET library's EndpointDevices.
namespace EndpointDevices {

// Whether /sys/class/<cls>/<name> belongs to the peripheral at address
// (for example "i2c-0" and 0x40012000)
bool IsAt(const std::string& cls, const std::string& name, uint64_t address);

// The number N of /sys/class/<cls>/<prefix>N that belongs to the
// peripheral at address, or -1
int Find(const std::string& cls, const std::string& prefix, uint64_t address);

// The folder of the IIO device (ADC, DAC...) named name, for example
// /sys/bus/iio/devices/iio:device0 for "48003000.adc:adc@0"
std::optional<std::string> FindIio(const std::string& name);

}  // namespace EndpointDevices

}  // namespace GHIElectronics::Endpoint::Internal
