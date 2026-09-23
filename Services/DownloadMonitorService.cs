using System.Windows.Threading;
using Microsoft.Win32;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

// Samples off the UI thread; publishes immutable results on the WPF dispatcher.
// Sampling pauses while the session is locked/disconnected.
public sealed class DownloadMonitorService : IDisposable
{
    private readonly NetworkSpeedReader _reader;
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private bool _disposed, _busy, _started, _locked;
    private int _generation;

    public event EventHandler<DownloadSpeedSnapshot>? SpeedUpdated;
    public event EventHandler<IReadOnlyList<NetworkAdapterDiagnostic>>? DiagnosticsUpdated;
    public event EventHandler<NetworkReadResult>? SampleUpdated;
    public NetworkReadResult? Latest { get; private set; }

    public DownloadMonitorService(NetworkSpeedReader? reader = null)
    {
        _reader = reader ?? new();
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    // True while the 1 s timer runs (never before Start, after Dispose, or while locked).
    internal bool IsSampling => _timer.IsEnabled;

    public void ConfigureAdapters(IReadOnlyDictionary<string, bool> overrides)
    {
        _generation++;
        _reader.ConfigureAdapters(overrides);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _started = true;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        if (_locked) return;
        _timer.Start();
        _ = RefreshAsync();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) => Post(e.Mode switch
    {
        PowerModes.Suspend => OnSuspended,
        PowerModes.Resume => OnResumed,
        _ => ResetMeasurements
    });

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                Post(OnSessionLocked); break;
            case SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                Post(OnSessionUnlocked); break;
        }
    }

    // SystemEvents fire on their own thread; every state change happens on the dispatcher.
    private void Post(Action action)
    {
        if (!_disposed && !_dispatcher.HasShutdownStarted)
            _dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
    }

    internal void OnSessionLocked() { _locked = true; Pause(); }
    internal void OnSessionUnlocked() { _locked = false; Resume(); }
    // Suspend only resets: the timer cannot tick while asleep, and SystemEvents never reports
    // PBT_APMRESUMEAUTOMATIC, so stopping here could leave sampling stopped after wake.
    internal void OnSuspended() => ResetMeasurements();
    internal void OnResumed() => Resume();

    private void Pause()
    {
        _timer.Stop();
        ResetMeasurements();
    }

    private void Resume()
    {
        ResetMeasurements();
        if (_disposed || !_started || _locked) return;
        _timer.Start();
        _ = RefreshAsync();
    }

    private async void OnTick(object? sender, EventArgs e) => await RefreshAsync();

    internal void ResetMeasurements()
    {
        _generation++;
        _reader.Reset();
    }

    public async Task RefreshAsync()
    {
        if (_disposed || _busy) return;
        _busy = true;
        int generation = _generation;
        try
        {
            NetworkReadResult result = await Task.Run(_reader.ReadWithDiagnostics);
            if (_disposed || generation != _generation) return;
            Latest = result;
            SampleUpdated?.Invoke(this, result);
            SpeedUpdated?.Invoke(this, result.Snapshot);
            DiagnosticsUpdated?.Invoke(this, result.Diagnostics);
        }
        catch (Exception ex)
        {
            AppLog.Error("Monitor update", ex);
        }
        finally { _busy = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _timer.Stop();
        _timer.Tick -= OnTick;
        if (!_started) return;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
