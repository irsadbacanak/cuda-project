using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

public class InputReceiver
{
    private readonly int _port;
    private TcpListener? _listener;
    private bool _running;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern ushort MapVirtualKey(uint uCode, uint uMapType);

    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE       = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
    private const uint MOUSEEVENTF_WHEEL      = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE   = 0x8000;
    private const uint KEYEVENTF_KEYUP        = 0x0002;

    public InputReceiver(int port) => _port = port;

    public void Start()
    {
        _running  = true;
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine($"[Input] Port {_port}'da kontrol dinliyor...");
        Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync();
                Console.WriteLine("[Input] Kontrol istemcisi bağlandı.");
                _ = HandleClient(client);
            }
            catch when (!_running) { break; }
            catch (Exception ex)   { Console.WriteLine($"[Input] Accept hatası: {ex.Message}"); }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        var buf = new byte[9];
        try
        {
            using var stream = client.GetStream();
            while (_running && client.Connected)
            {
                int type = stream.ReadByte();
                if (type < 0) break;

                switch (type)
                {
                    case 0x01: // MouseMove: [4B float xNorm][4B float yNorm]
                        await ReadExact(stream, buf, 8);
                        MoveMouseAbs(BitConverter.ToSingle(buf, 0), BitConverter.ToSingle(buf, 4));
                        break;
                    case 0x02: // MouseDown: [1B button]
                        { int b = stream.ReadByte(); if (b >= 0) MouseButton(b, true); }
                        break;
                    case 0x03: // MouseUp: [1B button]
                        { int b = stream.ReadByte(); if (b >= 0) MouseButton(b, false); }
                        break;
                    case 0x04: // MouseScroll: [4B int delta]
                        await ReadExact(stream, buf, 4);
                        MouseScroll(BitConverter.ToInt32(buf, 0));
                        break;
                    case 0x05: // KeyDown: [2B ushort vk]
                        await ReadExact(stream, buf, 2);
                        InjectKey(BitConverter.ToUInt16(buf, 0), false);
                        break;
                    case 0x06: // KeyUp: [2B ushort vk]
                        await ReadExact(stream, buf, 2);
                        InjectKey(BitConverter.ToUInt16(buf, 0), true);
                        break;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Input] İstemci koptu: {ex.Message}"); }
        finally { client.Close(); Console.WriteLine("[Input] Kontrol istemcisi ayrıldı."); }
    }

    private static async Task ReadExact(NetworkStream s, byte[] buf, int n)
    {
        int r = 0;
        while (r < n)
        {
            int x = await s.ReadAsync(buf.AsMemory(r, n - r));
            if (x == 0) throw new EndOfStreamException();
            r += x;
        }
    }

    private static void MoveMouseAbs(float xNorm, float yNorm)
    {
        // MOUSEEVENTF_ABSOLUTE koordinatları 0-65535 aralığındadır
        var inp = new INPUT
        {
            type = INPUT_MOUSE,
            u    = { mi = new MOUSEINPUT
            {
                dx      = (int)(xNorm * 65535f),
                dy      = (int)(yNorm * 65535f),
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
            }}
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void MouseButton(int btn, bool down)
    {
        uint flags = (btn, down) switch
        {
            (0, true)  => MOUSEEVENTF_LEFTDOWN,   (0, false) => MOUSEEVENTF_LEFTUP,
            (1, true)  => MOUSEEVENTF_RIGHTDOWN,  (1, false) => MOUSEEVENTF_RIGHTUP,
            (2, true)  => MOUSEEVENTF_MIDDLEDOWN, (2, false) => MOUSEEVENTF_MIDDLEUP,
            _          => 0u
        };
        if (flags == 0) return;
        var inp = new INPUT { type = INPUT_MOUSE, u = { mi = new MOUSEINPUT { dwFlags = flags } } };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void MouseScroll(int delta)
    {
        var inp = new INPUT
        {
            type = INPUT_MOUSE,
            u    = { mi = new MOUSEINPUT { mouseData = (uint)delta, dwFlags = MOUSEEVENTF_WHEEL } }
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void InjectKey(ushort vk, bool keyUp)
    {
        ushort scan = MapVirtualKey(vk, 0);
        var inp = new INPUT
        {
            type = INPUT_KEYBOARD,
            u    = { ki = new KEYBDINPUT
            {
                wVk     = vk,
                wScan   = scan,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
            }}
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    public void Stop() { _running = false; _listener?.Stop(); }
}
