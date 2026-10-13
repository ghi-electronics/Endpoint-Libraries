/* stb_image_write's code (JPEG only, to memory), built once into libendpoint (src/Camera.cpp uses it) */
#define STB_IMAGE_WRITE_IMPLEMENTATION
#define STBI_WRITE_NO_STDIO
#include "stb_image_write.h"
