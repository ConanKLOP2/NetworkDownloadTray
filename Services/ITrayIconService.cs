using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public interface ITrayIconService : IDisposable
{
    bool IsCreated { get; }
    string? Error { get; }
    int PixelSize { get; }
    void Update(DownloadSpeedSnapshot snapshot);
}
