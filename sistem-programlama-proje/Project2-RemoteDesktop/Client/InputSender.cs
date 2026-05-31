using System.Net.Sockets;

public class InputSender : IDisposable
{
    private TcpClient?     _client;
    private NetworkStream? _stream;
    private readonly object _lock = new();

    public bool IsConnected { get; private set; }

    public void Connect(string host, int port)
    {
        Dispose();
        _client     = new TcpClient();
        _client.Connect(host, port);
        _stream     = _client.GetStream();
        IsConnected = true;
    }

    public void SendMouseMove(float xNorm, float yNorm) =>
        Send(Build(0x01, BitConverter.GetBytes(xNorm), BitConverter.GetBytes(yNorm)));

    public void SendMouseDown(byte btn) => Send([0x02, btn]);
    public void SendMouseUp(byte btn)   => Send([0x03, btn]);

    public void SendMouseScroll(int delta) =>
        Send(Build(0x04, BitConverter.GetBytes(delta)));

    public void SendKeyDown(ushort vk) =>
        Send(Build(0x05, BitConverter.GetBytes(vk)));

    public void SendKeyUp(ushort vk) =>
        Send(Build(0x06, BitConverter.GetBytes(vk)));

    private static byte[] Build(byte type, params byte[][] parts)
    {
        int len = 1 + parts.Sum(p => p.Length);
        var buf = new byte[len];
        buf[0]  = type;
        int pos = 1;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, buf, pos, p.Length); pos += p.Length; }
        return buf;
    }

    private void Send(byte[] data)
    {
        if (!IsConnected) return;
        try { lock (_lock) _stream?.Write(data, 0, data.Length); }
        catch { IsConnected = false; }
    }

    public void Dispose()
    {
        IsConnected = false;
        _stream?.Close();
        _client?.Close();
        _client = null;
        _stream = null;
    }
}
