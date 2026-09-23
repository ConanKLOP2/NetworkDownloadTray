using System.Windows;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class App : Application
{
    private DownloadMonitorService? _monitor;
    private readonly SettingsService _settingsService = new();
    private TrayIconService? _tray;
    private MainWindow? _window;
    private AppSettings? _settings;
    private WindowsStartupService? _startup;
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
        _settings = settings; _startup = startup;
        _tray = new TrayIconService(() => { if (!IsExiting) GetOrCreateWindow().ShowFromTray(); }, ShutdownApplication);
        _tray.Update(new(0, "Starting", true, true));
        _monitor.SampleUpdated += (_, result) => _tray.Update(result.Snapshot);
        // Built on first show only; MainWindow pulls _monitor.Latest on Loaded, so a late window is current.
        if (!settings.StartMinimizedToTray || !_tray.IsCreated) GetOrCreateWindow().Show();
        _monitor.Start();
    }

    private MainWindow GetOrCreateWindow()
    {
        if (_window != null) return _window;
        _window = new MainWindow(_monitor!, _settings!, _settingsService, _tray!, _startup!);
        MainWindow = _window;
        return _window;
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
