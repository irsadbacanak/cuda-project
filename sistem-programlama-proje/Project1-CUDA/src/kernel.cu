#include "kernel.h"
#include <cuda_runtime.h>
#include <cmath>

__global__ void sobel_kernel(const uint8_t* input, uint8_t* output, int width, int height) {
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x >= width || y >= height) return;

    if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
        output[y * width + x] = 0;
        return;
    }

    int gx = -input[(y-1)*width + (x-1)] + input[(y-1)*width + (x+1)]
             - 2*input[y*width + (x-1)] + 2*input[y*width + (x+1)]
             - input[(y+1)*width + (x-1)] + input[(y+1)*width + (x+1)];

    int gy = -input[(y-1)*width + (x-1)] - 2*input[(y-1)*width + x] - input[(y-1)*width + (x+1)]
             +  input[(y+1)*width + (x-1)] + 2*input[(y+1)*width + x] + input[(y+1)*width + (x+1)];

    int magnitude = (int)sqrtf((float)(gx*gx + gy*gy));
    output[y * width + x] = (uint8_t)(magnitude > 255 ? 255 : magnitude);
}

void apply_sobel(const uint8_t* d_input, uint8_t* d_output, int width, int height) {
    dim3 block(16, 16);
    dim3 grid((width  + block.x - 1) / block.x,
              (height + block.y - 1) / block.y);
    sobel_kernel<<<grid, block>>>(d_input, d_output, width, height);
    cudaDeviceSynchronize();
}
