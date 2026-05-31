cmake_minimum_required(VERSION 3.20)
project(CudaProject CUDA CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CUDA_STANDARD 17)

find_package(CUDAToolkit REQUIRED)
find_package(OpenCV REQUIRED)

add_executable(main
    src/main.cu
)

target_link_libraries(main
    PRIVATE
    CUDA::cudart
    ${OpenCV_LIBS}
)

target_include_directories(main
    PRIVATE
    ${OpenCV_INCLUDE_DIRS}
)
