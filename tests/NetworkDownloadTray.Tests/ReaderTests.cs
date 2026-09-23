using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class ReaderTests
{
    private sealed class Provider : INetworkStatisticsProvider
    {
        public int Calls;
        public IReadOnlyList<AdapterStatistics> Values = [];
        public bool Fail;
        public IReadOnlyList<AdapterStatistics> Read()
        {
            Calls++;
            if (Fail) throw new NetworkInformationException();
            return Values;
        }
    }

    [Fact]
    public void ReadsOncePerSampleAndDoesNotCountVirtualDuplicates()
    {
        var provider = new Provider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        provider.Values = [
            new("wifi","Wi-Fi","Intel wireless",NetworkInterfaceType.Wireless80211,OperationalStatus.Up,100),
            new("filter","Wi-Fi WFP","Virtual Filter",NetworkInterfaceType.Wireless80211,OperationalStatus.Up,100)];
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
        clock++;
        provider.Values = [
            provider.Values[0] with { BytesReceived = 1000100 },
            provider.Values[1] with { BytesReceived = 1000100 }];
        var result = reader.ReadWithDiagnostics();
        Assert.Equal(8d, result.Snapshot.MegabitsPerSecond);
        Assert.Equal(2, provider.Calls);
        Assert.False(result.Diagnostics[1].Included);
        reader.ConfigureAdapters(new Dictionary<string,bool> { ["wifi"] = false });
        Assert.False(reader.ReadWithDiagnostics().Snapshot.IsAvailable);
    }

    [Fact]
    public void DescriptionAndSelectionModeFollowOverrides()
    {
        var provider = new Provider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        provider.Values = [
            new("wifi","Wi-Fi","Intel wireless",NetworkInterfaceType.Wireless80211,OperationalStatus.Up,100),
            new("lan","Ethernet","Realtek LAN",NetworkInterfaceType.Ethernet,OperationalStatus.Up,100),
            new("vpn","VPN","Virtual Ethernet",NetworkInterfaceType.Ethernet,OperationalStatus.Up,100)];
        var result = reader.ReadWithDiagnostics();
        Assert.Equal("2 active adapters", result.Snapshot.AdapterDescription);
        Assert.All(result.Diagnostics, x => Assert.Equal("Automatic", x.SelectionMode));
        reader.ConfigureAdapters(new Dictionary<string, bool> { ["wifi"] = false, ["vpn"] = true });
        result = reader.ReadWithDiagnostics();
        Assert.Equal("2 active adapters", result.Snapshot.AdapterDescription);
        Assert.Equal(["Exclude", "Automatic", "Include"], result.Diagnostics.Select(x => x.SelectionMode));
        reader.ConfigureAdapters(new Dictionary<string, bool> { ["lan"] = false });
        result = reader.ReadWithDiagnostics();
        Assert.Equal("Intel wireless", result.Snapshot.AdapterDescription);
        Assert.True(result.Snapshot.IsMeasuring);
    }

    [Fact]
    public void ResetBetweenReadsStartsNewBaseline()
    {
        var provider = new Provider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        provider.Values = [new("wifi","Wi-Fi","Intel",NetworkInterfaceType.Wireless80211,OperationalStatus.Up,100)];
        reader.ReadWithDiagnostics();
        reader.Reset();
        clock++;
        provider.Values = [provider.Values[0] with { BytesReceived = 99999999 }];
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
        clock++;
        provider.Values = [provider.Values[0] with { BytesReceived = 100999999 }];
        Assert.Equal(8d, reader.ReadWithDiagnostics().Snapshot.MegabitsPerSecond);
    }

    [Fact]
    public void ProviderFailureAndUnreadableCounterRecoverWithoutSpikes()
    {
        var provider = new Provider { Fail = true };
        var reader = new NetworkSpeedReader(provider, () => 1);
        Assert.False(reader.ReadWithDiagnostics().Snapshot.IsAvailable);
        provider.Fail = false;
        provider.Values = [new("wifi","Wi-Fi","Intel",NetworkInterfaceType.Wireless80211,OperationalStatus.Up,null)];
        var result = reader.ReadWithDiagnostics();
        Assert.False(result.Diagnostics[0].Included);
        Assert.Equal("Statistics unavailable", result.Diagnostics[0].ExclusionReason);
        provider.Values = [provider.Values[0] with { BytesReceived = 99999999 }];
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
    }
}
