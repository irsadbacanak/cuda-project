using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

public class ScreenCapture
{
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
        var bounds = Screen.PrimaryScreen!.Bounds;
        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);

        using var ms = new MemoryStream();
        bmp.Save(ms, _jpegCodec, _encParams);
        return ms.ToArray();
    }
}
