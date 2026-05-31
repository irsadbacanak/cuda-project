using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class ScreenCapture
{
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint hdc, int nIndex);
    private const int DESKTOPHORZRES = 118; // fiziksel genişlik
    private const int DESKTOPVERTRES = 117; // fiziksel yükseklik

    private readonly ImageCodecInfo    _jpegCodec;
    private readonly EncoderParameters _encParams;

    public ScreenCapture(int quality = 70)
    {
        _jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        _encParams = new EncoderParameters(1);
        _encParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
    }

    public byte[] Capture()
    {
        // DPI ölçeklemesinden bağımsız fiziksel piksel boyutu
        using var refG = Graphics.FromHwnd(nint.Zero);
        nint hdc    = refG.GetHdc();
        int width   = GetDeviceCaps(hdc, DESKTOPHORZRES);
        int height  = GetDeviceCaps(hdc, DESKTOPVERTRES);
        refG.ReleaseHdc(hdc);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

        using var ms = new MemoryStream();
        bmp.Save(ms, _jpegCodec, _encParams);
        return ms.ToArray();
    }
}
