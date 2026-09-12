using System.Windows;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class App : Application
{
    private DownloadMonitorService? _monitor;
    private readonly SettingsService _settingsService = new();
    private TrayIconService? _tray;
    private Mutex? _instance;
    private bool _ownsInstance;

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

        _instance = new Mutex(true, @"Local\NetworkDownloadTray", out _ownsInstance);
        if (!_ownsInstance)
        {
            MessageBox.Show("Network Download Tray is already running. Use Open from its tray menu.");
            Shutdown();
            return;
        }
        AppSettings settings = _settingsService.Load();
        var startup = new WindowsStartupService();
        try { settings.AutoStartWithWindows = startup.IsEnabled(); }
        catch (Exception ex) { AppLog.Error("Read startup status", ex); }

        _monitor = new DownloadMonitorService();
        _monitor.ConfigureAdapters(settings.AdapterOverrides);
        MainWindow? window = null;
        _tray = new TrayIconService(() => window?.ShowFromTray(), ShutdownApplication);
        window = new MainWindow(_monitor, settings, _settingsService, _tray, startup);
        MainWindow = window;
        _tray.Update(new(0, "Starting", true, true));
        _monitor.SampleUpdated += (_, result) => _tray.Update(result.Snapshot);
        if (!settings.StartMinimizedToTray || !_tray.IsCreated) window.Show();
        _monitor.Start();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        IsExiting = true;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        _tray?.Dispose();
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
