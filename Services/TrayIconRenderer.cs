using System.Windows.Media;
using H.NotifyIcon;

namespace NetworkDownloadTray.Services;

public static class TrayIconRenderer
{
    public static GeneratedIconSource Create(double megabitsPerSecond, bool measuring)
    {
        string text = measuring ? "..." : DownloadSpeedCalculator.FormatMegabits(megabitsPerSecond);
        double fontSize = text.Length switch
        {
            <= 2 => 32,
            3 => 25,
            _ => 19
        };

        return new GeneratedIconSource
        {
            Text = text,
            FontFamily = new FontFamily("Tahoma"),
            FontSize = fontSize,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 55)),
            Background = Brushes.Transparent
        };
    }
}
