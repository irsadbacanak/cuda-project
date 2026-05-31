using System.Drawing;
using System.Windows.Forms;

public class DisplayForm : Form
{
    private readonly TextBox    _txtIp;
    private readonly TextBox    _txtPort;
    private readonly Button     _btnConnect;
    private readonly PictureBox _pictureBox;
    private readonly Label      _lblFps;
    private readonly CheckBox   _chkCuda;

    private readonly ConnectionManager _connection = new();
    private int      _frameCount;
    private DateTime _fpsTimer = DateTime.Now;
    private bool     _connected;

    public DisplayForm()
    {
        Text        = "Uzak Masaustu Istemcisi";
        Size        = new Size(1280, 820);
        MinimumSize = new Size(800, 600);

        // --- Üst panel ---
        var panel = new Panel { Dock = DockStyle.Top, Height = 44 };

        var lblIp   = new Label  { Text = "IP:",   Left = 8,   Top = 14, AutoSize = true };
        _txtIp      = new TextBox { Text = "127.0.0.1", Left = 30,  Top = 10, Width = 110 };
        var lblPort = new Label  { Text = "Port:", Left = 148, Top = 14, AutoSize = true };
        _txtPort    = new TextBox { Text = "8888", Left = 185, Top = 10, Width = 55 };
        _btnConnect = new Button  { Text = "Baglan", Left = 250, Top = 8, Width = 90 };
        _chkCuda    = new CheckBox { Text = "CUDA Modu (port 9999)", Left = 350, Top = 12, AutoSize = true };
        _lblFps     = new Label  { Text = "FPS: --", Left = 570, Top = 14, AutoSize = true,
                                   Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        panel.Controls.AddRange(new Control[] { lblIp, _txtIp, lblPort, _txtPort,
                                                _btnConnect, _chkCuda, _lblFps });

        // --- PictureBox ---
        _pictureBox = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };

        Controls.Add(_pictureBox);
        Controls.Add(panel);

        // --- Olaylar ---
        _chkCuda.CheckedChanged += (_, _) => _txtPort.Text = _chkCuda.Checked ? "9999" : "8888";
        _btnConnect.Click       += OnConnectClick;
        FormClosing             += (_, _) => _connection.Disconnect();

        _connection.FrameReceived += OnFrameReceived;
        _connection.Disconnected  += OnDisconnected;
    }

    private void OnConnectClick(object? sender, EventArgs e)
    {
        if (_connected)
        {
            _connection.Disconnect();
            _btnConnect.Text = "Baglan";
            _connected = false;
            return;
        }

        try
        {
            _connection.Connect(_txtIp.Text.Trim(), int.Parse(_txtPort.Text.Trim()));
            _btnConnect.Text = "Baglantıyı Kes";
            _connected = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Baglanti hatasi:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnFrameReceived(byte[] jpeg)
    {
        try
        {
            Image img;
            using (var ms = new MemoryStream(jpeg))
                img = Image.FromStream(ms);

            _frameCount++;
            double elapsed = (DateTime.Now - _fpsTimer).TotalSeconds;
            string? fpsText = null;
            if (elapsed >= 1.0)
            {
                fpsText     = $"FPS: {_frameCount / elapsed:F1}";
                _frameCount = 0;
                _fpsTimer   = DateTime.Now;
            }

            if (!IsHandleCreated || IsDisposed) { img.Dispose(); return; }

            Invoke(() =>
            {
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = img;
                if (fpsText != null) _lblFps.Text = fpsText;
            });
        }
        catch { }
    }

    private void OnDisconnected()
    {
        if (!IsHandleCreated || IsDisposed) return;
        Invoke(() =>
        {
            _btnConnect.Text = "Baglan";
            _connected = false;
            _lblFps.Text = "FPS: --";
        });
    }
}
