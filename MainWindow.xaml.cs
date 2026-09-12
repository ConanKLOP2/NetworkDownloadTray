using System.Windows;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class MainWindow : Window
{
    private readonly DownloadMonitorService _monitor;

    public MainWindow(DownloadMonitorService monitor)
    {
        _monitor = monitor;
        InitializeComponent();
        _monitor.SpeedUpdated += Monitor_SpeedUpdated;
        Loaded += (_, _) => UpdateTrayStatus();
    }

    private void Monitor_SpeedUpdated(object? sender, DownloadSpeedSnapshot snapshot)
    {
        SpeedText.Text = snapshot.IsMeasuring
            ? "Đang đo..."
            : snapshot.IsAvailable
                ? $"{snapshot.MegabitsPerSecond:0} Mbps"
                : "Không khả dụng";
        AdapterText.Text = snapshot.IsAvailable
            ? $"Adapter: {snapshot.AdapterDescription}"
            : "Không có network adapter đang hoạt động";
        UpdateTrayStatus();
    }

    private void UpdateTrayStatus()
    {
        TrayStatusText.Text = _monitor.IsTrayIconCreated
            ? "Tray icon registered"
            : "Tray icon not registered";
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _monitor.SpeedUpdated -= Monitor_SpeedUpdated;
        if (!Application.Current.ShutdownMode.Equals(ShutdownMode.OnExplicitShutdown))
        {
            base.OnClosing(e);
            return;
        }

        // Closing the test window should not stop the tray monitor.
        e.Cancel = true;
        Hide();
    }
}
