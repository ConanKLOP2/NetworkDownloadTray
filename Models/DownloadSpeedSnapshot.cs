namespace NetworkDownloadTray.Models;

public sealed record DownloadSpeedSnapshot(
    double MegabitsPerSecond,
    string AdapterDescription,
    bool IsMeasuring,
    bool IsAvailable);
