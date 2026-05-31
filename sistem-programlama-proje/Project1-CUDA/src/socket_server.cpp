#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#include "socket_server.h"
#include <iostream>
#include <vector>
#include <cstdint>

SocketServer::SocketServer()
    : server_sock_(INVALID_SOCKET), client_sock_(INVALID_SOCKET), running_(false)
{
    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);
}

SocketServer::~SocketServer() {
    stop();
    WSACleanup();
}

void SocketServer::start(int port) {
    running_ = true;
    thread_ = std::thread(&SocketServer::accept_loop, this, port);
}

void SocketServer::stop() {
    running_ = false;
    if (server_sock_ != INVALID_SOCKET) {
        closesocket(server_sock_);
        server_sock_ = INVALID_SOCKET;
    }
    {
        std::lock_guard<std::mutex> lk(socket_mutex_);
        if (client_sock_ != INVALID_SOCKET) {
            closesocket(client_sock_);
            client_sock_ = INVALID_SOCKET;
        }
    }
    if (thread_.joinable())
        thread_.join();
}

void SocketServer::accept_loop(int port) {
    server_sock_ = socket(AF_INET, SOCK_STREAM, 0);
    if (server_sock_ == INVALID_SOCKET) return;

    int opt = 1;
    setsockopt(server_sock_, SOL_SOCKET, SO_REUSEADDR, (char*)&opt, sizeof(opt));

    sockaddr_in addr{};
    addr.sin_family      = AF_INET;
    addr.sin_port        = htons((u_short)port);
    addr.sin_addr.s_addr = INADDR_ANY;

    if (bind(server_sock_, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        std::cerr << "[Sunucu] Bind hatasi: " << WSAGetLastError() << std::endl;
        return;
    }
    listen(server_sock_, 1);
    std::cout << "[Sunucu] Port " << port << "'da dinliyor..." << std::endl;

    while (running_) {
        SOCKET client = accept(server_sock_, nullptr, nullptr);
        if (client == INVALID_SOCKET) break;

        std::cout << "[Sunucu] Istemci baglandi." << std::endl;
        {
            std::lock_guard<std::mutex> lk(socket_mutex_);
            client_sock_ = client;
        }

        // Bağlantı kopuşunu algıla: select + MSG_PEEK
        while (running_) {
            fd_set rfds;
            FD_ZERO(&rfds);
            FD_SET(client, &rfds);
            timeval tv{ 0, 100000 }; // 100ms timeout
            int r = select(0, &rfds, nullptr, nullptr, &tv);
            if (r > 0) {
                char buf[1];
                if (recv(client, buf, 1, MSG_PEEK) <= 0) break; // bağlantı koptu
            } else if (r == SOCKET_ERROR) break;

            // send_frame bağlantıyı kapattıysa çık
            std::lock_guard<std::mutex> lk(socket_mutex_);
            if (client_sock_ == INVALID_SOCKET) break;
        }

        std::cout << "[Sunucu] Istemci baglantisi kesildi. Yeniden bekleniyor..." << std::endl;
        {
            std::lock_guard<std::mutex> lk(socket_mutex_);
            if (client_sock_ != INVALID_SOCKET) {
                closesocket(client_sock_);
                client_sock_ = INVALID_SOCKET;
            }
        }
    }
}

void SocketServer::send_frame(const cv::Mat& frame) {
    std::lock_guard<std::mutex> lk(socket_mutex_);
    if (client_sock_ == INVALID_SOCKET) return;

    std::vector<uchar> jpeg_buf;
    cv::imencode(".jpg", frame, jpeg_buf, { cv::IMWRITE_JPEG_QUALITY, 80 });

    uint32_t net_size = htonl(static_cast<uint32_t>(jpeg_buf.size()));
    if (send(client_sock_, reinterpret_cast<char*>(&net_size), 4, 0) == SOCKET_ERROR) {
        closesocket(client_sock_);
        client_sock_ = INVALID_SOCKET;
        return;
    }

    int total     = 0;
    int remaining = static_cast<int>(jpeg_buf.size());
    while (total < remaining) {
        int sent = send(client_sock_,
                        reinterpret_cast<char*>(jpeg_buf.data()) + total,
                        remaining - total, 0);
        if (sent == SOCKET_ERROR) {
            closesocket(client_sock_);
            client_sock_ = INVALID_SOCKET;
            return;
        }
        total += sent;
    }
}
