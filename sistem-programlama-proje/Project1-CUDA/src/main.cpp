#include "socket_server.h"  // Winsock2 için opencv'den önce gelmeli
#include <opencv2/opencv.hpp>
#include <cuda_runtime.h>
#include "kernel.h"
#include <iostream>
#include <chrono>
#include <mutex>

int main() {
    SocketServer raw_server;    // Ham gri frame  → port 9998
    SocketServer sobel_server;  // CUDA Sobel     → port 9999
    raw_server.start(9998);
    sobel_server.start(9999);

    cv::VideoCapture cap(0);
    if (!cap.isOpened()) {
        std::cerr << "Webcam acilamadi!" << std::endl;
        raw_server.stop();
        sobel_server.stop();
        return -1;
    }

    cv::Mat frame, result;
    uint8_t* d_input  = nullptr;
    uint8_t* d_output = nullptr;
    int prev_width = 0, prev_height = 0;

    std::mutex frame_mutex;
    auto prev_time = std::chrono::steady_clock::now();

    while (true) {
        cap >> frame;
        if (frame.empty()) break;

        int width  = frame.cols;
        int height = frame.rows;
        int size   = width * height * 3; // BGR: 3 kanal

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

            cudaMemcpy(d_input, frame.data, size, cudaMemcpyHostToDevice);
            apply_sobel_color(d_input, d_output, width, height);
            result = cv::Mat(height, width, CV_8UC3);
            cudaMemcpy(result.data, d_output, size, cudaMemcpyDeviceToHost);

            raw_server.send_frame(frame);    // Ham renkli görüntü
            sobel_server.send_frame(result); // Renkli CUDA Sobel
        }

        auto now = std::chrono::steady_clock::now();
        double fps = 1.0 / std::chrono::duration<double>(now - prev_time).count();
        prev_time = now;

        cv::putText(frame,  "Ham Goruntu",          cv::Point(10, 30), cv::FONT_HERSHEY_SIMPLEX, 1.0, cv::Scalar(0, 255, 0), 2);
        cv::putText(result, cv::format("CUDA Sobel  FPS: %.1f", fps), cv::Point(10, 30), cv::FONT_HERSHEY_SIMPLEX, 1.0, cv::Scalar(0, 200, 255), 2);

        cv::Mat display;
        cv::hconcat(frame, result, display);
        cv::imshow("Sobel Edge Detection - CUDA", display);

        if (cv::waitKey(1) == 'q') break;
    }

    cudaFree(d_input);
    cudaFree(d_output);
    cap.release();
    cv::destroyAllWindows();
    raw_server.stop();
    sobel_server.stop();

    return 0;
}
