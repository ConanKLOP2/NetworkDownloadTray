using System.Text.Json;
using System.IO;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _filePath;
    public SettingsService(string? filePath = null) => _filePath = Path.GetFullPath(filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetworkDownloadTray", "settings.json"));

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
            settings.AdapterOverrides ??= new();
            return settings;
        }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (UnauthorizedAccessException) { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        string temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _filePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { AppLog.Error("Remove temporary settings file", ex); }
        }
    }
}
