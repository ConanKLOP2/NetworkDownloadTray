using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NetworkDownloadTray.Services;

public static class TrayIconRenderer
{
    public static Icon Create(double megabytesPerSecond, bool measuring)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        string text = measuring ? "..." : Format(megabytesPerSecond);
        float fontSize = text.Length switch
        {
            <= 2 => 17f,
            3 => 14f,
            _ => 11f
        };

        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var bounds = new RectangleF(0, 0, 32, 32);
        using var shadowBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
        using var valueBrush = new SolidBrush(Color.FromArgb(255, 255, 220, 80));

        // A subtle shadow keeps the number readable on both light and dark taskbars.
        graphics.DrawString(text, font, shadowBrush, new RectangleF(1, 2, 32, 32), format);
        graphics.DrawString(text, font, valueBrush, bounds, format);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static string Format(double value) => DownloadSpeedCalculator.FormatMegabits(value);
}
