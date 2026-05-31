#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <winsock2.h>
#include <opencv2/opencv.hpp>
#include <thread>
#include <mutex>
#include <atomic>

class SocketServer {
public:
    SocketServer();
    ~SocketServer();

    void start(int port);
    void send_frame(const cv::Mat& frame);
    void stop();

private:
    void accept_loop(int port);

    SOCKET            server_sock_;
    SOCKET            client_sock_;
    std::thread       thread_;
    std::mutex        socket_mutex_;
    std::atomic<bool> running_;
};
