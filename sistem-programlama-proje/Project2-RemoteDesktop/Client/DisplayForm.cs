using System.Drawing;
using System.Windows.Forms;

public class DisplayForm : Form
{
    private readonly TextBox    _txtIp;
    private readonly Button     _btnConnect;
    private readonly Label      _lblFps;
    private readonly PictureBox _pbRaw;
    private readonly PictureBox _pbSobel;

    private readonly ConnectionManager _rawConn   = new();
    private readonly ConnectionManager _sobelConn = new();

    private int      _frameCount;
    private DateTime _fpsTimer = DateTime.Now;
    private bool     _connected;

    public DisplayForm()
    {
        Text        = "Uzak Masaustu - Ham vs CUDA Sobel";
        Size        = new Size(1400, 620);
        MinimumSize = new Size(900, 500);

        // --- Üst panel ---
        var panel = new Panel { Dock = DockStyle.Top, Height = 44 };

        var lblIp   = new Label { Text = "IP:", Left = 8,  Top = 14, AutoSize = true };
        _txtIp      = new TextBox { Text = "127.0.0.1", Left = 28, Top = 10, Width = 120 };
        _btnConnect = new Button  { Text = "Baglan", Left = 158, Top = 8, Width = 90 };
        _lblFps     = new Label   { Text = "FPS: --", Left = 260, Top = 14, AutoSize = true,
                                    Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lblInfo = new Label
        {
            Text      = "Sol: Ham Görüntü (port 9998)   |   Sağ: CUDA Sobel (port 9999)",
            Left      = 380, Top = 14, AutoSize = true,
            ForeColor = Color.DimGray
        };

        panel.Controls.AddRange(new Control[] { lblIp, _txtIp, _btnConnect, _lblFps, lblInfo });

        // --- İki panel yan yana ---
        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 2
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblRaw = new Label
        {
            Text      = "Ham Görüntü",
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        var lblSobel = new Label
        {
            Text      = "CUDA Sobel",
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(20, 60, 100),
            ForeColor = Color.White
        };

        _pbRaw = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Black
        };
        _pbSobel = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Black
        };

        table.Controls.Add(lblRaw,   0, 0);
        table.Controls.Add(lblSobel, 1, 0);
        table.Controls.Add(_pbRaw,   0, 1);
        table.Controls.Add(_pbSobel, 1, 1);

        Controls.Add(table);
        Controls.Add(panel);

        // --- Olaylar ---
        _btnConnect.Click += OnConnectClick;
        FormClosing       += (_, _) => { _rawConn.Disconnect(); _sobelConn.Disconnect(); };

        _rawConn.FrameReceived   += jpeg => OnFrameReceived(jpeg, _pbRaw,   updateFps: false);
        _sobelConn.FrameReceived += jpeg => OnFrameReceived(jpeg, _pbSobel, updateFps: true);
        _rawConn.Disconnected    += OnDisconnected;
        _sobelConn.Disconnected  += OnDisconnected;
    }

    private void OnConnectClick(object? sender, EventArgs e)
    {
        if (_connected)
        {
            _rawConn.Disconnect();
            _sobelConn.Disconnect();
            _btnConnect.Text = "Baglan";
            _connected = false;
            return;
        }

        try
        {
            string ip = _txtIp.Text.Trim();
            _rawConn.Connect(ip, 9998);
            _sobelConn.Connect(ip, 9999);
            _btnConnect.Text = "Baglantıyı Kes";
            _connected = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Baglanti hatasi:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnFrameReceived(byte[] jpeg, PictureBox pb, bool updateFps)
    {
        try
        {
            Image img;
            using (var ms = new MemoryStream(jpeg))
                img = Image.FromStream(ms);

            string? fpsText = null;
            if (updateFps)
            {
                _frameCount++;
                double elapsed = (DateTime.Now - _fpsTimer).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    fpsText     = $"FPS: {_frameCount / elapsed:F1}";
                    _frameCount = 0;
                    _fpsTimer   = DateTime.Now;
                }
            }

            if (!IsHandleCreated || IsDisposed) { img.Dispose(); return; }

            Invoke(() =>
            {
                pb.Image?.Dispose();
                pb.Image = img;
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
