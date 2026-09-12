using NetworkDownloadTray.Models;
using System.Text.Json;

namespace NetworkDownloadTray.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void SettingsRoundTripPreservesValues()
    {
        var original = new AppSettings { AutoStartWithWindows = true, StartMinimizedToTray = true };
        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.True(restored!.AutoStartWithWindows);
        Assert.True(restored.StartMinimizedToTray);
    }
}
