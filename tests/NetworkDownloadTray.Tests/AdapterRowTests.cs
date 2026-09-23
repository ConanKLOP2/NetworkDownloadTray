using System.Collections.Generic;
using System.Net.NetworkInformation;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Tests;

public class AdapterRowTests
{
    private static readonly NetworkAdapterDiagnostic Data = new("Wi-Fi", "Intel Wireless", NetworkInterfaceType.Wireless80211,
        OperationalStatus.Up, true, 100, string.Empty) { Id = "wifi-id" };

    private static List<string?> Track(AdapterRow row)
    {
        var names = new List<string?>();
        row.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void OnlyChangedBytesRaiseSingleNotification()
    {
        var row = new AdapterRow(Data);
        var names = Track(row);
        row.Apply(Data with { BytesReceived = 200 });
        Assert.Equal(["BytesReceived"], names);
        Assert.Equal(200, row.BytesReceived);
    }

    [Fact]
    public void IdenticalRecordRaisesNothing()
    {
        var row = new AdapterRow(Data);
        var names = Track(row);
        row.Apply(Data with { });
        Assert.Empty(names);
    }

    [Fact]
    public void IncludedChangeIsNotifiedAndVisible()
    {
        var row = new AdapterRow(Data);
        var names = Track(row);
        row.Apply(Data with { Included = false, ExclusionReason = "Virtual adapter", SelectionMode = "Excluded" });
        Assert.False(row.Included);
        Assert.Equal(["Included", "ExclusionReason", "SelectionMode"], names);
    }
}
