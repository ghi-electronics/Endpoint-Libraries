// Serial ports for Endpoint in C++, with the same API as .NET's
// System.IO.Ports.SerialPort (Python programs use pyserial):
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Ports;
//
//   EPM815::SerialPort::Initialize(EPM815::SerialPort::Uart4);
//   SerialPort port(EPM815::SerialPort::Uart4, 115200);
//   port.Open();
//   port.WriteLine("Hello");
//   std::string reply = port.ReadLine();
//
// .NET's properties are a getter and a setter of the same name:
// port.ReadTimeout() and port.ReadTimeout(1000).
#pragma once

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <span>
#include <string>
#include <string_view>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Ports {

enum class Parity { None = 0, Odd = 1, Even = 2, Mark = 3, Space = 4 };
enum class StopBits { None = 0, One = 1, Two = 2, OnePointFive = 3 };
enum class Handshake { None = 0, XOnXOff = 1, RequestToSend = 2, RequestToSendXOnXOff = 3 };

class SerialPort {
public:
    static constexpr int InfiniteTimeout = -1;

    explicit SerialPort(std::string portName, int baudRate = 9600, Ports::Parity parity = Ports::Parity::None,
                        int dataBits = 8, Ports::StopBits stopBits = Ports::StopBits::One)
        : portName_(std::move(portName)), baudRate_(baudRate), parity_(parity), dataBits_(dataBits), stopBits_(stopBits) {}

    // Settings: set them before Open (BaudRate also while open)
    const std::string& PortName() const { return portName_; }
    void PortName(std::string value) { Closed("PortName"); portName_ = std::move(value); }
    int BaudRate() const { return baudRate_; }
    void BaudRate(int value) {
        if (IsOpen()) {
            Core::detail::Check(endpoint_serial_set_baud_rate(handle_.Get("SerialPort"), value));
        }
        baudRate_ = value;
    }
    Ports::Parity Parity() const { return parity_; }
    void Parity(Ports::Parity value) { Closed("Parity"); parity_ = value; }
    int DataBits() const { return dataBits_; }
    void DataBits(int value) { Closed("DataBits"); dataBits_ = value; }
    Ports::StopBits StopBits() const { return stopBits_; }
    void StopBits(Ports::StopBits value) { Closed("StopBits"); stopBits_ = value; }
    Ports::Handshake Handshake() const { return handshake_; }
    void Handshake(Ports::Handshake value) { Closed("Handshake"); handshake_ = value; }

    // Milliseconds, or InfiniteTimeout (the default)
    int ReadTimeout() const { return readTimeout_; }
    void ReadTimeout(int milliseconds) { readTimeout_ = milliseconds; }
    int WriteTimeout() const { return writeTimeout_; }
    void WriteTimeout(int milliseconds) { writeTimeout_ = milliseconds; }

    // What ReadLine and WriteLine use: "\n"
    const std::string& NewLine() const { return newLine_; }
    void NewLine(std::string value) { newLine_ = std::move(value); }

    void Open() {
        if (IsOpen()) {
            throw Core::InvalidOperationException("The port is already open.");
        }
        void* handle = nullptr;
        Core::detail::Check(endpoint_serial_open(portName_.c_str(), baudRate_, static_cast<int>(parity_), dataBits_,
                                                 static_cast<int>(stopBits_), static_cast<int>(handshake_), &handle));
        handle_ = Handle(handle);
        pending_.clear();
    }

    void Close() { handle_.Reset(); }
    bool IsOpen() const { return open(); }

    int BytesToRead() const {
        int count = 0;
        Core::detail::Check(endpoint_serial_bytes_to_read(h(), &count));
        return count + static_cast<int>(pending_.size());
    }

