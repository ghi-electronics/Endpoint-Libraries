// Single translation unit that stamps the stb_image implementation into
// libendpoint.so. Consumers (src/Graphics.cpp) include the plain header for
// declarations only.
#define STB_IMAGE_IMPLEMENTATION
#define STBI_NO_HDR
#define STBI_NO_LINEAR
#define STBI_NO_PIC
#define STBI_NO_PNM
#include "stb_image.h"
