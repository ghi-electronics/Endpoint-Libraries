# libendpoint: the board's hardware for Python and C++ (the .NET libraries
# have their own C# code). Build for the board:
#
#   cmake --preset endpoint
#   cmake --build --preset endpoint
#
# Output in build/: libendpoint.so (loaded by the Python package) and
# libendpoint.a (linked into C++ programs).

cmake_minimum_required(VERSION 3.20)
project(libendpoint VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
# Only the functions marked for export are visible in libendpoint.so
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

file(GLOB ENDPOINT_SOURCES CONFIGURE_DEPENDS src/*.cpp)

add_library(endpoint_objects OBJECT ${ENDPOINT_SOURCES})
target_include_directories(endpoint_objects PUBLIC include PRIVATE src)
target_compile_options(endpoint_objects PRIVATE -Wall -Wextra)
# Release: no debug information (libendpoint.a is linked into customers' programs)
target_compile_options(endpoint_objects PRIVATE $<$<CONFIG:Release>:-g0>)

add_library(endpoint SHARED $<TARGET_OBJECTS:endpoint_objects>)
# Release: no debug information in the library the Python package carries
target_link_options(endpoint PRIVATE $<$<CONFIG:Release>:-s>)
add_library(endpoint_static STATIC $<TARGET_OBJECTS:endpoint_objects>)
set_target_properties(endpoint_static PROPERTIES OUTPUT_NAME endpoint)

# Board tests of the internal parts (run as root on the board)
add_executable(foundation-test tests/FoundationTest.cpp)
target_include_directories(foundation-test PRIVATE src)
target_link_libraries(foundation-test PRIVATE endpoint_static)

# Board test of the public C++ API (endpoint.h), as a customer would use it
add_executable(cpp-api-test tests/CppApiTest.cpp)
target_include_directories(cpp-api-test PRIVATE include)
target_link_libraries(cpp-api-test PRIVATE endpoint_static)
