#pragma once
#include <cstdint>

void apply_sobel(const uint8_t* d_input, uint8_t* d_output, int width, int height);
