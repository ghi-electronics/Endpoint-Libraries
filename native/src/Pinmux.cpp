#include "Pinmux.h"

#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fcntl.h>
#include <string>
#include <sys/wait.h>
#include <unistd.h>
#include <vector>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Pinmux {

namespace {

constexpr const char* PinmuxPath = "/usr/sbin/endpoint-pinmux";

// Join args for diagnostics, so error messages name what was asked.
std::string Join(const std::vector<std::string>& args) {
    std::string s = PinmuxPath;
    for (const auto& a : args) { s += ' '; s += a; }
    return s;
}

// fork+exec PinmuxPath with args; capture stdout (if wanted) and stderr.
// Returns the child's exit code; -1 on fork/exec failure (errno set).
int Run(const std::vector<std::string>& args, std::string* stdoutOut, std::string* stderrOut) {
    int outPipe[2] = {-1, -1};
    int errPipe[2] = {-1, -1};
    if (pipe(outPipe) < 0) return -1;
    if (pipe(errPipe) < 0) {
        close(outPipe[0]); close(outPipe[1]);
        return -1;
    }

    pid_t child = fork();
    if (child < 0) {
        close(outPipe[0]); close(outPipe[1]);
        close(errPipe[0]); close(errPipe[1]);
        return -1;
    }
    if (child == 0) {
        // Child: wire up stdout / stderr, then exec.
        dup2(outPipe[1], STDOUT_FILENO);
        dup2(errPipe[1], STDERR_FILENO);
        close(outPipe[0]); close(outPipe[1]);
        close(errPipe[0]); close(errPipe[1]);

        std::vector<char*> argv;
        argv.push_back(const_cast<char*>(PinmuxPath));
        for (const auto& a : args) argv.push_back(const_cast<char*>(a.c_str()));
        argv.push_back(nullptr);
        execv(PinmuxPath, argv.data());
        _exit(127);
    }

    // Parent: close the write ends, drain both pipes until EOF.
    close(outPipe[1]);
    close(errPipe[1]);

    auto drain = [](int fd, std::string* dst) {
        char buf[256];
        ssize_t n;
        while ((n = read(fd, buf, sizeof buf)) > 0) {
            if (dst) dst->append(buf, static_cast<size_t>(n));
        }
    };
    drain(outPipe[0], stdoutOut);
    drain(errPipe[0], stderrOut);
    close(outPipe[0]);
    close(errPipe[0]);

    int status = 0;
    while (waitpid(child, &status, 0) < 0 && errno == EINTR) {}
    if (WIFEXITED(status)) return WEXITSTATUS(status);
    if (WIFSIGNALED(status)) return 128 + WTERMSIG(status);
    return -1;
}

std::string Rtrim(std::string s) {
    while (!s.empty() && (s.back() == '\n' || s.back() == '\r' ||
                          s.back() == ' '  || s.back() == '\t')) {
        s.pop_back();
    }
    return s;
}

}  // namespace

void Set(int pin, uint32_t function) {
    char funcBuf[16];
    std::snprintf(funcBuf, sizeof funcBuf, "0x%X", function);
    std::vector<std::string> args{"set", std::to_string(pin), funcBuf};
    std::string err;
    int code = Run(args, nullptr, &err);
    if (code != 0) {
        ThrowIO(Join(args) + " failed (exit " + std::to_string(code) + "): " + Rtrim(err));
    }
}

uint32_t Get(int pin) {
    std::vector<std::string> args{"get", std::to_string(pin)};
    std::string out, err;
    int code = Run(args, &out, &err);
    if (code != 0) {
        ThrowIO(Join(args) + " failed (exit " + std::to_string(code) + "): " + Rtrim(err));
    }
    // Output: "pin=<n> function=0x<hex> level=<0|1>\n"
    constexpr const char* key = "function=";
    auto i = out.find(key);
    if (i == std::string::npos) {
        ThrowInvalidOperation("endpoint-pinmux get " + std::to_string(pin) +
                              ": unexpected output '" + Rtrim(out) + "'");
    }
    auto start = i + std::strlen(key);
    auto end = start;
    while (end < out.size() && out[end] != ' ' && out[end] != '\t' &&
                               out[end] != '\n' && out[end] != '\r') {
        ++end;
    }
    std::string value = out.substr(start, end - start);
    // Accept both "0x<hex>" (what the C binary emits) and plain decimal.
    try {
        return static_cast<uint32_t>(std::stoul(value, nullptr, 0));
    } catch (const std::exception&) {
        ThrowInvalidOperation("endpoint-pinmux get " + std::to_string(pin) +
                              ": cannot parse function '" + value + "'");
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::Pinmux
