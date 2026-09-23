using System.Collections.Generic;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class AdapterTests
{
    [Fact]
    public void NewAdapterDoesNotAddHistoricalBytes()
    {
        var tracker = new AdapterCounterTracker();
        tracker.Sample(new Dictionary<string, long> { ["wifi"] = 100 }, 1);
        var sample = tracker.Sample(new Dictionary<string, long> { ["wifi"] = 1000100, ["vpn"] = 999999999 }, 2);
        Assert.Equal(8d, sample.Speed);
    }

    [Fact]
    public void ResetAndResumeStartNewBaseline()
    {
        var tracker = new AdapterCounterTracker();
        tracker.Sample(new Dictionary<string, long> { ["wifi"] = 100 }, 1);
        Assert.True(tracker.Sample(new Dictionary<string, long> { ["wifi"] = 20 }, 2).Measuring);
        Assert.True(tracker.Sample(new Dictionary<string, long> { ["wifi"] = 1000000 }, 30).Measuring);
    }

    [Fact]
    public void SwappedBuffersKeepOnlyTheLatestCounters()
    {
        var tracker = new AdapterCounterTracker();
        tracker.Sample(new Dictionary<string, long> { ["wifi"] = 100, ["lan"] = 100 }, 1);
        tracker.Sample(new Dictionary<string, long> { ["wifi"] = 200 }, 2);
        // "lan" dropped out last tick, so its return must not be compared with the stale value from tick 1.
        var sample = tracker.Sample(new Dictionary<string, long> { ["wifi"] = 1000200, ["lan"] = 999999999 }, 3);
        Assert.Equal(8d, sample.Speed);
        Assert.False(sample.Measuring);
        tracker.Reset();
        Assert.True(tracker.Sample(new SortedDictionary<string, long> { ["wifi"] = 2000200 }, 4).Measuring);
        Assert.Equal(8d, tracker.Sample(new SortedDictionary<string, long> { ["wifi"] = 3000200 }, 5).Speed);
    }

    [Fact]
    public void ElapsedOutsideWindowIsMeasuringAndEmptyIsNot()
    {
        var tracker = new AdapterCounterTracker();
        Assert.Equal((0d, false), tracker.Sample(new Dictionary<string, long>(), 1));
        tracker.Sample(new Dictionary<string, long> { ["wifi"] = 100 }, 1);
        Assert.True(tracker.Sample(new Dictionary<string, long> { ["wifi"] = 200 }, 1).Measuring);
        Assert.Equal(0.0008, tracker.Sample(new Dictionary<string, long> { ["wifi"] = 1200 }, 11).Speed, 10);
    }

    [Fact]
    public void OverrideCanIncludeVirtualButNotDisconnectedAdapter()
    {
        Assert.NotEmpty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up));
        Assert.Empty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true));
        Assert.NotEmpty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Down, true));
    }
}
