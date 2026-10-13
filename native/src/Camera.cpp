#include "Camera.h"

#include <fcntl.h>
#include <linux/videodev2.h>
#include <poll.h>
#include <sys/eventfd.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <unistd.h>

#include <algorithm>
#include <atomic>
#include <cerrno>
#include <chrono>
#include <condition_variable>
#include <cstring>
#include <mutex>
#include <thread>

#include "Errors.h"
#include "UsbAccess.h"

extern "C" {
#include "../third-party/stb/stb_image_write.h"
}

namespace GHIElectronics::Endpoint::Internal::Camera {

namespace {

using Clock = std::chrono::steady_clock;

// The first picture waits this long for the camera's auto exposure to settle
constexpr auto WarmUp = std::chrono::milliseconds(800);
// A camera started by Capture keeps running this long after the last picture
constexpr auto KeepRunning = std::chrono::seconds(3);

// The JPEG standard's Huffman tables (DHT segment; its Annex K, as stb_image_write
// has them): MJPEG cameras often leave them out of their frames
constexpr uint8_t StandardHuffmanTables[] = {
    0xFF, 0xC4, 0x01, 0xA2, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
    0x0B, 0x10, 0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00,
    0x01, 0x7D, 0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51,
    0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52,
    0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26,
    0x27, 0x28, 0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47,
    0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67,
    0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87,
    0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
    0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
    0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
    0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
    0xF7, 0xF8, 0xF9, 0xFA, 0x01, 0x00, 0x03, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
    0x0B, 0x11, 0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, 0x04, 0x07, 0x05, 0x04, 0x04, 0x00, 0x01,
    0x02, 0x77, 0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07,
    0x61, 0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33,
    0x52, 0xF0, 0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19,
    0x1A, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46,
    0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66,
    0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85,
    0x86, 0x87, 0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
    0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA,
    0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8,
    0xD9, 0xDA, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
    0xF7, 0xF8, 0xF9, 0xFA,
};

uint32_t V4l2Format(int pixelFormat) { return pixelFormat == Mjpeg ? V4L2_PIX_FMT_MJPEG : V4L2_PIX_FMT_YUYV; }

int Ioctl(int fd, unsigned long request, void* argument) {
    int result;
    do {
        result = ioctl(fd, request, argument);
    } while (result < 0 && errno == EINTR);
    return result;
}

[[noreturn]] void ThrowCamera(const std::string& what) {
    if (errno == ENODEV || errno == EIO) {
        ThrowIO(what + ": the camera was unplugged");
    }
    ThrowSystemError(what);
}

// The camera's control for each of ours
constexpr uint32_t ControlIds[ControlCount] = {
    V4L2_CID_BRIGHTNESS, V4L2_CID_CONTRAST, V4L2_CID_SATURATION, V4L2_CID_HUE, V4L2_CID_GAMMA, V4L2_CID_GAIN,
    V4L2_CID_SHARPNESS, V4L2_CID_BACKLIGHT_COMPENSATION, V4L2_CID_AUTO_WHITE_BALANCE,
    V4L2_CID_WHITE_BALANCE_TEMPERATURE, V4L2_CID_EXPOSURE_AUTO, V4L2_CID_EXPOSURE_ABSOLUTE,
    V4L2_CID_POWER_LINE_FREQUENCY, V4L2_CID_FOCUS_AUTO, V4L2_CID_FOCUS_ABSOLUTE, V4L2_CID_ZOOM_ABSOLUTE,
    V4L2_CID_PAN_ABSOLUTE, V4L2_CID_TILT_ABSOLUTE,
};
constexpr const char* ControlNames[ControlCount] = {
    "Brightness", "Contrast", "Saturation", "Hue", "Gamma", "Gain", "Sharpness", "BacklightCompensation",
    "AutoWhiteBalance", "WhiteBalanceTemperature", "AutoExposure", "Exposure", "PowerLineFrequency",
    "AutoFocus", "Focus", "Zoom", "Pan", "Tilt",
};

// An MJPEG frame as a JPEG every program reads: with the standard Huffman
// tables if the camera left them out, and without what some cameras add
// after the JPEG's end (EOI)
std::vector<uint8_t> FixMjpeg(const uint8_t* data, size_t length) {
    for (size_t end = length; end >= 4; end--) {
        if (data[end - 2] == 0xFF && data[end - 1] == 0xD9) {
            length = end;
            break;
        }
        if (data[end - 1] != 0) {
            break;  // not padding: leave the frame as it is
        }
    }
    std::vector<uint8_t> jpeg(data, data + length);
    if (length < 4 || data[0] != 0xFF || data[1] != 0xD8) {
        return jpeg;
    }
    size_t at = 2;
    while (at + 4 <= length) {
        if (data[at] != 0xFF) {
            break;  // not a marker: leave the frame as it is
        }
        uint8_t marker = data[at + 1];
        if (marker == 0xFF) {
            at++;  // fill byte
            continue;
        }
        if (marker == 0xC4) {
            return jpeg;  // it has its tables
        }
        if (marker == 0xDA) {
            jpeg.insert(jpeg.begin() + static_cast<long>(at), std::begin(StandardHuffmanTables),
                        std::end(StandardHuffmanTables));
            return jpeg;
        }
        at += 2 + ((data[at + 2] << 8) | data[at + 3]);
    }
    return jpeg;
}

// A YUYV frame (two pixels in 4 bytes: Y0 U Y1 V) as a JPEG
std::vector<uint8_t> EncodeYuyv(const uint8_t* data, int width, int height, int quality) {
    std::vector<uint8_t> rgb(static_cast<size_t>(width) * height * 3);
    auto clamp = [](int v) { return static_cast<uint8_t>(v < 0 ? 0 : v > 255 ? 255 : v); };
    for (size_t pair = 0; pair < static_cast<size_t>(width) * height / 2; pair++) {
        const uint8_t* p = data + pair * 4;
        int d = p[1] - 128, e = p[3] - 128;
        for (int k = 0; k < 2; k++) {
            int c = 298 * (p[k * 2] - 16);
            uint8_t* out = &rgb[(pair * 2 + k) * 3];
            out[0] = clamp((c + 409 * e + 128) >> 8);
            out[1] = clamp((c - 100 * d - 208 * e + 128) >> 8);
            out[2] = clamp((c + 516 * d + 128) >> 8);
        }
    }
    std::vector<uint8_t> jpeg;
    stbi_write_jpg_to_func([](void* context, void* bytes, int size) {
        auto out = static_cast<std::vector<uint8_t>*>(context);
        out->insert(out->end(), static_cast<uint8_t*>(bytes), static_cast<uint8_t*>(bytes) + size);
    }, &jpeg, width, height, 3, rgb.data(), quality);
    return jpeg;
}

struct Buffer {
    void* start = nullptr;
    size_t length = 0;
};

}  // namespace

struct State {
    int fd = -1;
    int wake = -1;
    std::string name;
    std::vector<Format> formats;
    Format current;
    int frameRate = 0;  // asked for (0: the highest)
    int quality = 85;
    FrameCallback frame;
    std::shared_ptr<UsbAccess::Watch> watch;

