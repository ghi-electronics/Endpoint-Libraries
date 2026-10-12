// The hidden C layer (see CApi.h) for the C++ SerialPort and CanRaw classes

#include <cstdint>

#include "CApi.h"
#include "Ports.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {
template <typename T>
T* Handle(void* handle, const char* name) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, std::string("The ") + name + " is disposed");
    }
    return static_cast<T*>(handle);
}
}  // namespace

// ---------------------------------------------------------------- SerialPort

ENDPOINT_API int endpoint_serial_open(const char* portName, int baudRate, int parity, int dataBits, int stopBits,
                                      int handshake, void** port) {
    return Call([&] { *port = new SerialPortImpl(portName, baudRate, parity, dataBits, stopBits, handshake); });
}

ENDPOINT_API int endpoint_serial_close(void* port) {
    return Call([&] { delete static_cast<SerialPortImpl*>(port); });
}

ENDPOINT_API int endpoint_serial_set_baud_rate(void* port, int baudRate) {
    return Call([&] { Handle<SerialPortImpl>(port, "SerialPort")->SetBaudRate(baudRate); });
}

// timeoutMs -1: no limit
ENDPOINT_API int endpoint_serial_read(void* port, uint8_t* buffer, int count, int timeoutMs, int* read) {
    return Call([&] { *read = static_cast<int>(Handle<SerialPortImpl>(port, "SerialPort")->Read(buffer, count, timeoutMs)); });
}

ENDPOINT_API int endpoint_serial_write(void* port, const uint8_t* buffer, int count, int timeoutMs) {
    return Call([&] { Handle<SerialPortImpl>(port, "SerialPort")->Write(buffer, count, timeoutMs); });
}

ENDPOINT_API int endpoint_serial_bytes_to_read(void* port, int* count) {
    return Call([&] { *count = Handle<SerialPortImpl>(port, "SerialPort")->BytesToRead(); });
}

ENDPOINT_API int endpoint_serial_discard_in(void* port) {
    return Call([&] { Handle<SerialPortImpl>(port, "SerialPort")->DiscardInBuffer(); });
}

ENDPOINT_API int endpoint_serial_discard_out(void* port) {
    return Call([&] { Handle<SerialPortImpl>(port, "SerialPort")->DiscardOutBuffer(); });
}

// ---------------------------------------------------------------- CanRaw

ENDPOINT_API int endpoint_canraw_open(const char* networkInterface, void** can) {
    return Call([&] { *can = new CanRawImpl(networkInterface); });
}

ENDPOINT_API int endpoint_canraw_close(void* can) {
    return Call([&] { delete static_cast<CanRawImpl*>(can); });
}

ENDPOINT_API int endpoint_canraw_write_frame(void* can, uint32_t id, const uint8_t* data, int length) {
    return Call([&] { Handle<CanRawImpl>(can, "CanRaw")->WriteFrame(id, data, length); });
}

// Blocks until a frame comes; *ok is 0 if it could not be read
ENDPOINT_API int endpoint_canraw_read_frame(void* can, uint32_t* id, uint8_t* data, int capacity, int* length, int* ok) {
    return Call([&] {
        size_t n = 0;
        *ok = Handle<CanRawImpl>(can, "CanRaw")->TryReadFrame(*id, data, capacity, n);
        *length = static_cast<int>(n);
    });
}

ENDPOINT_API int endpoint_canraw_filter(void* can, uint32_t id) {
    return Call([&] { Handle<CanRawImpl>(can, "CanRaw")->Filter(id); });
}
