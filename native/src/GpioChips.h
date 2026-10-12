#pragma once

namespace GHIElectronics::Endpoint::Internal {

// Which /dev/gpiochipN is which port. The kernel names them GPIOA ... GPIOZ;
// their numbers depend on the order they started in.
namespace GpioChips {

// The gpiochip number of the port (A = 0 ... I = 8, Z = 9)
int Find(int port);

}  // namespace GpioChips

}  // namespace GHIElectronics::Endpoint::Internal
