// Board-support registry for libendpoint. Mirrors .NET's
// GHIElectronics.Endpoint.Core.BoardSupport / IBoardSupport and Python's
// GHIElectronics.Endpoint.Core._board_support.BoardSupport. The chip-neutral
// controllers (I2cController, SpiController, PwmController, CanController,
// SerialPortController) dispatch through BoardSupport::Current() so the
// same user code works on every board.
//
// libendpoint registers an EPM815 / EPN256 / EPN128 BoardSupport at process
// start via a static initializer that picks from ChipDetect. Non-Endpoint
// boards (Pi Zero 2 W via the endpoint-drivers RaspberryPi header) register
// their own `Zero2WBoardSupport : UnknownBoardSupport` from a static
// initializer in the user's translation unit.
//
// UnknownBoardSupport is the default: every peripheral method throws a
// std::runtime_error whose message names the missing package. Concrete
// BoardSupport classes inherit from it and override only the peripherals
// their board implements; anything they don't override keeps the throwing
// default (which is the right answer for peripherals that don't exist on
// that board — CAN on Pi, DAC on Nuvoton, etc.).

#pragma once

#include <mutex>
#include <stdexcept>
#include <string>

#include "Core.h"

namespace GHIElectronics::Endpoint::Core {

struct IBoardSupport {
    virtual ~IBoardSupport() = default;

    virtual const std::string& Name() const = 0;

    // ---------- GPIO dispatch ----------
    // The /dev/gpiochipN number the pin lives on, and the line within that
    // chip. Endpoint boards (EPM815 / EPN256 / EPN128) share the STM32-style
    // convention "16 lines per chip, chips labelled gpioa/gpiob/..."; those
    // defaults are provided by the concrete board-support classes in
    // BoardSupportInit.cpp. The Pi driver (Zero2W.h) overrides both to
    // return chip 0 and the BCM pin number directly, because the Pi exposes
    // every GPIO on a single /dev/gpiochip0.
    virtual int GpioChipId(int pin) = 0;
    virtual int GpioLineId(int pin) = 0;

    // ---------- Peripheral dispatch ----------
    virtual void I2cInitialize(int bus, int frequency) = 0;
    virtual void I2cUnInitialize(int bus) = 0;
    virtual int  I2cGetFrequency(int bus) = 0;

    virtual void SpiInitialize(int bus, int bufferSize) = 0;
    virtual void SpiUnInitialize(int bus) = 0;

    virtual void PwmInitialize(int pin) = 0;
    virtual void PwmUnInitialize(int pin) = 0;
    virtual int  PwmGetChipId(int pin) = 0;
    virtual int  PwmGetChannelId(int pin) = 0;

    virtual void CanInitialize(const std::string& port, int bitrate) = 0;
    virtual void CanUnInitialize(const std::string& port) = 0;

    virtual void SerialPortInitialize(const std::string& portName, bool enableHardwareFlowControl) = 0;
    virtual void SerialPortUnInitialize(const std::string& portName) = 0;
};

// Default board support: every method throws with a helpful message. Lives
// in BoardSupport::Current() until a real board registers.
struct UnknownBoardSupport : IBoardSupport {
    const std::string& Name() const override {
        static const std::string n = "Unknown";
        return n;
    }

    int  GpioChipId(int) override                  { Nope("GPIO"); return 0; }
    int  GpioLineId(int) override                  { Nope("GPIO"); return 0; }

    void I2cInitialize(int, int) override          { Nope("I2C"); }
    void I2cUnInitialize(int) override             { Nope("I2C"); }
    int  I2cGetFrequency(int) override             { Nope("I2C"); return 0; }

    void SpiInitialize(int, int) override          { Nope("SPI"); }
    void SpiUnInitialize(int) override             { Nope("SPI"); }

    void PwmInitialize(int) override               { Nope("PWM"); }
    void PwmUnInitialize(int) override             { Nope("PWM"); }
    int  PwmGetChipId(int) override                { Nope("PWM"); return 0; }
    int  PwmGetChannelId(int) override             { Nope("PWM"); return 0; }

    void CanInitialize(const std::string&, int) override    { Nope("CAN"); }
    void CanUnInitialize(const std::string&) override       { Nope("CAN"); }

    void SerialPortInitialize(const std::string&, bool) override { Nope("serial port"); }
    void SerialPortUnInitialize(const std::string&) override     { Nope("serial port"); }

protected:
    [[noreturn]] void Nope(const char* what) const {
        throw std::runtime_error(
            std::string(what) + " is not supported on this board ('" + Name() +
            "'). If this is an Endpoint board (EPM815 / EPDomino24 / EPN256 / EPN128), "
            "libendpoint should register its BoardSupport automatically at init. "
            "If this is a Raspberry Pi Zero 2 W, include the "
            "GHIElectronics.Endpoint.Drivers.RaspberryPi/Zero2W.h header, which "
            "registers Zero2WBoardSupport on load.");
    }
};

namespace BoardSupport {

namespace detail {
    inline std::mutex& Gate() { static std::mutex g; return g; }

