#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

namespace GHIElectronics::Endpoint::Internal {

// As .NET's System.IO.Ports.SerialPort (for C++; Python uses pyserial).
// The values of parity, stopBits and handshake are those of the .NET enums.
class SerialPortImpl {
public:
    SerialPortImpl(const std::string& portName, int baudRate, int parity, int dataBits, int stopBits, int handshake);
    ~SerialPortImpl();

    SerialPortImpl(const SerialPortImpl&) = delete;
    SerialPortImpl& operator=(const SerialPortImpl&) = delete;

    void SetBaudRate(int baudRate);
    // Waits up to timeoutMs (-1: no limit) for the first byte, then returns
    // what has arrived; a Timeout error if nothing came
    size_t Read(uint8_t* buffer, size_t count, int timeoutMs);
    void Write(const uint8_t* buffer, size_t count, int timeoutMs);
    int BytesToRead();
    void DiscardInBuffer();
    void DiscardOutBuffer();

private:
    void Configure(int baudRate);

    int fd_ = -1;
    std::string name_;
    int parity_, dataBits_, stopBits_, handshake_;
};

// As .NET IoT's Iot.Device.SocketCan.CanRaw (for C++; Python uses python-can).
// Ids are the raw CAN id with the system's flags (CanId.Value in .NET).
class CanRawImpl {
public:
    explicit CanRawImpl(const std::string& networkInterface);
    ~CanRawImpl();

    CanRawImpl(const CanRawImpl&) = delete;
    CanRawImpl& operator=(const CanRawImpl&) = delete;

    void WriteFrame(uint32_t id, const uint8_t* data, size_t length);
    // Blocks until a frame comes; false if it cannot be read
    bool TryReadFrame(uint32_t& id, uint8_t* data, size_t capacity, size_t& length);
    void Filter(uint32_t id);

private:
    int socket_ = -1;
    std::string interface_;
};

}  // namespace GHIElectronics::Endpoint::Internal
