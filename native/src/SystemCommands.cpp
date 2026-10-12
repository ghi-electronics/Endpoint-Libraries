#include "SystemCommands.h"

#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <spawn.h>
#include <sys/wait.h>
#include <unistd.h>

#include <cerrno>
#include <chrono>
#include <cstring>

#include "Errors.h"

extern char** environ;

namespace GHIElectronics::Endpoint::Internal::SystemCommands {

namespace {

std::string Trim(const std::string& text) {
    auto start = text.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) {
        return {};
    }
    return text.substr(start, text.find_last_not_of(" \t\r\n") - start + 1);
}

struct Pipe {
    int read = -1;
    int write = -1;

    Pipe() {
        int fds[2];
        if (pipe2(fds, O_CLOEXEC) != 0) {
            ThrowSystemError("Cannot run a system tool");
        }
        read = fds[0];
        write = fds[1];
    }

    ~Pipe() {
        if (read >= 0) close(read);
        if (write >= 0) close(write);
    }
};

}  // namespace

void Run(const std::string& program, const std::string& what, const std::vector<std::string>& arguments, int timeoutMs) {
    Pipe output, error;

    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    posix_spawn_file_actions_adddup2(&actions, output.write, STDOUT_FILENO);
    posix_spawn_file_actions_adddup2(&actions, error.write, STDERR_FILENO);

    std::vector<char*> argv;
    argv.push_back(const_cast<char*>(program.c_str()));
    for (const auto& argument : arguments) {
        argv.push_back(const_cast<char*>(argument.c_str()));
    }
    argv.push_back(nullptr);

    pid_t pid;
    auto spawned = posix_spawnp(&pid, program.c_str(), &actions, nullptr, argv.data(), environ);
    posix_spawn_file_actions_destroy(&actions);
    if (spawned != 0) {
        throw EndpointError(ErrorKind::IO, what + ": cannot run " + program + ": " + std::strerror(spawned));
    }
    close(output.write);
    output.write = -1;
    close(error.write);
    error.write = -1;

    // Read both outputs until the tool closes them, or the time is up
    std::string text[2];
    pollfd fds[2] = {{output.read, POLLIN, 0}, {error.read, POLLIN, 0}};
    auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
    auto open = 2;
    while (open > 0) {
        auto left = std::chrono::duration_cast<std::chrono::milliseconds>(deadline - std::chrono::steady_clock::now()).count();
        if (left <= 0) {
            kill(pid, SIGKILL);
            waitpid(pid, nullptr, 0);
            throw EndpointError(ErrorKind::IO, what + ": " + program + " took too long");
        }
        if (poll(fds, 2, static_cast<int>(left)) < 0 && errno != EINTR) {
            break;
        }
        for (int i = 0; i < 2; i++) {
            if (fds[i].fd >= 0 && (fds[i].revents & (POLLIN | POLLHUP | POLLERR))) {
                char buffer[512];
                auto n = read(fds[i].fd, buffer, sizeof buffer);
                if (n > 0) {
                    text[i].append(buffer, n);
                }
                else {
                    fds[i].fd = -1;  // closed: poll ignores it now
                    open--;
                }
            }
        }
    }

    int status = 0;
    while (waitpid(pid, &status, 0) < 0 && errno == EINTR) {
    }
    if (!WIFEXITED(status) || WEXITSTATUS(status) != 0) {
        auto reason = Trim(text[1]);
        if (reason.empty()) {
            reason = Trim(text[0]);
        }
        if (reason.empty()) {
            reason = program + " failed";
        }
        throw EndpointError(ErrorKind::IO, what + ": " + reason);
    }
}

}  // namespace GHIElectronics::Endpoint::Internal::SystemCommands
