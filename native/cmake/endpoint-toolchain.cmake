# Builds C and C++ for Endpoint boards (Nuvoton MA35D1: Cortex-A35, aarch64)
# with Zig's clang, the same compiler "endpoint-cli c++" uses:
#
#   cmake -G Ninja -S . -B build -DCMAKE_TOOLCHAIN_FILE=cmake/endpoint-toolchain.cmake
#
# Zig is found in ENDPOINT_ZIG (the zig program), or in the tools folder
# endpoint-cli downloads it to (ENDPOINT_TOOLS, or %LOCALAPPDATA%\Endpoint\tools).

set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR aarch64)

set(ENDPOINT_ZIG_VERSION 0.17.0)

if(DEFINED ENV{ENDPOINT_ZIG})
    set(ENDPOINT_ZIG "$ENV{ENDPOINT_ZIG}")
else()
    if(DEFINED ENV{ENDPOINT_TOOLS})
        set(_tools "$ENV{ENDPOINT_TOOLS}")
    elseif(CMAKE_HOST_WIN32)
        set(_tools "$ENV{LOCALAPPDATA}/Endpoint/tools")
    else()
        set(_tools "$ENV{HOME}/.local/share/Endpoint/tools")
    endif()
    if(CMAKE_HOST_WIN32)
        set(ENDPOINT_ZIG "${_tools}/zig-x86_64-windows-${ENDPOINT_ZIG_VERSION}/zig.exe")
    else()
        set(ENDPOINT_ZIG "${_tools}/zig-x86_64-linux-${ENDPOINT_ZIG_VERSION}/zig")
    endif()
endif()
file(TO_CMAKE_PATH "${ENDPOINT_ZIG}" ENDPOINT_ZIG)

if(NOT EXISTS "${ENDPOINT_ZIG}")
    message(FATAL_ERROR "Zig not found at ${ENDPOINT_ZIG}: build an Endpoint C/C++ project once with "
                        "\"endpoint-cli c++\" (it downloads Zig), or set ENDPOINT_ZIG")
endif()

# The compiler is "zig cc" / "zig c++"
set(CMAKE_C_COMPILER "${ENDPOINT_ZIG}" cc)
set(CMAKE_CXX_COMPILER "${ENDPOINT_ZIG}" c++)

# The board's processor, and the oldest glibc the program may need (as endpoint-cli)
set(_target "-target aarch64-linux-gnu.2.34 -mcpu=cortex_a35")
set(CMAKE_C_FLAGS_INIT "${_target}")
set(CMAKE_CXX_FLAGS_INIT "${_target}")
set(CMAKE_EXE_LINKER_FLAGS_INIT "${_target}")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "${_target}")

# Static libraries with "zig ar" / "zig ranlib"
set(CMAKE_AR "${ENDPOINT_ZIG}")
set(CMAKE_RANLIB "${ENDPOINT_ZIG}")
foreach(_lang C CXX)
    set(CMAKE_${_lang}_ARCHIVE_CREATE "<CMAKE_AR> ar qc <TARGET> <LINK_FLAGS> <OBJECTS>")
    set(CMAKE_${_lang}_ARCHIVE_APPEND "<CMAKE_AR> ar q <TARGET> <LINK_FLAGS> <OBJECTS>")
    set(CMAKE_${_lang}_ARCHIVE_FINISH "<CMAKE_RANLIB> ranlib <TARGET>")
endforeach()

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
