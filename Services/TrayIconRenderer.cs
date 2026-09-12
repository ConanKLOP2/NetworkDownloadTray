using System.Windows.Media;
using System.Windows;
using H.NotifyIcon;

namespace NetworkDownloadTray.Services;

public static class TrayIconRenderer
{
    public static GeneratedIconSource Create(double megabitsPerSecond, bool measuring)
    {
        string text = measuring ? "..." : DownloadSpeedCalculator.FormatMegabits(megabitsPerSecond);
        double fontSize = text.Length switch
        {
            <= 2 => 72,
            3 => 58,
            _ => 44
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
