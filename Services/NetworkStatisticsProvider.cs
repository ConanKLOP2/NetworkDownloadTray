using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Services.Native;

namespace NetworkDownloadTray.Services;

public sealed record AdapterStatistics(string Id, string Name, string Description,
    NetworkInterfaceType Type, OperationalStatus Status, long? BytesReceived);

public interface INetworkStatisticsProvider
{
    IReadOnlyList<AdapterStatistics> Read();
}

public sealed class NetworkStatisticsProvider : INetworkStatisticsProvider
{
    private const long MaxCacheAgeMs = 30_000;
    // Static so providers are never rooted by NetworkChange; each instance compares generations.
    private static int _networkGeneration;

    static NetworkStatisticsProvider()
    {
        try
        {
            NetworkChange.NetworkAddressChanged += (_, _) => Interlocked.Increment(ref _networkGeneration);
            NetworkChange.NetworkAvailabilityChanged += (_, _) => Interlocked.Increment(ref _networkGeneration);
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { }
    }

    private sealed class CachedAdapter(NetworkInterface adapter, Guid? guid)
    {
        public readonly NetworkInterface Adapter = adapter;
        public readonly Guid? Guid = guid;
        public bool NoRow;
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _indexByGuid = [];
    private CachedAdapter[] _adapters = [];
    private OperationalStatus[] _status = [];
    private long[] _bytes = [];
    private bool[] _found = [];
    private volatile bool _invalid = true;
    private int _seenGeneration;
    private long _enumeratedAt;
    private bool _nativeFailureLogged;

    internal int EnumerationCount { get; private set; }

    internal void Invalidate() => _invalid = true;

    public IReadOnlyList<AdapterStatistics> Read()
    {
        lock (_gate)
        {
            int error;
            IntPtr table;
            try { error = IpHelperApi.GetIfTable2(out table); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                LogNativeFailure(ex);
                return ReadManaged();
            }
            if (error != 0)
            {
                LogNativeFailure(new Win32Exception(error));
                return ReadManaged();
            }
            try
            {
                bool fresh = false;
                if (IsStale()) { Enumerate(); fresh = true; }
                if (!Join(table, fresh) && !fresh)
                {
                    // A cached adapter vanished: refresh membership now so this tick stays complete.
                    Enumerate();
                    Join(table, true);
                }
                return BuildResult();
            }
            finally { IpHelperApi.FreeMibTable(table); }
        }
    }

    private bool IsStale() => _invalid || Volatile.Read(ref _networkGeneration) != _seenGeneration
        || Environment.TickCount64 - _enumeratedAt > MaxCacheAgeMs;

    private void Enumerate()
    {
        int generation = Volatile.Read(ref _networkGeneration);
        var adapters = NetworkInterface.GetAllNetworkInterfaces();
        // Mark fresh only after a successful enumeration so a failure retries next tick.
        _invalid = false;
        _seenGeneration = generation;
        _enumeratedAt = Environment.TickCount64;
        EnumerationCount++;
        _indexByGuid.Clear();
        _adapters = new CachedAdapter[adapters.Length];
        for (int i = 0; i < adapters.Length; i++)
        {
            Guid? guid = Guid.TryParse(adapters[i].Id, out var parsed) ? parsed : null;
            _adapters[i] = new(adapters[i], guid);
            if (guid is { } key) _indexByGuid.TryAdd(key, i);
        }
        _status = new OperationalStatus[adapters.Length];
        _bytes = new long[adapters.Length];
        _found = new bool[adapters.Length];
    }

    // Returns false when an adapter that previously had a table row is missing (removed adapter).
    private bool Join(IntPtr table, bool fresh)
    {
        Array.Clear(_found);
        int count = IpHelperApi.RowCount(table);
        Debug.Assert(count >= 0 && count <= (int.MaxValue - IpHelperApi.RowsOffset) / IpHelperApi.RowSize);
        for (int i = 0; i < count; i++)
        {
            IntPtr row = IpHelperApi.Row(table, i);
            if (!_indexByGuid.TryGetValue(IpHelperApi.InterfaceGuid(row), out int index) || _found[index]) continue;
            _found[index] = true;
            _status[index] = (OperationalStatus)IpHelperApi.OperStatus(row);
            _bytes[index] = IpHelperApi.InOctets(row);
        }
        bool complete = true;
        for (int i = 0; i < _adapters.Length; i++)
        {
            var cached = _adapters[i];
            if (_found[i] || cached.Guid is null) continue;
            // Adapters absent right after enumeration stay on the managed path until the next refresh.
            if (fresh) cached.NoRow = true;
            else if (!cached.NoRow) complete = false;
        }
        return complete;
    }

    private List<AdapterStatistics> BuildResult()
    {
        var result = new List<AdapterStatistics>(_adapters.Length);
        for (int i = 0; i < _adapters.Length; i++)
        {
            var adapter = _adapters[i].Adapter;
            result.Add(_found[i]
                ? new(adapter.Id, adapter.Name, adapter.Description, adapter.NetworkInterfaceType, _status[i], _bytes[i])
                : ToStatistics(adapter));
        }
        return result;
    }

    private void LogNativeFailure(Exception exception)
    {
        if (_nativeFailureLogged) return;
        _nativeFailureLogged = true;
        AppLog.Error("GetIfTable2", exception);
    }

    private static IReadOnlyList<AdapterStatistics> ReadManaged()
    {
        var result = new List<AdapterStatistics>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            result.Add(ToStatistics(adapter));
        return result;
    }

    private static AdapterStatistics ToStatistics(NetworkInterface adapter)
    {
        long? bytes = null;
        try { bytes = adapter.GetIPStatistics().BytesReceived; }
        catch (NetworkInformationException) { }
        return new(adapter.Id, adapter.Name, adapter.Description,
            adapter.NetworkInterfaceType, adapter.OperationalStatus, bytes);
    }
}
