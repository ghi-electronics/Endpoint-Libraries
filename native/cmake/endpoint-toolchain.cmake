# Builds C and C++ for Endpoint boards with Zig's clang, the same compiler
# that "endpoint-cli c++" uses. One toolchain covers both architectures;
# pick via the ENDPOINT_ARCH cache variable:
#
#   aarch64 (default) — Nuvoton MA35D1 / MA35D0 (Cortex-A35, 64-bit)
#   arm               — ST STM32MP153       (Cortex-A7,  32-bit, hard float)
#
# Use through the matching preset:
#
#   cmake --preset endpoint-arm64
#   cmake --build --preset endpoint-arm64
#
#   cmake --preset endpoint-arm
#   cmake --build --preset endpoint-arm
#
# Zig is found in ENDPOINT_ZIG (the zig program), or in the tools folder
# endpoint-cli downloads it to (ENDPOINT_TOOLS, or %LOCALAPPDATA%\Endpoint\tools).

if(NOT DEFINED ENDPOINT_ARCH)
    set(ENDPOINT_ARCH "aarch64" CACHE STRING "Target architecture: aarch64 (MA35) or arm (STM32MP1)")
endif()

if(ENDPOINT_ARCH STREQUAL "aarch64")
    set(CMAKE_SYSTEM_PROCESSOR aarch64)
    # Cortex-A35 (MA35D1 / MA35D0) with glibc 2.34 to match endpoint-cli.
    set(_target "-target aarch64-linux-gnu.2.34 -mcpu=cortex_a35")
elseif(ENDPOINT_ARCH STREQUAL "arm")
    set(CMAKE_SYSTEM_PROCESSOR arm)
    # Cortex-A7 (STM32MP153) with hard float + glibc 2.34.
    set(_target "-target arm-linux-gnueabihf.2.34 -mcpu=cortex_a7")
else()
    message(FATAL_ERROR "ENDPOINT_ARCH='${ENDPOINT_ARCH}' is not supported: use 'aarch64' or 'arm'")
endif()
message(STATUS "libendpoint target: ${ENDPOINT_ARCH}")

set(CMAKE_SYSTEM_NAME Linux)

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
