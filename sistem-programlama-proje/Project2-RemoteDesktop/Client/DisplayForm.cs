using System.Drawing;
using System.Windows.Forms;

public class DisplayForm : Form
{
    private readonly TextBox    _txtIp;
    private readonly Button     _btnConnect;
    private readonly Label      _lblFps;
    private readonly Label      _lblRaw;
    private readonly CheckBox   _chkControl;
    private readonly PictureBox _pbRaw;
    private readonly PictureBox _pbSobel;

    private readonly ConnectionManager _rawConn    = new();
    private readonly ConnectionManager _sobelConn  = new();
    private readonly InputSender       _inputSender = new();

    private int      _frameCount;
    private DateTime _fpsTimer = DateTime.Now;
    private bool     _connected;
    private Point    _lastSentMousePos;

    public DisplayForm()
    {
        Text        = "Uzak Masaüstü - Masaüstü vs CUDA Sobel";
        Size        = new Size(1400, 620);
        MinimumSize = new Size(900, 500);
        WindowState = FormWindowState.Maximized;
        KeyPreview  = true;

        // --- Üst panel ---
        var panel = new Panel { Dock = DockStyle.Top, Height = 44 };

        var lblIp   = new Label  { Text = "IP:", Left = 8,  Top = 14, AutoSize = true };
        _txtIp      = new TextBox { Text = "127.0.0.1", Left = 28,  Top = 10, Width = 120 };
        _btnConnect = new Button  { Text = "Baglan",    Left = 158, Top = 8,  Width = 90 };
        _lblFps     = new Label
        {
            Text      = "FPS: --",
            Left      = 260, Top = 14,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _chkControl = new CheckBox
        {
            Text      = "Kontrol Modu",
            Left      = 370, Top = 12,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.OrangeRed,
            Enabled   = false
        };

        var lblInfo = new Label
        {
            Text      = "Sol: Uzak Masaüstü (8888)  |  Sağ: CUDA Sobel (9999)  |  Kontrol: port 8889",
            Left      = 500, Top = 14,
            AutoSize  = true,
            ForeColor = Color.DimGray
        };

        panel.Controls.AddRange(new Control[] { lblIp, _txtIp, _btnConnect, _lblFps, _chkControl, lblInfo });

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

        _lblRaw = new Label
        {
            Text      = "Uzak Masaüstü",
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

        table.Controls.Add(_lblRaw,  0, 0);
        table.Controls.Add(lblSobel, 1, 0);
        table.Controls.Add(_pbRaw,   0, 1);
        table.Controls.Add(_pbSobel, 1, 1);

        Controls.Add(table);
        Controls.Add(panel);

        // --- Temel olaylar ---
        _btnConnect.Click += OnConnectClick;
        FormClosing       += (_, _) =>
        {
            _rawConn.Disconnect();
            _sobelConn.Disconnect();
            _inputSender.Dispose();
        };

        _rawConn.FrameReceived   += jpeg => OnFrameReceived(jpeg, _pbRaw,   updateFps: false);
        _sobelConn.FrameReceived += jpeg => OnFrameReceived(jpeg, _pbSobel, updateFps: true);
        _rawConn.Disconnected    += OnDisconnected;
        _sobelConn.Disconnected  += OnDisconnected;

        // --- Kontrol modu olayları ---
        _chkControl.CheckedChanged += OnControlModeChanged;
        _pbRaw.MouseMove           += OnRawMouseMove;
        _pbRaw.MouseDown           += OnRawMouseDown;
        _pbRaw.MouseUp             += OnRawMouseUp;
        _pbRaw.MouseEnter          += OnRawMouseEnter;
        _pbRaw.MouseLeave          += OnRawMouseLeave;
        _pbRaw.Paint               += OnRawPaint;
    }

    // ── Bağlantı ────────────────────────────────────────────────────────────

    private void OnConnectClick(object? sender, EventArgs e)
    {
        if (_connected)
        {
            _rawConn.Disconnect();
            _sobelConn.Disconnect();
            _inputSender.Dispose();
            _btnConnect.Text    = "Baglan";
            _chkControl.Checked = false;
            _chkControl.Enabled = false;
            _connected          = false;
            return;
        }

        try
        {
            string ip = _txtIp.Text.Trim();
            _rawConn.Connect(ip, 8888);     // C# Sunucu → gerçek masaüstü
            _sobelConn.Connect(ip, 9999);   // CUDA Sobel stream
            _inputSender.Connect(ip, 8889); // Kontrol kanalı

            _btnConnect.Text    = "Baglantıyı Kes";
            _chkControl.Enabled = true;
            _connected          = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Baglanti hatasi:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Frame gösterimi ─────────────────────────────────────────────────────

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
            _inputSender.Dispose();
            _btnConnect.Text    = "Baglan";
            _chkControl.Checked = false;
            _chkControl.Enabled = false;
            _connected          = false;
            _lblFps.Text        = "FPS: --";
        });
    }

    // ── Kontrol modu ────────────────────────────────────────────────────────

    private void OnControlModeChanged(object? sender, EventArgs e)
    {
        bool on       = _chkControl.Checked;
        _lblRaw.BackColor = on ? Color.FromArgb(120, 30, 30) : Color.FromArgb(40, 40, 40);
        _lblRaw.Text      = on ? "Uzak Masaüstü  [KONTROL AKTİF]" : "Uzak Masaüstü";
        _pbRaw.Invalidate();
    }

    private void OnRawMouseEnter(object? sender, EventArgs e)
    {
        if (_chkControl.Checked)
            _pbRaw.Cursor = Cursors.Cross;
    }

    private void OnRawMouseLeave(object? sender, EventArgs e)
    {
        _pbRaw.Cursor = Cursors.Default;
    }

    private void OnRawMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_chkControl.Checked || !_inputSender.IsConnected) return;

