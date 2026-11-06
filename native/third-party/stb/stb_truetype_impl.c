// Single translation unit that stamps the stb_truetype implementation into
// libendpoint.so. Consumers (src/Graphics.cpp) include the plain header for
// declarations only.
#define STB_TRUETYPE_IMPLEMENTATION
#include "stb_truetype.h"
