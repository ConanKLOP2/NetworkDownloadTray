using System.Windows.Threading;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using H.NotifyIcon;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class DownloadMonitorService : IDisposable
{
    private readonly NetworkSpeedReader _reader = new();
    private readonly DispatcherTimer _timer;
    private readonly TaskbarIcon _taskbarIcon;
    private bool _disposed;
    private Action? _openWindow;

    public event EventHandler<DownloadSpeedSnapshot>? SpeedUpdated;
    public event EventHandler<IReadOnlyList<NetworkAdapterDiagnostic>>? DiagnosticsUpdated;

    public bool IsTrayIconCreated => _taskbarIcon.IsCreated;

    public DownloadMonitorService()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Download: measuring...",
            Visibility = Visibility.Visible,
            IconSource = TrayIconRenderer.Create(0, true),
            ContextMenu = CreateContextMenu()
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

    public void SetOpenWindowAction(Action openWindow) => _openWindow = openWindow;

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) => _openWindow?.Invoke();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(openItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void OnTick(object? sender, EventArgs e) => Update();

    private void Update()
    {
        DownloadSpeedSnapshot snapshot = _reader.Read();
        DiagnosticsUpdated?.Invoke(this, _reader.GetDiagnostics());
        SpeedUpdated?.Invoke(this, snapshot);
        _taskbarIcon.IconSource = TrayIconRenderer.Create(snapshot.MegabitsPerSecond, snapshot.IsMeasuring);
        _taskbarIcon.Visibility = Visibility.Visible;

        _taskbarIcon.ToolTipText = snapshot.IsAvailable
            ? snapshot.IsMeasuring
                ? $"Download: measuring...\nAdapter: {snapshot.AdapterDescription}"
                : $"Download: {snapshot.MegabitsPerSecond:0} Mbps\nAdapter: {snapshot.AdapterDescription}"
            : "Download: unavailable\nNo active network adapter";
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
