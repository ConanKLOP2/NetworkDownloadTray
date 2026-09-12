using System;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class StartupTests
{
    private sealed class Store : IStartupStore
    {
        public string? Command;
        public string? Read() => Command;
        public void Write(string? command) => Command = command;
    }
    [Fact]
    public void QuotesPublishedPathAndRemovesOnlyOwnEntry()
    {
        var store = new Store();
        var service = new WindowsStartupService(store, () => @"C:\My Apps\NetworkDownloadTray.exe", _ => true);
        service.SetEnabled(true);
        Assert.Equal("\"C:\\My Apps\\NetworkDownloadTray.exe\"", store.Command);
        Assert.True(service.IsEnabled());
        service.SetEnabled(false);
        Assert.Null(store.Command);
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", true)]
    [InlineData(@"C:\missing.exe", false)]
    [InlineData("relative.exe", true)]
    public void RejectsInvalidStartupTarget(string path, bool exists)
    {
        var store = new Store();
        var service = new WindowsStartupService(store, () => path, _ => exists);
        Assert.Throws<InvalidOperationException>(() => service.SetEnabled(true));
        Assert.Null(store.Command);
    }
}
