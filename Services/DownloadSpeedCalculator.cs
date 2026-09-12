namespace NetworkDownloadTray.Services;

public static class DownloadSpeedCalculator
{
    public static double BytesPerSecondToMegabits(long deltaBytes, double elapsedSeconds)
    {
        if (deltaBytes <= 0 || !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return 0;
        return deltaBytes / elapsedSeconds * 8d / 1_000_000d;
    }

    public static string FormatMegabits(double megabitsPerSecond) =>
        Math.Round(double.IsFinite(megabitsPerSecond) ? Math.Max(0, megabitsPerSecond) : 0,
            MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
