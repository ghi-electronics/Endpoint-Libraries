#include "Ports.h"

#include <fcntl.h>
#include <linux/can.h>
#include <linux/can/raw.h>
#include <net/if.h>
#include <poll.h>
#include <sys/ioctl.h>
#include <sys/socket.h>
#include <termios.h>
#include <unistd.h>

#include <cerrno>
#include <cstring>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal {

// ---------------------------------------------------------------- Serial port

namespace {

// .NET's enums
enum Parity { ParityNone = 0, ParityOdd = 1, ParityEven = 2, ParityMark = 3, ParitySpace = 4 };
enum StopBits { StopBitsNone = 0, StopBitsOne = 1, StopBitsTwo = 2, StopBitsOnePointFive = 3 };
enum Handshake { HandshakeNone = 0, XOnXOff = 1, RequestToSend = 2, RequestToSendXOnXOff = 3 };

speed_t Speed(int baudRate) {
    switch (baudRate) {
        case 50: return B50;           case 75: return B75;           case 110: return B110;
        case 134: return B134;         case 150: return B150;         case 200: return B200;
        case 300: return B300;         case 600: return B600;         case 1200: return B1200;
        case 1800: return B1800;       case 2400: return B2400;       case 4800: return B4800;
        case 9600: return B9600;       case 19200: return B19200;     case 38400: return B38400;
        case 57600: return B57600;     case 115200: return B115200;   case 230400: return B230400;
        case 460800: return B460800;   case 500000: return B500000;   case 576000: return B576000;
        case 921600: return B921600;   case 1000000: return B1000000; case 1152000: return B1152000;
        case 1500000: return B1500000; case 2000000: return B2000000; case 2500000: return B2500000;
        case 3000000: return B3000000; case 3500000: return B3500000; case 4000000: return B4000000;
    }
    ThrowArgumentOutOfRange("The baud rate " + std::to_string(baudRate) + " is not supported");
}

}  // namespace

SerialPortImpl::SerialPortImpl(const std::string& portName, int baudRate, int parity, int dataBits, int stopBits,
                               int handshake)
    : name_(portName), parity_(parity), dataBits_(dataBits), stopBits_(stopBits), handshake_(handshake) {
    if (parity < ParityNone || parity > ParitySpace) ThrowArgumentOutOfRange("Unknown parity");
    if (dataBits < 5 || dataBits > 8) ThrowArgumentOutOfRange("The data bits must be 5 to 8");
    if (stopBits < StopBitsOne || stopBits > StopBitsOnePointFive) ThrowArgumentOutOfRange("The stop bits must be One, Two or OnePointFive");
    if (handshake < HandshakeNone || handshake > RequestToSendXOnXOff) ThrowArgumentOutOfRange("Unknown handshake");
    Speed(baudRate);

    fd_ = open(portName.c_str(), O_RDWR | O_NOCTTY | O_NONBLOCK | O_CLOEXEC);
    if (fd_ < 0) {
        if (errno == ENOENT) {
            ThrowIO("The port '" + portName + "' does not exist");
        }
        ThrowSystemError("Cannot open " + portName);
    }
    try {
        Configure(baudRate);
    }
    catch (...) {
        close(fd_);
        throw;
    }
}

SerialPortImpl::~SerialPortImpl() {
    if (fd_ >= 0) {
        close(fd_);
    }
}

void SerialPortImpl::Configure(int baudRate) {
    termios t{};
    if (tcgetattr(fd_, &t) < 0) {
        ThrowSystemError("Cannot read the settings of " + name_);
    }
    cfmakeraw(&t);
    cfsetispeed(&t, Speed(baudRate));
    cfsetospeed(&t, Speed(baudRate));

    t.c_cflag &= ~(CSIZE | PARENB | PARODD | CMSPAR | CSTOPB | CRTSCTS);
    t.c_cflag |= CLOCAL | CREAD;
    t.c_cflag |= dataBits_ == 5 ? CS5 : dataBits_ == 6 ? CS6 : dataBits_ == 7 ? CS7 : CS8;
    switch (parity_) {
        case ParityOdd: t.c_cflag |= PARENB | PARODD; break;
        case ParityEven: t.c_cflag |= PARENB; break;
        case ParityMark: t.c_cflag |= PARENB | CMSPAR | PARODD; break;
        case ParitySpace: t.c_cflag |= PARENB | CMSPAR; break;
    }
    if (stopBits_ != StopBitsOne) {
        t.c_cflag |= CSTOPB;  // 1.5 stop bits with 5 data bits
    }
    t.c_iflag &= ~(IXON | IXOFF | IXANY);
    if (handshake_ == RequestToSend || handshake_ == RequestToSendXOnXOff) {
        t.c_cflag |= CRTSCTS;
    }
    if (handshake_ == XOnXOff || handshake_ == RequestToSendXOnXOff) {
        t.c_iflag |= IXON | IXOFF;
    }
    t.c_cc[VMIN] = 0;
    t.c_cc[VTIME] = 0;
    if (tcsetattr(fd_, TCSANOW, &t) < 0) {
        ThrowSystemError("Cannot set up " + name_);
    }
}

