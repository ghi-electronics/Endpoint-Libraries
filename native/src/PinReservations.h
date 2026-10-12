#pragma once

#include <initializer_list>
#include <string>
#include <vector>

namespace GHIElectronics::Endpoint::Internal {

// Which pins this program uses, and for what, so two features cannot use
// the same pin by mistake (for example CAN2 and UART5 share PB12/PB13)
namespace PinReservations {

// Reserves the pins for owner, all or none; an InvalidOperation error
// "Pin PA0 is already in use (GPIO)" if one is taken
void Reserve(const std::string& owner, const std::vector<int>& pins);

void Release(const std::vector<int>& pins);

bool IsReserved(int pin);

}  // namespace PinReservations

}  // namespace GHIElectronics::Endpoint::Internal
