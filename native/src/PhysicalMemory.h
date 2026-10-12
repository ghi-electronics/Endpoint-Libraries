#pragma once

#include <cstdint>

namespace GHIElectronics::Endpoint::Internal {

// The processor's registers, through /dev/mem. Each 4 KB page is mapped once
// and kept for the life of the process, and every access is locked, so
// changes from several threads cannot mix.
namespace PhysicalMemory {

uint32_t Read(uint64_t address);
void Write(uint64_t address, uint32_t value);

// Clears then sets bits, as one step for other threads
void Modify(uint64_t address, uint32_t clear, uint32_t set);

}  // namespace PhysicalMemory

}  // namespace GHIElectronics::Endpoint::Internal
