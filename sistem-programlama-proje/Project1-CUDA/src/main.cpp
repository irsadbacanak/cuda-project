#include "socket_server.h"  // Winsock2 için opencv'den önce gelmeli
#include <opencv2/opencv.hpp>
#include <cuda_runtime.h>
#include "kernel.h"
#include <iostream>
#include <chrono>
#include <mutex>

int main() {
    SocketServer server;
    server.start(9999);

    cv::VideoCapture cap(0);
    if (!cap.isOpened()) {
        std::cerr << "Webcam acilamadi!" << std::endl;
        server.stop();
        return -1;
    }

    cv::Mat frame, gray, result;
    uint8_t* d_input  = nullptr;
    uint8_t* d_output = nullptr;
    int prev_width = 0, prev_height = 0;

    std::mutex frame_mutex; // CUDA işlemi bitmeden soket göndermemek için
    auto prev_time = std::chrono::steady_clock::now();

    while (true) {
        cap >> frame;
        if (frame.empty()) break;

        cv::cvtColor(frame, gray, cv::COLOR_BGR2GRAY);

        int width  = gray.cols;
        int height = gray.rows;
        int size   = width * height;

        {
            std::lock_guard<std::mutex> lk(frame_mutex);

            if (width != prev_width || height != prev_height) {
                cudaFree(d_input);
                cudaFree(d_output);
                cudaMalloc(&d_input,  size);
                cudaMalloc(&d_output, size);
                prev_width  = width;
                prev_height = height;
            }

            cudaMemcpy(d_input, gray.data, size, cudaMemcpyHostToDevice);
            apply_sobel(d_input, d_output, width, height); // cudaDeviceSynchronize içerir
            result = cv::Mat(height, width, CV_8UC1);
            cudaMemcpy(result.data, d_output, size, cudaMemcpyDeviceToHost);

            server.send_frame(result); // CUDA tamamlandıktan sonra gönder
        }

        auto now = std::chrono::steady_clock::now();
        double fps = 1.0 / std::chrono::duration<double>(now - prev_time).count();
        prev_time = now;

        cv::putText(result, cv::format("FPS: %.1f", fps), cv::Point(10, 30),
                    cv::FONT_HERSHEY_SIMPLEX, 1.0, cv::Scalar(255), 2);

        cv::imshow("Sobel Edge Detection - CUDA", result);

        if (cv::waitKey(1) == 'q') break;
    }

    cudaFree(d_input);
    cudaFree(d_output);
    cap.release();
    cv::destroyAllWindows();
    server.stop();

    return 0;
}
