// The version of libendpoint: the same as the GHI Endpoint libraries for .NET
// and Python. The one place to change it (CMakeLists.txt reads it from here).
//
// Within a minor version (0.2.x) the library only adds, never changes or
// removes: code written for 0.2.0 works with every 0.2.x. A breaking change
// goes to the next minor version (0.3). So a library built on libendpoint
// needs "0.2.0 or newer, below 0.3", and can check it when it compiles:
//
//   #if ENDPOINT_VERSION_NUMBER < 200 || ENDPOINT_VERSION_NUMBER >= 300
//   #error "This library needs libendpoint 0.2"
//   #endif
#pragma once

#define ENDPOINT_VERSION_MAJOR 0
#define ENDPOINT_VERSION_MINOR 3
#define ENDPOINT_VERSION_PATCH 0

// "0.2.0"
#define ENDPOINT_VERSION_TEXT(major, minor, patch) #major "." #minor "." #patch
#define ENDPOINT_VERSION_EXPAND(major, minor, patch) ENDPOINT_VERSION_TEXT(major, minor, patch)
#define ENDPOINT_VERSION \
    ENDPOINT_VERSION_EXPAND(ENDPOINT_VERSION_MAJOR, ENDPOINT_VERSION_MINOR, ENDPOINT_VERSION_PATCH)

// One number to compare: 0.2.0 is 200, 1.4.12 is 10412
#define ENDPOINT_VERSION_NUMBER (ENDPOINT_VERSION_MAJOR * 10000 + ENDPOINT_VERSION_MINOR * 100 + ENDPOINT_VERSION_PATCH)
