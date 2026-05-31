// Kullanım: ClientCrossPlatform [host] [tcp-port] [http-port]
// Varsayılan: 127.0.0.1 9999 8080

var host     = args.ElementAtOrDefault(0) ?? "127.0.0.1";
var tcpPort  = int.Parse(args.ElementAtOrDefault(1) ?? "9999");
var httpPort = int.Parse(args.ElementAtOrDefault(2) ?? "8080");

var streamer  = new HttpStreamer(httpPort);
var receiver  = new TcpReceiver();

// FPS sayacı
int      frameCount = 0;
DateTime fpsTimer   = DateTime.UtcNow;

receiver.FrameReceived += jpeg =>
{
    streamer.PushFrame(jpeg);

    frameCount++;
    double elapsed = (DateTime.UtcNow - fpsTimer).TotalSeconds;
    if (elapsed >= 1.0)
    {
        Console.WriteLine($"FPS: {frameCount / elapsed,5:F1}  |  frame: {jpeg.Length,7} B");
        frameCount = 0;
        fpsTimer   = DateTime.UtcNow;
    }
};

receiver.Disconnected += () =>
{
    Console.WriteLine("[!] TCP bağlantısı kesildi.");
};

// Ctrl+C ile temiz kapanış
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

streamer.Start();
Console.WriteLine($"MJPEG stream  →  http://localhost:{httpPort}/");
Console.WriteLine($"TCP hedef     →  {host}:{tcpPort}");
Console.WriteLine("Bağlanılıyor...");

try
{
    receiver.Connect(host, tcpPort);
    Console.WriteLine("Bağlandı. Çıkmak için Ctrl+C.");
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException) { }
catch (Exception ex)
{
    Console.WriteLine($"[!] Bağlantı hatası: {ex.Message}");
}
finally
{
    receiver.Disconnect();
    streamer.Stop();
    Console.WriteLine("Kapatıldı.");
}
