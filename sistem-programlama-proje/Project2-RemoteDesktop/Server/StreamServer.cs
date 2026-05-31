using System.Net;
using System.Net.Sockets;

public class StreamServer
{
    private readonly int           _port;
    private readonly ScreenCapture _capture;
    private TcpListener?           _listener;
    private bool                   _running;

    public StreamServer(int port)
    {
        _port    = port;
        _capture = new ScreenCapture(70);
    }

    public void Start()
    {
        _running  = true;
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine($"[Sunucu] Port {_port}'da dinliyor...");

        while (_running)
        {
            try
            {
                Console.WriteLine("[Sunucu] Baglanti bekleniyor...");
                var client = _listener.AcceptTcpClient();
                Console.WriteLine("[Sunucu] Istemci baglandi.");
                HandleClient(client);
            }
            catch (SocketException) when (!_running) { break; }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            while (_running && client.Connected)
            {
                var jpeg      = _capture.Capture();
                var sizeBytes = BitConverter.GetBytes((uint)jpeg.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(sizeBytes);

                stream.Write(sizeBytes);
                stream.Write(jpeg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sunucu] Baglanti koptu: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("[Sunucu] Baglanti kapatildi. Yeniden bekleniyor...");
        }
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();
    }
}