    // Meyers singleton. Thread-safe lazy init guarantees the slot is
    // live before any static initializer tries to use it, regardless of
    // translation-unit init order.
    inline IBoardSupport*& Slot() {
        static IBoardSupport* instance = new UnknownBoardSupport();
        return instance;
    }

    // Tracked by a flag rather than `dynamic_cast<UnknownBoardSupport*>`:
    // the concrete boards derive from UnknownBoardSupport to inherit its
    // throwing defaults, so the cast succeeds even once a real board is in
    // the slot and would report "nothing registered" forever.
    inline bool& Registered() { static bool r = false; return r; }
}

// The registered board. Returns UnknownBoardSupport if nothing registered
// (every peripheral call then throws with a helpful "add the right
// package/header" message).
inline IBoardSupport& Current() {
    std::lock_guard<std::mutex> lock(detail::Gate());
    return *detail::Slot();
}

// True if a board has registered its implementation.
inline bool IsRegistered() {
    std::lock_guard<std::mutex> lock(detail::Gate());
    return detail::Registered();
}

// Called from each board's static-initializer. Replacing UnknownBoardSupport
// is always allowed; same-name replacement is a no-op; different-name
// replacement throws (a program must bind to at most one board).
inline void Register(IBoardSupport* board) {
    if (!board) throw std::invalid_argument("board is null");
    std::lock_guard<std::mutex> lock(detail::Gate());
    auto*& slot = detail::Slot();
    if (!detail::Registered() || slot->Name() == board->Name()) {
        delete slot;
        slot = board;
        detail::Registered() = true;
        return;
    }
    const std::string msg = "Board '" + slot->Name() + "' is already registered; "
        "cannot also register '" + board->Name() + "'. A program must bind to only one board.";
    delete board;
    throw std::runtime_error(msg);
}

}  // namespace BoardSupport

}  // namespace GHIElectronics::Endpoint::Core

// Force-link marker for BoardSupportInit.o out of libendpoint.a. The archive
// member containing EPM815BoardSupport / EPN256BoardSupport / EPN128BoardSupport
// and the chip-auto-register static initializer has no symbol the user's code
// references directly (the lambda is anonymous-namespace, the board support
// classes are internal). Without this reference, the linker's default archive-
// member selection drops the TU entirely and the chip never auto-registers;
// GPIO then throws "GPIO is not supported on this board ('Unknown')" on every
// Endpoint board.
//
// The symbol below is defined in BoardSupportInit.cpp. endpoint.h transitively
// includes BoardSupport.h for every user, so every user's object file ends up
// with an unresolved reference to it, which only BoardSupportInit.o can
// satisfy. The linker is then forced to pull the TU out of the archive, and
// its static initializer runs at program start, registering the right board
// for the detected chip.
//
// Not needed for shared-library consumers (libendpoint.so) because .so link
// rules do not do archive member selection.
extern "C" int endpoint_internal_board_support_init_token;

// Globally-unique namespace name so this does not shadow any
// GHIElectronics::Endpoint::Core::Internal name lookup inside src/.
namespace endpoint_board_support_force_link {
    // __attribute__((used)): tell the compiler this variable is in use so it
    // does not optimize away the initializer's read of the external token.
    // -O0 (debug builds) would keep it anyway, but a Release build at -O2 can
    // and will drop an apparently-unused static with internal linkage.
    __attribute__((used))
    inline const int _token = endpoint_internal_board_support_init_token;
}

// C ABI entry point for board driver .so files that live outside libendpoint
// (endpoint-drivers/cpp/RaspberryPi for Pi today). A driver .so links against
// libendpoint, declares its own BoardSupport subclass, and in its
// __attribute__((constructor)) calls:
//
//     endpoint_register_board_support(new Zero2WBoardSupport());
//
// The call is a thin wrapper around GHIElectronics::Endpoint::Core::
// BoardSupport::Register: takes ownership of the pointer, replaces
// UnknownBoardSupport silently, replaces the same-named board as a no-op,
// throws if a different real board is already registered. Exported via
// libendpoint.map's `endpoint_*` pattern so it is visible from dlopen'd
// callers.
//
// Needed because Python and .NET load the driver .so at runtime; they cannot
// trigger a C++ translation unit's static initializer the way a C/C++ user's
// `#include <endpoint/Zero2W.h>` does. The .so's constructor runs on dlopen
// and calls this entry point, giving all three languages the same registration
// behavior.
// visibility("default") is needed in addition to the extern "C" linkage
// because libendpoint's CMake build sets CXX_VISIBILITY_PRESET=hidden; the
// version script (libendpoint.map) can only promote symbols that are already
// visible at object level. Matches the ENDPOINT_API macro used by every
// other extern "C" entry point inside src/.
extern "C" __attribute__((visibility("default")))
void endpoint_register_board_support(void* board_support_impl);
