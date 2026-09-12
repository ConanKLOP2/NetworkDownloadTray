using System.Windows.Threading;
using System.Windows;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class DownloadMonitorService : IDisposable
{
    private readonly NetworkSpeedReader _reader = new();
    private readonly DispatcherTimer _timer;
    private readonly TaskbarIcon _taskbarIcon;
    private bool _disposed;

    public event EventHandler<DownloadSpeedSnapshot>? SpeedUpdated;

    public bool IsTrayIconCreated => _taskbarIcon.IsCreated;

    public DownloadMonitorService()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Download: measuring...",
            Visibility = Visibility.Visible,
            IconSource = CreateBitmapImage(0, true)
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        Update();
        // The TaskbarIcon is created in code-behind, so it never receives
        // the Loaded event that normally calls ForceCreate internally.
        _taskbarIcon.ForceCreate(enablesEfficiencyMode: false);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) => Update();

    private void Update()
    {
        DownloadSpeedSnapshot snapshot = _reader.Read();
        SpeedUpdated?.Invoke(this, snapshot);
        using Icon icon = TrayIconRenderer.Create(snapshot.MegabitsPerSecond, snapshot.IsMeasuring);
        _taskbarIcon.IconSource = ConvertToBitmapImage(icon);
        _taskbarIcon.Visibility = Visibility.Visible;

        _taskbarIcon.ToolTipText = snapshot.IsAvailable
            ? snapshot.IsMeasuring
                ? $"Download: measuring...\nAdapter: {snapshot.AdapterDescription}"
                : $"Download: {snapshot.MegabitsPerSecond:0} Mbps\nAdapter: {snapshot.AdapterDescription}"
            : "Download: unavailable\nNo active network adapter";
    }

    private static BitmapImage ConvertToBitmapImage(Icon icon)
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetworkDownloadTray");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "tray-icon.ico");
        using (var fileStream = File.Create(filePath))
        {
            icon.Save(fileStream);
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(filePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapImage CreateBitmapImage(double speed, bool measuring)
    {
        using Icon icon = TrayIconRenderer.Create(speed, measuring);
        return ConvertToBitmapImage(icon);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _taskbarIcon.Dispose();
    }
}
