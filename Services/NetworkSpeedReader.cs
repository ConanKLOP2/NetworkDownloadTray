using System.Diagnostics;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class NetworkSpeedReader
{
    private long _previousBytes;
    private long _previousTimestamp;
    private bool _hasSample;

    public DownloadSpeedSnapshot Read()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsUsableAdapter)
            .ToArray();

        if (adapters.Length == 0)
        {
            Reset();
            return new(0, "No active adapter", false, false);
        }

        long currentBytes = 0;
        foreach (var adapter in adapters)
        {
            try
            {
                currentBytes += adapter.GetIPv4Statistics().BytesReceived;
            }
            catch (NetworkInformationException)
            {
                // Adapter may disappear while being sampled.
            }
        }

        long now = Stopwatch.GetTimestamp();
        string description = adapters.Length == 1
            ? adapters[0].Description
            : $"{adapters.Length} active adapters";

        if (!_hasSample)
        {
            _previousBytes = currentBytes;
            _previousTimestamp = now;
            _hasSample = true;
            return new(0, description, true, true);
        }

        double elapsedSeconds = (now - _previousTimestamp) / (double)Stopwatch.Frequency;
        long deltaBytes = currentBytes - _previousBytes;

        _previousBytes = currentBytes;
        _previousTimestamp = now;

        if (elapsedSeconds <= 0 || deltaBytes < 0)
        {
            return new(0, description, true, true);
        }

        double megabitsPerSecond = DownloadSpeedCalculator.BytesPerSecondToMegabits(deltaBytes, elapsedSeconds);
        return new(megabitsPerSecond, description, false, true);
    }

    public NetworkReadResult ReadWithDiagnostics()
    {
        var allAdapters = NetworkInterface.GetAllNetworkInterfaces();
        var diagnostics = new List<NetworkAdapterDiagnostic>(allAdapters.Length);
        var includedAdapters = new List<(NetworkInterface Adapter, long BytesReceived)>();

        foreach (var adapter in allAdapters)
        {
            long? bytes = null;
            try { bytes = adapter.GetIPv4Statistics().BytesReceived; } catch (NetworkInformationException) { }

            bool included = IsUsableAdapter(adapter);
            string reason = GetExclusionReason(adapter);
            diagnostics.Add(new NetworkAdapterDiagnostic(adapter.Name, adapter.Description, adapter.NetworkInterfaceType, adapter.OperationalStatus, included, bytes, reason));
            if (included && bytes.HasValue) includedAdapters.Add((adapter, bytes.Value));
        }

        long currentBytes = includedAdapters.Sum(item => item.BytesReceived);
        string description = includedAdapters.Count == 1 ? includedAdapters[0].Adapter.Description : $"{includedAdapters.Count} active adapters";
        long now = Stopwatch.GetTimestamp();

        if (includedAdapters.Count == 0)
        {
            Reset();
            return new(new(0, "No active adapter", false, false), diagnostics);
        }

        if (!_hasSample)
        {
            _previousBytes = currentBytes;
            _previousTimestamp = now;
            _hasSample = true;
            return new(new(0, description, true, true), diagnostics);
        }

        double elapsedSeconds = (now - _previousTimestamp) / (double)Stopwatch.Frequency;
        long deltaBytes = currentBytes - _previousBytes;
        _previousBytes = currentBytes;
        _previousTimestamp = now;

        if (elapsedSeconds <= 0 || deltaBytes < 0)
            return new(new(0, description, true, true), diagnostics);

        double speed = DownloadSpeedCalculator.BytesPerSecondToMegabits(deltaBytes, elapsedSeconds);
        return new(new(speed, description, false, true), diagnostics);
    }

    public IReadOnlyList<NetworkAdapterDiagnostic> GetDiagnostics()
    {
        return NetworkInterface.GetAllNetworkInterfaces().Select(adapter =>
        {
            bool included = IsUsableAdapter(adapter);
            long? bytes = null;
            try { bytes = adapter.GetIPv4Statistics().BytesReceived; } catch (NetworkInformationException) { }
            string reason = GetExclusionReason(adapter);
            return new NetworkAdapterDiagnostic(adapter.Name, adapter.Description, adapter.NetworkInterfaceType, adapter.OperationalStatus, included, bytes, reason);
        }).ToArray();
    }

    public void Reset() => _hasSample = false;

    private static bool IsUsableAdapter(NetworkInterface adapter) =>
        adapter.OperationalStatus == OperationalStatus.Up &&
        IsPhysicalNetworkType(adapter.NetworkInterfaceType) &&
        !IsVirtualAdapter(adapter);

    private static string GetExclusionReason(NetworkInterface adapter)
    {
        if (adapter.OperationalStatus != OperationalStatus.Up) return "Status is not Up";
        if (!IsPhysicalNetworkType(adapter.NetworkInterfaceType)) return "Non-physical interface type";
        if (IsVirtualAdapter(adapter)) return "Virtual/filter interface";
        return string.Empty;
    }

    private static bool IsVirtualAdapter(NetworkInterface adapter)
    {
        string identity = $"{adapter.Name} {adapter.Description}".ToUpperInvariant();
        string[] markers =
        [
            "VIRTUAL",
            "WAN MINIport".ToUpperInvariant(),
            "WFP",
            "FILTER",
            "QOS PACKET SCHEDULER",
            "NETWORK MONITOR",
            "WI-FI DIRECT",
            "LOOPBACK",
            "BLUETOOTH",
            "KERNEL DEBUG",
            "VMWARE",
            "VIRTUALBOX",
            "HYPER-V",
            "TAP-WINDOWS",
            "WIREGUARD"
        ];
        return markers.Any(identity.Contains);
    }

    private static bool IsPhysicalNetworkType(NetworkInterfaceType type) =>
        type == NetworkInterfaceType.Ethernet ||
        type == NetworkInterfaceType.FastEthernetT ||
        type == NetworkInterfaceType.GigabitEthernet ||
        type == NetworkInterfaceType.Wireless80211;
}
