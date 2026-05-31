using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Channels;

public class HttpStreamer
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _clients = new();

    // Her istemciye en fazla 2 frame bekletir; dolarsa eskiyi atar (lag önleme)
    private static readonly BoundedChannelOptions ChannelOpts = new(2)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    };

    private static readonly byte[] IndexHtml = Encoding.UTF8.GetBytes("""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>Remote Desktop Stream</title>
          <style>
            body { margin: 0; background: #111; display: flex; flex-direction: column;
                   align-items: center; justify-content: center; height: 100vh; }
            img  { max-width: 100%; max-height: 100vh; }
            p    { color: #aaa; font-family: monospace; margin-top: 8px; }
          </style>
        </head>
        <body>
          <img src="/stream" alt="stream">
          <p>MJPEG — <a href="/stream" style="color:#6af">/stream</a></p>
        </body>
        </html>
        """);

    public HttpStreamer(int port)
    {
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _ = AcceptLoop();
    }

    private async Task AcceptLoop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }

            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path == "/stream")
                _ = HandleStreamAsync(ctx);
            else
                _ = HandleIndexAsync(ctx);
        }
    }

    private async Task HandleIndexAsync(HttpListenerContext ctx)
    {
        ctx.Response.StatusCode   = 200;
        ctx.Response.ContentType  = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = IndexHtml.Length;
        try
        {
            await ctx.Response.OutputStream.WriteAsync(IndexHtml);
        }
        catch { }
        finally { ctx.Response.Close(); }
    }

    private async Task HandleStreamAsync(HttpListenerContext ctx)
    {
        var id      = Guid.NewGuid();
        var channel = Channel.CreateBounded<byte[]>(ChannelOpts);
        _clients[id] = channel;

        ctx.Response.StatusCode  = 200;
        ctx.Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
        // Tarayıcının arabelleği boşaltmasını engellemeyelim
        ctx.Response.SendChunked = true;

        var stream = ctx.Response.OutputStream;
        try
        {
            await foreach (var jpeg in channel.Reader.ReadAllAsync())
            {
                // MJPEG part header
                var header = Encoding.ASCII.GetBytes(
                    $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n");

                await stream.WriteAsync(header);
                await stream.WriteAsync(jpeg);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
                await stream.FlushAsync();
            }
        }
        catch { }
        finally
        {
            _clients.TryRemove(id, out _);
            try { ctx.Response.Close(); } catch { }
        }
    }

    // Her yeni frame'i bütün bağlı istemcilere yayınla
    public void PushFrame(byte[] jpeg)
    {
        foreach (var (_, ch) in _clients)
            ch.Writer.TryWrite(jpeg);
    }

    public void Stop()
    {
        foreach (var (_, ch) in _clients)
            ch.Writer.TryComplete();
        try { _listener.Stop(); } catch { }
    }
}
