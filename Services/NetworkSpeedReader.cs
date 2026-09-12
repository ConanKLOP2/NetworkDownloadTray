using System.Diagnostics;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class NetworkSpeedReader
{
    private readonly object _gate = new();
    private readonly AdapterCounterTracker _tracker = new();
    private readonly INetworkStatisticsProvider _provider;
    private readonly Func<double> _clock;
    private IReadOnlyDictionary<string, bool> _overrides = new Dictionary<string, bool>();

    public NetworkSpeedReader(INetworkStatisticsProvider? provider = null, Func<double>? clock = null)
    {
        _provider = provider ?? new NetworkStatisticsProvider();
        _clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    }

    public void ConfigureAdapters(IReadOnlyDictionary<string, bool> overrides)
    {
        lock (_gate)
        {
            _overrides = new Dictionary<string, bool>(overrides);
            _tracker.Reset();
        }
    }

    public void Reset() { lock (_gate) _tracker.Reset(); }

    public NetworkReadResult ReadWithDiagnostics()
    {
        lock (_gate)
        {
            try { return ReadCore(); }
            catch (System.Net.NetworkInformation.NetworkInformationException ex)
            {
                _tracker.Reset();
                AppLog.Error("Read network adapters", ex);
                return new(new(0, "Network statistics unavailable", false, false), []);
            }
        }
    }

    private NetworkReadResult ReadCore()
    {
        var diagnostics = new List<NetworkAdapterDiagnostic>();
        var counters = new Dictionary<string, long>();
        foreach (var adapter in _provider.Read())
        {
            long? bytes = adapter.BytesReceived;
            string reason = AdapterFilter.GetExclusionReason(adapter.Name, adapter.Description,
                adapter.Type, adapter.Status,
                _overrides.TryGetValue(adapter.Id, out bool forced) ? forced : null);
            if (reason.Length == 0 && (!bytes.HasValue || bytes.Value < 0)) reason = "Statistics unavailable";
            bool included = reason.Length == 0;
            diagnostics.Add(new(adapter.Name, adapter.Description, adapter.Type,
                adapter.Status, included, bytes, reason)
            {
                Id = adapter.Id,
                SelectionMode = _overrides.TryGetValue(adapter.Id, out bool choice) ? (choice ? "Include" : "Exclude") : "Automatic"
            });
            if (included) counters[adapter.Id] = bytes!.Value;
        }
        var (speed, measuring) = _tracker.Sample(counters, _clock());
        string description = counters.Count == 1
            ? diagnostics.First(x => x.Included).Description : $"{counters.Count} active adapters";
        return new(new(speed, description, measuring, counters.Count > 0), diagnostics);
    }
}
