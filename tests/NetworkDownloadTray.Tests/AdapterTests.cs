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
    public void OverrideCanIncludeVirtualButNotDisconnectedAdapter()
    {
        Assert.NotEmpty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up));
        Assert.Empty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true));
        Assert.NotEmpty(AdapterFilter.GetExclusionReason("VPN", "Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Down, true));
    }
}