    mutable std::mutex mutex;
    std::condition_variable changed;
    bool running = false;
    bool events = false;  // StartStreaming
    bool stopping = false;
    std::thread worker;
    std::vector<Buffer> buffers;
    Clock::time_point started, lastUse;
    // The newest frame as the camera sent it
    std::vector<uint8_t> latest;
    uint64_t latestNumber = 0;
    Clock::time_point latestTime;
    Format latestFormat;
    bool failed = false;

    ~State() {
        if (wake >= 0) {
            close(wake);
        }
        if (fd >= 0) {
            close(fd);
        }
    }

    std::vector<uint8_t> Jpeg(const uint8_t* data, size_t length, const Format& format) const {
        if (format.pixelFormat == Mjpeg) {
            return FixMjpeg(data, length);
        }
        if (length < static_cast<size_t>(format.width) * format.height * 2) {
            return {};
        }
        return EncodeYuyv(data, format.width, format.height, quality);
    }

    // Starts the camera's pictures (the mutex held)
    void Start() {
        v4l2_format format{};
        format.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        format.fmt.pix.width = static_cast<uint32_t>(current.width);
        format.fmt.pix.height = static_cast<uint32_t>(current.height);
        format.fmt.pix.pixelformat = V4l2Format(current.pixelFormat);
        format.fmt.pix.field = V4L2_FIELD_ANY;
        if (Ioctl(fd, VIDIOC_S_FMT, &format) != 0) {
            ThrowCamera("Cannot set the camera's picture size");
        }
        current.width = static_cast<int>(format.fmt.pix.width);
        current.height = static_cast<int>(format.fmt.pix.height);
        int fps = frameRate > 0 ? frameRate : current.maxFrameRate;
        if (fps > 0) {
            v4l2_streamparm parm{};
            parm.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            parm.parm.capture.timeperframe.numerator = 1;
            parm.parm.capture.timeperframe.denominator = static_cast<uint32_t>(fps);
            Ioctl(fd, VIDIOC_S_PARM, &parm);  // a camera without frame rates keeps its own
        }
        v4l2_requestbuffers request{};
        request.count = 4;
        request.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        request.memory = V4L2_MEMORY_MMAP;
        if (Ioctl(fd, VIDIOC_REQBUFS, &request) != 0 || request.count < 2) {
            ThrowCamera("Cannot get the camera's picture buffers");
        }
        buffers.assign(request.count, Buffer{});
        for (uint32_t i = 0; i < request.count; i++) {
            v4l2_buffer buffer{};
            buffer.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            buffer.memory = V4L2_MEMORY_MMAP;
            buffer.index = i;
            if (Ioctl(fd, VIDIOC_QUERYBUF, &buffer) != 0) {
                Release();
                ThrowCamera("Cannot get the camera's picture buffers");
            }
            void* start = mmap(nullptr, buffer.length, PROT_READ | PROT_WRITE, MAP_SHARED, fd, buffer.m.offset);
            if (start == MAP_FAILED) {
                Release();
                ThrowCamera("Cannot get the camera's picture buffers");
            }
            buffers[i] = {start, buffer.length};
            if (Ioctl(fd, VIDIOC_QBUF, &buffer) != 0) {
                Release();
                ThrowCamera("Cannot start the camera");
            }
        }
        v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        if (Ioctl(fd, VIDIOC_STREAMON, &type) != 0) {
            Release();
            ThrowCamera("Cannot start the camera");
        }
        running = true;
        failed = false;
        started = lastUse = Clock::now();
    }