    // Waits up to ReadTimeout for the first byte, then returns what has arrived
    // (TimeoutException if nothing came)
    int Read(std::span<uint8_t> buffer) {
        if (!pending_.empty()) {
            auto n = std::min(buffer.size(), pending_.size());
            std::copy_n(pending_.begin(), n, buffer.begin());
            pending_.erase(0, n);
            return static_cast<int>(n);
        }
        int read = 0;
        Core::detail::Check(endpoint_serial_read(h(), buffer.data(), static_cast<int>(buffer.size()), readTimeout_, &read));
        return read;
    }
    int Read(uint8_t* buffer, int offset, int count) { return Read(std::span<uint8_t>(buffer + offset, count)); }

    // A byte, waiting up to ReadTimeout
    int ReadByte() {
        uint8_t value = 0;
        Read(std::span<uint8_t>(&value, 1));
        return value;
    }

    // Everything that has arrived, without waiting
    std::string ReadExisting() {
        std::string text = std::move(pending_);
        pending_.clear();
        int available = 0;
        Core::detail::Check(endpoint_serial_bytes_to_read(h(), &available));
        if (available > 0) {
            std::string more(available, '\0');
            int read = 0;
            Core::detail::Check(endpoint_serial_read(h(), reinterpret_cast<uint8_t*>(more.data()), available, 0, &read));
            text.append(more.data(), read);
        }
        return text;
    }

    // Reads up to NewLine (not included). On a timeout, what came so far is
    // kept for the next read.
    std::string ReadLine() {
        auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(readTimeout_);
        while (true) {
            auto at = pending_.find(newLine_);
            if (at != std::string::npos) {
                auto line = pending_.substr(0, at);
                pending_.erase(0, at + newLine_.size());
                return line;
            }
            int wait = readTimeout_;
            if (readTimeout_ >= 0) {
                wait = static_cast<int>(std::chrono::duration_cast<std::chrono::milliseconds>(
                                            deadline - std::chrono::steady_clock::now()).count());
                if (wait <= 0) {
                    throw Core::TimeoutException("The operation has timed out.");
                }
            }
            uint8_t chunk[256];
            int read = 0;
            Core::detail::Check(endpoint_serial_read(h(), chunk, sizeof chunk, wait, &read));
            pending_.append(reinterpret_cast<char*>(chunk), read);
        }
    }

    void Write(std::span<const uint8_t> buffer) {
        Core::detail::Check(endpoint_serial_write(h(), buffer.data(), static_cast<int>(buffer.size()), writeTimeout_));
    }
    void Write(const uint8_t* buffer, int offset, int count) { Write(std::span<const uint8_t>(buffer + offset, count)); }
    void Write(std::string_view text) {
        Write(std::span<const uint8_t>(reinterpret_cast<const uint8_t*>(text.data()), text.size()));
    }
    void WriteLine(std::string_view text) {
        Write(text);
        Write(std::string_view(newLine_));
    }

    void DiscardInBuffer() {
        pending_.clear();
        Core::detail::Check(endpoint_serial_discard_in(h()));
    }
    void DiscardOutBuffer() { Core::detail::Check(endpoint_serial_discard_out(h())); }

    void Dispose() { Close(); }

private:
    using Handle = Core::detail::Handle<endpoint_serial_close>;

    void* h() const {
        if (!open()) {
            throw Core::InvalidOperationException("The port is closed.");
        }
        return handle_.Get("SerialPort");
    }
    bool open() const {
        try {
            handle_.Get("SerialPort");
            return true;
        }
        catch (const Core::ObjectDisposedException&) {
            return false;
        }
    }
    void Closed(const char* what) const {
        if (open()) {
            throw Core::InvalidOperationException(std::string(what) + " cannot change while the port is open.");
        }
    }

    std::string portName_;
    int baudRate_;
    Ports::Parity parity_;
    int dataBits_;
    Ports::StopBits stopBits_;
    Ports::Handshake handshake_ = Ports::Handshake::None;
    int readTimeout_ = InfiniteTimeout;
    int writeTimeout_ = InfiniteTimeout;
    std::string newLine_ = "\n";
    std::string pending_;
    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Ports
