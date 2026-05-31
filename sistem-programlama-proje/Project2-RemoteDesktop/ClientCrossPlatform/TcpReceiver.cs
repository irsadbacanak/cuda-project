using System.Net.Sockets;

public class TcpReceiver
{
    private TcpClient?               _client;
    private NetworkStream?           _stream;
    private CancellationTokenSource? _cts;

    public event Action<byte[]>? FrameReceived;
    public event Action?         Disconnected;

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        _client.Connect(host, port);
        _stream = _client.GetStream();
        _cts    = new CancellationTokenSource();
        Task.Run(() => ReceiveLoop(_cts.Token));
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            var sizeBuf = new byte[4];
            while (!ct.IsCancellationRequested)
            {
                await ReadExactAsync(sizeBuf, 4, ct);
                // Sunucu big-endian uint32 gönderiyor (4-byte boyut)
                if (BitConverter.IsLittleEndian) Array.Reverse(sizeBuf);
                int size = (int)BitConverter.ToUInt32(sizeBuf, 0);

                var data = new byte[size];
                await ReadExactAsync(data, size, ct);
                FrameReceived?.Invoke(data);
            }
        }
        catch (OperationCanceledException) { }
        catch { Disconnected?.Invoke(); }
    }

    private async Task ReadExactAsync(byte[] buf, int count, CancellationToken ct)
    {
        int read = 0;
        while (read < count)
        {
            int r = await _stream!.ReadAsync(buf.AsMemory(read, count - read), ct);
            if (r == 0) throw new EndOfStreamException("Baglanti kesildi");
            read += r;
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        _client = null;
        _stream = null;
    }
}