    // Stops the camera's pictures (the mutex held)
    void Release() {
        v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        Ioctl(fd, VIDIOC_STREAMOFF, &type);
        for (auto& buffer : buffers) {
            if (buffer.start != nullptr) {
                munmap(buffer.start, buffer.length);
            }
        }
        buffers.clear();
        v4l2_requestbuffers request{};
        request.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        request.memory = V4L2_MEMORY_MMAP;
        Ioctl(fd, VIDIOC_REQBUFS, &request);
        running = false;
    }

    // Starts the camera and the thread that takes its frames (the mutex held)
    void Run(const std::shared_ptr<State>& self) {
        if (running) {
            return;
        }
        if (worker.joinable()) {
            worker.join();  // ended by itself (idle)
        }
        Start();
        stopping = false;
        worker = std::thread(Work, self);
    }

    // Stops the thread and the camera (the mutex not held)
    void Halt(std::unique_lock<std::mutex>& lock) {
        stopping = true;
        uint64_t one = 1;
        (void)!write(wake, &one, sizeof one);
        if (worker.joinable()) {
            if (worker.get_id() == std::this_thread::get_id()) {
                worker.detach();  // stopped by its own frame event
                return;
            }
            std::thread finishing = std::move(worker);
            lock.unlock();
            finishing.join();
            lock.lock();
        }
    }

