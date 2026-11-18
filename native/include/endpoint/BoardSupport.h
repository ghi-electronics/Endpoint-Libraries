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

    // Meyers singleton — thread-safe lazy init guarantees the slot is
    // live before any static initializer tries to use it, regardless of
    // translation-unit init order.
    inline IBoardSupport*& Slot() {
        static IBoardSupport* instance = new UnknownBoardSupport();
        return instance;
    }
}

// The registered board. Returns UnknownBoardSupport if nothing registered
// (every peripheral call then throws with a helpful "add the right
// package/header" message).
inline IBoardSupport& Current() {
    std::lock_guard<std::mutex> lock(detail::Gate());
    return *detail::Slot();
}

// True if a real board (not UnknownBoardSupport) has registered.
inline bool IsRegistered() {
    std::lock_guard<std::mutex> lock(detail::Gate());
    return dynamic_cast<UnknownBoardSupport*>(detail::Slot()) == nullptr;
}

// Called from each board's static-initializer. Replacing UnknownBoardSupport
// is always allowed; same-name replacement is a no-op; different-name
// replacement throws (a program must bind to at most one board).
inline void Register(IBoardSupport* board) {
    if (!board) throw std::invalid_argument("board is null");
    std::lock_guard<std::mutex> lock(detail::Gate());
    auto*& slot = detail::Slot();
    if (dynamic_cast<UnknownBoardSupport*>(slot) != nullptr || slot->Name() == board->Name()) {
        delete slot;
        slot = board;
        return;
    }
    const std::string msg = "Board '" + slot->Name() + "' is already registered; "
        "cannot also register '" + board->Name() + "'. A program must bind to only one board.";
    delete board;
    throw std::runtime_error(msg);
}

}  // namespace BoardSupport

}  // namespace GHIElectronics::Endpoint::Core
