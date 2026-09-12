using System.Windows;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class MainWindow : Window
{
    private readonly DownloadMonitorService _monitor;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private DateTime _lastDiagnosticsRefreshUtc = DateTime.MinValue;

    public MainWindow(DownloadMonitorService monitor, AppSettings settings, SettingsService settingsService)
    {
        _monitor = monitor;
        _settings = settings;
        _settingsService = settingsService;
        InitializeComponent();
        AutoStartCheckBox.IsChecked = settings.AutoStartWithWindows;
        StartMinimizedCheckBox.IsChecked = settings.StartMinimizedToTray;
        _monitor.SpeedUpdated += Monitor_SpeedUpdated;
        _monitor.DiagnosticsUpdated += Monitor_DiagnosticsUpdated;
        Loaded += (_, _) => UpdateTrayStatus();
    }

    private void Monitor_DiagnosticsUpdated(object? sender, IReadOnlyList<NetworkAdapterDiagnostic> diagnostics)
    {
        if (DateTime.UtcNow - _lastDiagnosticsRefreshUtc < TimeSpan.FromSeconds(5)) return;
        _lastDiagnosticsRefreshUtc = DateTime.UtcNow;
        AdapterGrid.ItemsSource = diagnostics;
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
        new WindowsStartupService().SetEnabled(_settings.AutoStartWithWindows);
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        _settings.AutoStartWithWindows = AutoStartCheckBox.IsChecked == true;
        SaveSettings();
    }

    private void Minimized_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        _settings.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
        SaveSettings();
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

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) app.ShutdownApplication();
        else Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App app && app.IsExiting)
        {
            _monitor.SpeedUpdated -= Monitor_SpeedUpdated;
            _monitor.DiagnosticsUpdated -= Monitor_DiagnosticsUpdated;
            base.OnClosing(e);
            return;
        }

        // The window close button hides the UI; tray monitoring continues.
        e.Cancel = true;
        Hide();
    }
}