    static void Work(std::shared_ptr<State> s) {
        while (true) {
            pollfd fds[] = {{s->fd, POLLIN, 0}, {s->wake, POLLIN, 0}};
            int ready = poll(fds, 2, 200);
            std::unique_lock lock(s->mutex);
            if (fds[1].revents & POLLIN) {
                uint64_t count;
                (void)!read(s->wake, &count, sizeof count);
            }
            bool idle = !s->events && Clock::now() - s->lastUse > KeepRunning;
            if (s->stopping || idle) {
                s->Release();
                s->changed.notify_all();
                return;
            }
            if (ready <= 0 || !(fds[0].revents & (POLLIN | POLLERR | POLLHUP))) {
                continue;
            }
            v4l2_buffer buffer{};
            buffer.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            buffer.memory = V4L2_MEMORY_MMAP;
            if (Ioctl(s->fd, VIDIOC_DQBUF, &buffer) != 0) {
                if (errno == EAGAIN) {
                    continue;
                }
                // Unplugged (or broken): Capture says so
                s->failed = true;
                s->Release();
                s->changed.notify_all();
                return;
            }
            const auto* data = static_cast<const uint8_t*>(s->buffers[buffer.index].start);
            size_t length = buffer.bytesused;
            bool good = length > 0 && !(buffer.flags & V4L2_BUF_FLAG_ERROR) &&
                        (s->current.pixelFormat != Mjpeg || (length > 2 && data[0] == 0xFF && data[1] == 0xD8));
            if (good) {
                s->latest.assign(data, data + length);
                s->latestNumber++;
                s->latestTime = Clock::now();
                s->latestFormat = s->current;
            }
            Ioctl(s->fd, VIDIOC_QBUF, &buffer);
            s->changed.notify_all();
            if (good && s->events && s->frame) {
                // The event without the mutex held (a handler may take a picture, or stop)
                auto raw = s->latest;
                auto format = s->latestFormat;
                lock.unlock();
                auto jpeg = s->Jpeg(raw.data(), raw.size(), format);
                if (!jpeg.empty()) {
                    try {
                        s->frame(jpeg.data(), jpeg.size(), format.width, format.height);
                    }
                    catch (...) {
                        // a handler's error is not the camera's
                    }
                }
            }
        }
    }
};

Device::Device(const std::string& deviceName, FrameCallback frame, std::function<void()> unplugged)
    : state(std::make_shared<State>()) {
    State& s = *state;
    s.frame = std::move(frame);
    s.fd = open(deviceName.c_str(), O_RDWR | O_NONBLOCK | O_CLOEXEC);
    if (s.fd < 0) {
        if (errno == ENOENT || errno == ENODEV || errno == ENXIO) {
            ThrowArgument("There is no camera " + deviceName +
                          " (it was unplugged, or the name is not a DeviceName from UsbHostController)");
        }
        ThrowSystemError("Cannot open " + deviceName);
    }
    v4l2_capability capability{};
    uint32_t caps = 0;
    if (Ioctl(s.fd, VIDIOC_QUERYCAP, &capability) == 0) {
        caps = (capability.capabilities & V4L2_CAP_DEVICE_CAPS) ? capability.device_caps : capability.capabilities;
    }
    if (!(caps & V4L2_CAP_VIDEO_CAPTURE) || !(caps & V4L2_CAP_STREAMING)) {
        ThrowArgument(deviceName + " is not a camera (use the DeviceName of a Webcam from UsbHostController)");
    }
    s.name = reinterpret_cast<const char*>(capability.card);

    // What it can send: MJPEG and YUYV (every USB camera has one of them), the sizes and frame rates
    for (uint32_t index = 0;; index++) {
        v4l2_fmtdesc description{};
        description.index = index;
        description.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        if (Ioctl(s.fd, VIDIOC_ENUM_FMT, &description) != 0) {
            break;
        }
        int pixelFormat;
        if (description.pixelformat == V4L2_PIX_FMT_MJPEG || description.pixelformat == V4L2_PIX_FMT_JPEG) {
            pixelFormat = Mjpeg;
        }
        else if (description.pixelformat == V4L2_PIX_FMT_YUYV) {
            pixelFormat = Yuyv;
        }
        else {
            continue;
        }
        for (uint32_t sizeIndex = 0;; sizeIndex++) {
            v4l2_frmsizeenum size{};
            size.index = sizeIndex;
            size.pixel_format = description.pixelformat;
            if (Ioctl(s.fd, VIDIOC_ENUM_FRAMESIZES, &size) != 0) {
                break;
            }
            Format format;
            format.pixelFormat = pixelFormat;
            if (size.type == V4L2_FRMSIZE_TYPE_DISCRETE) {
                format.width = static_cast<int>(size.discrete.width);
                format.height = static_cast<int>(size.discrete.height);
            }
            else {
                format.width = static_cast<int>(size.stepwise.max_width);
                format.height = static_cast<int>(size.stepwise.max_height);
            }
            v4l2_frmivalenum interval{};
            interval.pixel_format = description.pixelformat;
            interval.width = static_cast<uint32_t>(format.width);
            interval.height = static_cast<uint32_t>(format.height);
            for (interval.index = 0; Ioctl(s.fd, VIDIOC_ENUM_FRAMEINTERVALS, &interval) == 0; interval.index++) {
                auto fraction = interval.type == V4L2_FRMIVAL_TYPE_DISCRETE ? interval.discrete : interval.stepwise.min;
                if (fraction.numerator > 0) {
                    format.maxFrameRate = std::max(format.maxFrameRate, static_cast<int>(fraction.denominator / fraction.numerator));
                }
                if (interval.type != V4L2_FRMIVAL_TYPE_DISCRETE) {
                    break;
                }
            }
            bool known = std::any_of(s.formats.begin(), s.formats.end(), [&](const Format& f) {
                return f.width == format.width && f.height == format.height && f.pixelFormat == format.pixelFormat;
            });
            if (!known) {
                s.formats.push_back(format);
            }
            if (size.type != V4L2_FRMSIZE_TYPE_DISCRETE) {
                break;
            }
        }
    }
    if (s.formats.empty()) {
        ThrowNotSupported(deviceName + " sends no MJPEG or YUYV pictures");
    }
    // At first 640 x 480 (every USB camera has it), MJPEG if it can
    auto find = [&](int width, int height, int pixelFormat) {
        return std::find_if(s.formats.begin(), s.formats.end(), [&](const Format& f) {
            return f.width == width && f.height == height && f.pixelFormat == pixelFormat;
        });
    };
    auto first = find(640, 480, Mjpeg);
    if (first == s.formats.end()) first = find(640, 480, Yuyv);
    if (first == s.formats.end()) first = s.formats.begin();
    s.current = *first;

    s.wake = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
    if (s.wake < 0) {
        ThrowSystemError("Cannot use the camera");
    }
    s.watch = UsbAccess::WatchUnplug(s.fd, std::move(unplugged));
}

Device::~Device() {
    {
        std::unique_lock lock(state->mutex);
        state->events = false;
        state->Halt(lock);
        if (state->running) {
            state->Release();
        }
    }
    UsbAccess::StopWatch(state->watch);
}

std::string Device::Name() const { return state->name; }

bool Device::Connected() const { return !UsbAccess::IsGone(state->watch); }

std::vector<Format> Device::Formats() const { return state->formats; }

Format Device::Current() const {
    std::lock_guard lock(state->mutex);
    Format format = state->current;
    format.maxFrameRate = state->frameRate > 0 ? state->frameRate : state->current.maxFrameRate;
    return format;
}

void Device::SetResolution(int width, int height, int frameRate, int pixelFormat) {
    State& s = *state;
    if (frameRate < 0) {
        ThrowArgumentOutOfRange("The frame rate must be 0 (the camera's highest) or more");
    }
    // MJPEG if the camera has it at this size (it is small and fast), else YUYV
    const Format* chosen = nullptr;
    for (const auto& format : s.formats) {
        if (format.width == width && format.height == height && (pixelFormat < 0 || format.pixelFormat == pixelFormat) &&
            (chosen == nullptr || format.pixelFormat == Mjpeg)) {
            chosen = &format;
        }
    }
    if (chosen == nullptr && pixelFormat >= 0) {
        ThrowArgument("The camera has no " + std::to_string(width) + "x" + std::to_string(height) +
                      (pixelFormat == Mjpeg ? " MJPEG" : " YUYV") + " pictures");
    }
    if (chosen == nullptr) {
        std::string sizes;
        for (const auto& format : s.formats) {
            std::string size = std::to_string(format.width) + "x" + std::to_string(format.height);
            if (sizes.find(size) == std::string::npos) {
                sizes += (sizes.empty() ? "" : ", ") + size;
            }
        }
        ThrowArgument("The camera has no " + std::to_string(width) + "x" + std::to_string(height) + " pictures: it has " + sizes);
    }
    std::unique_lock lock(s.mutex);
    bool wasRunning = s.running, events = s.events;
    if (wasRunning) {
        s.Halt(lock);
        if (s.running) {
            s.Release();
        }
    }
    s.current = *chosen;
    s.frameRate = frameRate;
    s.latestNumber = 0;
    s.latest.clear();
    if (wasRunning && events) {
        s.Run(state);
    }
}

int Device::JpegQuality() const {
    std::lock_guard lock(state->mutex);
    return state->quality;
}

void Device::SetJpegQuality(int quality) {
    if (quality < 1 || quality > 100) {
        ThrowArgumentOutOfRange("The JPEG quality must be 1 to 100");
    }
    std::lock_guard lock(state->mutex);
    state->quality = quality;
}

std::vector<uint8_t> Device::Capture(int timeoutMs) {
    State& s = *state;
    std::unique_lock lock(s.mutex);
    if (UsbAccess::IsGone(s.watch)) {
        ThrowIO("The camera was unplugged");
    }
    s.Run(state);
    s.lastUse = Clock::now();
    // A new frame, after the auto exposure settled
    uint64_t after = s.latestNumber;
    auto ready = s.started + WarmUp;
    auto deadline = Clock::now() + std::chrono::milliseconds(timeoutMs < 0 ? 3600000 : timeoutMs) +
                    std::max(ready - Clock::now(), Clock::duration::zero());
    while (!(s.latestNumber > after && s.latestTime >= ready)) {
        if (s.failed || UsbAccess::IsGone(s.watch)) {
            ThrowIO("The camera was unplugged");
        }
        if (!s.running) {
            s.Run(state);  // it stopped (idle) while this waited: it warms up again
            ready = s.started + WarmUp;
        }
        if (s.changed.wait_until(lock, deadline) == std::cv_status::timeout &&
            !(s.latestNumber > after && s.latestTime >= ready)) {
            ThrowTimeout();
        }
    }
    s.lastUse = Clock::now();
    auto raw = s.latest;
    auto format = s.latestFormat;
    lock.unlock();
    auto jpeg = s.Jpeg(raw.data(), raw.size(), format);
    if (jpeg.empty()) {
        ThrowIO("The camera sent a broken picture");
    }
    return jpeg;
}

void Device::StartStreaming() {
    State& s = *state;
    std::lock_guard lock(s.mutex);
    if (UsbAccess::IsGone(s.watch)) {
        ThrowIO("The camera was unplugged");
    }
    s.events = true;
    s.Run(state);
}

void Device::StopStreaming() {
    State& s = *state;
    std::unique_lock lock(s.mutex);
    s.events = false;
    s.lastUse = Clock::now();  // a Capture right after still finds it running
}

bool Device::Streaming() const {
    std::lock_guard lock(state->mutex);
    return state->events;
}

ControlInfo Device::GetControl(int control) const {
    if (control < 0 || control >= ControlCount) {
        ThrowArgumentOutOfRange("There is no such camera control");
    }
    ControlInfo info;
    v4l2_queryctrl query{};
    query.id = ControlIds[control];
    if (Ioctl(state->fd, VIDIOC_QUERYCTRL, &query) != 0 || (query.flags & V4L2_CTRL_FLAG_DISABLED)) {
        return info;
    }
    v4l2_control value{};
    value.id = ControlIds[control];
    if (Ioctl(state->fd, VIDIOC_G_CTRL, &value) != 0) {
        return info;
    }
    info.supported = true;
    if (control == AutoExposure) {
        // A menu on the camera (manual, auto, priority modes): on (1) or off (0, manual)
        info.minimum = 0;
        info.maximum = 1;
        info.step = 1;
        info.defaultValue = query.default_value == V4L2_EXPOSURE_MANUAL ? 0 : 1;
        info.value = value.value == V4L2_EXPOSURE_MANUAL ? 0 : 1;
        return info;
    }
    info.minimum = query.minimum;
    info.maximum = query.maximum;
    info.step = query.step > 0 ? query.step : 1;
    info.defaultValue = query.default_value;
    info.value = value.value;
    return info;
}

void Device::SetControl(int control, int value) {
    ControlInfo info = GetControl(control);
    if (!info.supported) {
        ThrowNotSupported(std::string("The camera has no ") + ControlNames[control] + " control");
    }
    if (value < info.minimum || value > info.maximum) {
        ThrowArgumentOutOfRange(std::string(ControlNames[control]) + " must be " + std::to_string(info.minimum) +
                                " to " + std::to_string(info.maximum));
    }
    v4l2_control setting{};
    setting.id = ControlIds[control];
    setting.value = value;
    if (control == AutoExposure) {
        // On: the camera's automatic mode (USB cameras: aperture priority, others: auto)
        setting.value = V4L2_EXPOSURE_MANUAL;
        if (value != 0) {
            setting.value = V4L2_EXPOSURE_APERTURE_PRIORITY;
            if (Ioctl(state->fd, VIDIOC_S_CTRL, &setting) == 0) {
                return;
            }
            setting.value = V4L2_EXPOSURE_AUTO;
        }
    }
    if (Ioctl(state->fd, VIDIOC_S_CTRL, &setting) != 0) {
        if (errno == EACCES || errno == EBUSY) {
            ThrowInvalidOperation(std::string(ControlNames[control]) +
                                  " cannot change now (for example Exposure while AutoExposure is on)");
        }
        ThrowCamera(std::string("Cannot set ") + ControlNames[control]);
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::Camera
