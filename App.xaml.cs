using System.Windows;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class App : Application
{
    private DownloadMonitorService? _monitor;
    private readonly SettingsService _settingsService = new();

    public bool IsExiting { get; private set; }

    public void ShutdownApplication()
    {
        if (IsExiting) return;
        IsExiting = true;
        Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppSettings settings = _settingsService.Load();
        new WindowsStartupService().SetEnabled(settings.AutoStartWithWindows);

        _monitor = new DownloadMonitorService();
        var window = new MainWindow(_monitor, settings, _settingsService);
        MainWindow = window;
        _monitor.SetOpenWindowAction(window.ShowFromTray);
        if (!settings.StartMinimizedToTray) window.Show();

        // Start after the WPF dispatcher and the visible window are initialized.
        _monitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        base.OnExit(e);
    }
}
