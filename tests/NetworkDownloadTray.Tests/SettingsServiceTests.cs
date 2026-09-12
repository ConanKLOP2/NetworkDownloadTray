using System;
using System.IO;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void FailedSaveDoesNotLeaveTemporaryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetworkDownloadTrayTests", Guid.NewGuid().ToString("N"));
        string target = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(target);
        try
        {
            var service = new SettingsService(target);
            Exception? error = Record.Exception(() => service.Save(new AppSettings()));
            Assert.True(error is IOException or UnauthorizedAccessException,
                "Saving over a directory must report a filesystem error.");
            Assert.Empty(Directory.GetFiles(directory));
            Assert.True(Directory.Exists(target));
        }
        finally { Directory.Delete(target); Directory.Delete(directory); }
    }

    [Fact]
    public void MissingCorruptAndSavedSettingsAreHandled()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetworkDownloadTrayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "settings.json");
        try
        {
            var service = new SettingsService(file);
            Assert.False(service.Load().StartMinimizedToTray);
            File.WriteAllText(file, "{broken");
            Assert.False(service.Load().StartMinimizedToTray);
            service.Save(new AppSettings { StartMinimizedToTray = true, AdapterOverrides = new() { ["adapter-id"] = false } });
            Assert.True(service.Load().StartMinimizedToTray);
            Assert.False(service.Load().AdapterOverrides["adapter-id"]);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { File.Delete(file); Directory.Delete(directory); }
    }
}
