using System.Windows;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class App : Application
{
    private DownloadMonitorService? _monitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _monitor = new DownloadMonitorService();
        var window = new MainWindow(_monitor);
        MainWindow = window;
        window.Show();

        // Start after the WPF dispatcher and the visible window are initialized.
        _monitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        base.OnExit(e);
    }
}