void SerialPortImpl::SetBaudRate(int baudRate) { Configure(baudRate); }

size_t SerialPortImpl::Read(uint8_t* buffer, size_t count, int timeoutMs) {
    if (count == 0) {
        return 0;
    }
    pollfd waitFor{fd_, POLLIN, 0};
    int ready;
    while ((ready = poll(&waitFor, 1, timeoutMs)) < 0 && errno == EINTR) {
    }
    if (ready < 0) {
        ThrowSystemError("Cannot read " + name_);
    }
    if (ready == 0) {
        ThrowTimeout();
    }
    auto n = read(fd_, buffer, count);
    if (n < 0) {
        if (errno == EAGAIN) {
            ThrowTimeout();
        }
        ThrowSystemError("Cannot read " + name_);
    }
    return static_cast<size_t>(n);
}

void SerialPortImpl::Write(const uint8_t* buffer, size_t count, int timeoutMs) {
    size_t done = 0;
    while (done < count) {
        auto n = write(fd_, buffer + done, count - done);
        if (n > 0) {
            done += static_cast<size_t>(n);
            continue;
        }
        if (n < 0 && errno != EAGAIN && errno != EINTR) {
            ThrowSystemError("Cannot write " + name_);
        }
        pollfd waitFor{fd_, POLLOUT, 0};
        if (poll(&waitFor, 1, timeoutMs) == 0) {
            ThrowTimeout();
        }
    }
}

int SerialPortImpl::BytesToRead() {
    int count = 0;
    if (ioctl(fd_, FIONREAD, &count) < 0) {
        ThrowSystemError("Cannot read the state of " + name_);
    }
    return count;
}

void SerialPortImpl::DiscardInBuffer() { tcflush(fd_, TCIFLUSH); }

void SerialPortImpl::DiscardOutBuffer() { tcflush(fd_, TCOFLUSH); }

// ---------------------------------------------------------------- CAN

CanRawImpl::CanRawImpl(const std::string& networkInterface) : interface_(networkInterface) {
    socket_ = socket(PF_CAN, SOCK_RAW | SOCK_CLOEXEC, CAN_RAW);
    if (socket_ < 0) {
        ThrowSystemError("Cannot open a CAN socket");
    }
    ifreq request{};
    std::strncpy(request.ifr_name, networkInterface.c_str(), IFNAMSIZ - 1);
    sockaddr_can address{};
    address.can_family = AF_CAN;
    if (ioctl(socket_, SIOCGIFINDEX, &request) < 0 ||
        (address.can_ifindex = request.ifr_ifindex,
         bind(socket_, reinterpret_cast<sockaddr*>(&address), sizeof address) < 0)) {
        auto error = errno;
        close(socket_);
        errno = error;
        ThrowSystemError("Cannot open the CAN port " + networkInterface);
    }
}

CanRawImpl::~CanRawImpl() {
    if (socket_ >= 0) {
        close(socket_);
    }
}

void CanRawImpl::WriteFrame(uint32_t id, const uint8_t* data, size_t length) {
    if (length > CAN_MAX_DLEN) {
        ThrowArgument("A classic CAN frame carries up to 8 bytes");
    }
    can_frame frame{};
    frame.can_id = id;
    frame.can_dlc = static_cast<__u8>(length);
    std::memcpy(frame.data, data, length);
    if (write(socket_, &frame, sizeof frame) != sizeof frame) {
        if (errno == ENETDOWN) {
            ThrowIO(interface_ + " is not started: call EPM815.Can.Initialize first");
        }
        ThrowSystemError("Cannot send on " + interface_);
    }
}

bool CanRawImpl::TryReadFrame(uint32_t& id, uint8_t* data, size_t capacity, size_t& length) {
    can_frame frame{};
    if (read(socket_, &frame, sizeof frame) != sizeof frame) {
        return false;
    }
    id = frame.can_id;
    length = frame.can_dlc < capacity ? frame.can_dlc : capacity;
    std::memcpy(data, frame.data, length);
    return true;
}

void CanRawImpl::Filter(uint32_t id) {
    can_filter filter{};
    filter.can_id = id;
    filter.can_mask = CAN_EFF_FLAG | CAN_RTR_FLAG | ((id & CAN_EFF_FLAG) ? CAN_EFF_MASK : CAN_SFF_MASK);
    if (setsockopt(socket_, SOL_CAN_RAW, CAN_RAW_FILTER, &filter, sizeof filter) < 0) {
        ThrowSystemError("Cannot set the filter on " + interface_);
    }
}

}  // namespace GHIElectronics::Endpoint::Internal
