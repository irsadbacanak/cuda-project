cmake_minimum_required(VERSION 3.18)
project(CudaProject CUDA CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CUDA_STANDARD 17)
set(CMAKE_CUDA_ARCHITECTURES native)

find_package(CUDAToolkit REQUIRED)
find_package(OpenCV REQUIRED)

add_executable(main
    src/main.cpp
    src/kernel.cu
    src/socket_server.cpp
)

target_link_libraries(main
    PRIVATE
    CUDA::cudart
    ${OpenCV_LIBS}
    ws2_32
)

target_include_directories(main
    PRIVATE
    ${OpenCV_INCLUDE_DIRS}
    src/
)
