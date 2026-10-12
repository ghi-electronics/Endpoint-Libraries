#include "PhysicalMemory.h"

#include <fcntl.h>
#include <sys/mman.h>

#include <cstdio>
#include <map>
#include <mutex>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::PhysicalMemory {

namespace {

constexpr uint64_t PageSize = 4096;

std::mutex gate;
std::map<uint64_t, volatile uint32_t*> pages;
int memory = -1;

std::string Hex(uint64_t value) {
    char text[24];
    std::snprintf(text, sizeof text, "0x%llX", static_cast<unsigned long long>(value));
    return text;
}

// Caller holds gate
volatile uint32_t* Register(uint64_t address) {
    if ((address & 3) != 0) {
        ThrowArgument("Register address " + Hex(address) + " is not 4-byte aligned");
    }

    auto page = address & ~(PageSize - 1);
    auto found = pages.find(page);
    if (found == pages.end()) {
        if (memory < 0) {
            memory = open("/dev/mem", O_RDWR | O_SYNC | O_CLOEXEC);
            if (memory < 0) {
                ThrowSystemError("Cannot open /dev/mem (the program must run as root)");
            }
        }

        // mmap64 takes a 64-bit offset on 32-bit and 64-bit ARM alike
        auto mapped = mmap64(nullptr, PageSize, PROT_READ | PROT_WRITE, MAP_SHARED, memory, static_cast<off64_t>(page));
        if (mapped == MAP_FAILED) {
            ThrowSystemError("Cannot map the registers at " + Hex(page));
        }
        found = pages.emplace(page, static_cast<volatile uint32_t*>(mapped)).first;
    }

    return found->second + (address - page) / 4;
}

}  // namespace

uint32_t Read(uint64_t address) {
    std::lock_guard lock(gate);
    return *Register(address);
}

void Write(uint64_t address, uint32_t value) {
    std::lock_guard lock(gate);
    *Register(address) = value;
}

void Modify(uint64_t address, uint32_t clear, uint32_t set) {
    std::lock_guard lock(gate);
    auto r = Register(address);
    *r = (*r & ~clear) | set;
}

}  // namespace GHIElectronics::Endpoint::Internal::PhysicalMemory
