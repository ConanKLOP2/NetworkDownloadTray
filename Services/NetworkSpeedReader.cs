using System.Diagnostics;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

// ConfigureAdapters/Reset are lock-free (the UI thread never waits on provider I/O): they publish an immutable
// override snapshot and bump a generation. Readers are serialized by _readGate, which only readers ever take.
public sealed class NetworkSpeedReader
{
    private readonly object _readGate = new();
    private readonly AdapterCounterTracker _tracker = new();
    private readonly Dictionary<string, long> _counters = new();
    private readonly INetworkStatisticsProvider _provider;
    private readonly Func<double> _clock;
    private volatile IReadOnlyDictionary<string, bool> _overrides = new Dictionary<string, bool>();
    private int _resetGeneration, _appliedGeneration, _countTextFor = -1;
    private string _countText = string.Empty;

    public NetworkSpeedReader(INetworkStatisticsProvider? provider = null, Func<double>? clock = null)
    {
        _provider = provider ?? new NetworkStatisticsProvider();
        _clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    }

    public void ConfigureAdapters(IReadOnlyDictionary<string, bool> overrides)
    {
        _overrides = new Dictionary<string, bool>(overrides);
        Interlocked.Increment(ref _resetGeneration);
    }

    public void Reset() => Interlocked.Increment(ref _resetGeneration);

    public NetworkReadResult ReadWithDiagnostics()
    {
        lock (_readGate)
        {
            // Generation first: observing a generation implies observing the overrides published before it.
            int generation = Volatile.Read(ref _resetGeneration);
            var overrides = _overrides;
            IReadOnlyList<AdapterStatistics> adapters;
            try { adapters = _provider.Read(); }
            catch (NetworkInformationException ex)
            {
                _tracker.Reset();
                _appliedGeneration = generation;
                AppLog.Error("Read network adapters", ex);
                return new(new(0, "Network statistics unavailable", false, false), []);
            }
            var diagnostics = Build(adapters, overrides, out string description);
            double now = _clock();
            int current = Volatile.Read(ref _resetGeneration);
            if (current != _appliedGeneration) { _tracker.Reset(); _appliedGeneration = current; }
            bool available = _counters.Count > 0;
            // Reset/ConfigureAdapters ran during the provider read: the counters predate it (and may use stale
            // overrides), so they must not become a baseline. Discard them; the next read starts the new baseline.
            if (current != generation) return new(new(0, description, available, available), diagnostics);
            var (speed, measuring) = _tracker.Sample(_counters, now);
            return new(new(speed, description, measuring, available), diagnostics);
        }
    }

    private List<NetworkAdapterDiagnostic> Build(IReadOnlyList<AdapterStatistics> adapters,
        IReadOnlyDictionary<string, bool> overrides, out string description)
    {
        var diagnostics = new List<NetworkAdapterDiagnostic>(adapters.Count);
        _counters.Clear();
        string? single = null;
        for (int i = 0; i < adapters.Count; i++)
        {
            var adapter = adapters[i];
            long? bytes = adapter.BytesReceived;
            bool? forced = overrides.TryGetValue(adapter.Id, out bool choice) ? choice : null;
            string reason = AdapterFilter.GetExclusionReason(adapter.Name, adapter.Description,
                adapter.Type, adapter.Status, forced);
            if (reason.Length == 0 && (!bytes.HasValue || bytes.Value < 0)) reason = "Statistics unavailable";
            bool included = reason.Length == 0;
            diagnostics.Add(new(adapter.Name, adapter.Description, adapter.Type,
                adapter.Status, included, bytes, reason)
            {
                Id = adapter.Id,
                SelectionMode = forced switch { true => "Include", false => "Exclude", null => "Automatic" }
            });
            if (included) { _counters[adapter.Id] = bytes!.Value; single ??= adapter.Description; }
        }
        int count = _counters.Count;
        if (count != 1 && count != _countTextFor) { _countText = $"{count} active adapters"; _countTextFor = count; }
        description = count == 1 ? single! : _countText;
        return diagnostics;
    }
}
