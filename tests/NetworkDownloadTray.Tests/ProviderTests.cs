using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Services;
using NetworkDownloadTray.Services.Native;

namespace NetworkDownloadTray.Tests;

public class ProviderTests
{
    private sealed record Row(string Description, uint Type, uint Status, long InOctets);

    private static Dictionary<Guid, Row> ReadTable()
    {
        Assert.Equal(0, IpHelperApi.GetIfTable2(out IntPtr table));
        try
        {
            var rows = new Dictionary<Guid, Row>();
            for (int i = 0; i < IpHelperApi.RowCount(table); i++)
            {
                IntPtr row = IpHelperApi.Row(table, i);
                rows.TryAdd(IpHelperApi.InterfaceGuid(row), new(IpHelperApi.Description(row),
                    IpHelperApi.Type(row), IpHelperApi.OperStatus(row), IpHelperApi.InOctets(row)));
            }
            return rows;
        }
        finally { IpHelperApi.FreeMibTable(table); }
    }

    [Fact]
    public void NativeTableMatchesNetworkInterface()
    {
        var rows = ReadTable();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!Guid.TryParse(adapter.Id, out var guid)) continue;
            Assert.True(rows.TryGetValue(guid, out var row), adapter.Id);
            Assert.Equal(adapter.Description, row.Description);
            Assert.Equal((uint)adapter.NetworkInterfaceType, row.Type);
            Assert.Equal((uint)adapter.OperationalStatus, row.Status);
            long managed;
            try { managed = adapter.GetIPStatistics().BytesReceived; }
            catch (NetworkInformationException) { continue; }
            Assert.InRange(Math.Abs(managed - row.InOctets), 0, 10L * 1024 * 1024);
        }
    }

    [Fact]
    public void ReadReturnsSameAdaptersInSameOrderAsNetworkInterface()
    {
        var expected = NetworkInterface.GetAllNetworkInterfaces().Select(x => x.Id).ToArray();
        var actual = new NetworkStatisticsProvider().Read();
        Assert.Equal(expected, actual.Select(x => x.Id).ToArray());
        Assert.All(actual, x => Assert.NotNull(x.Name));
    }

    [Fact]
    public void SecondReadUsesCachedMembershipUntilInvalidated()
    {
        var provider = new NetworkStatisticsProvider();
        provider.Read();
        Assert.Equal(1, provider.EnumerationCount);
        for (int i = 0; i < 10; i++) provider.Read();
        // Tolerate one refresh from a real NetworkChange event racing the test.
        Assert.InRange(provider.EnumerationCount, 1, 2);
        int count = provider.EnumerationCount;
        provider.Invalidate();
        provider.Read();
        Assert.Equal(count + 1, provider.EnumerationCount);
    }
}