        // Küçük hareketleri filtrele (ağ trafiğini azaltır)
        if (Math.Abs(e.X - _lastSentMousePos.X) < 2 &&
            Math.Abs(e.Y - _lastSentMousePos.Y) < 2) return;

        _lastSentMousePos = e.Location;
        float xNorm = (float)e.X / _pbRaw.Width;
        float yNorm = (float)e.Y / _pbRaw.Height;
        _inputSender.SendMouseMove(xNorm, yNorm);
    }

    private void OnRawMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_chkControl.Checked || !_inputSender.IsConnected) return;
        int btn = e.Button switch
        {
            MouseButtons.Left   => 0,
            MouseButtons.Right  => 1,
            MouseButtons.Middle => 2,
            _ => -1
        };
        if (btn >= 0) _inputSender.SendMouseDown((byte)btn);
    }

    private void OnRawMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_chkControl.Checked || !_inputSender.IsConnected) return;
        int btn = e.Button switch
        {
            MouseButtons.Left   => 0,
            MouseButtons.Right  => 1,
            MouseButtons.Middle => 2,
            _ => -1
        };
        if (btn >= 0) _inputSender.SendMouseUp((byte)btn);
    }

    private void OnRawPaint(object? sender, PaintEventArgs e)
    {
        if (!_chkControl.Checked) return;
        using var pen = new Pen(Color.OrangeRed, 3);
        e.Graphics.DrawRectangle(pen, 1, 1, _pbRaw.Width - 3, _pbRaw.Height - 3);
    }

    // ── Klavye (tüm tuşları yakalar) ────────────────────────────────────────

    protected override bool ProcessKeyPreview(ref Message m)
    {
        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP   = 0x101;

        // IP kutusuna yazarken yerel tuşları engelleme
        if (_chkControl.Checked && _inputSender.IsConnected && !_txtIp.Focused)
        {
            ushort vk = (ushort)m.WParam;
            if (m.Msg == WM_KEYDOWN)    _inputSender.SendKeyDown(vk);
            else if (m.Msg == WM_KEYUP) _inputSender.SendKeyUp(vk);
            return true; // yerel işlemeyi engelle
        }
        return base.ProcessKeyPreview(ref m);
    }

    // ── Fare tekerleği ──────────────────────────────────────────────────────

    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEWHEEL = 0x020A;

        if (m.Msg == WM_MOUSEWHEEL && _chkControl.Checked && _inputSender.IsConnected)
        {
            var pos = PointToClient(Cursor.Position);
            if (_pbRaw.Bounds.Contains(pos))
            {
                int delta = (short)(m.WParam.ToInt64() >> 16);
                _inputSender.SendMouseScroll(delta);
                return;
            }
        }
        base.WndProc(ref m);
    }
}
