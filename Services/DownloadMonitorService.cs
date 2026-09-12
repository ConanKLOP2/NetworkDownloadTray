using System.Windows.Threading;
using Microsoft.Win32;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

// Samples off the UI thread; publishes immutable results on the WPF dispatcher.
public sealed class DownloadMonitorService : IDisposable
{
    private readonly NetworkSpeedReader _reader;
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private bool _disposed, _busy, _started;
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
        _timer.Start();
        _ = RefreshAsync();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (!_disposed && !_dispatcher.HasShutdownStarted)
            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed) return;
                ResetMeasurements();
                if (e.Mode == PowerModes.Resume) _ = RefreshAsync();
            }));
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
        if (_started) SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
