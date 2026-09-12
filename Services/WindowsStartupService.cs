using System.IO;

namespace NetworkDownloadTray.Services;

public sealed class WindowsStartupService
{
    private readonly IStartupStore _store;
    private readonly Func<string?> _processPath;
    private readonly Func<string, bool> _fileExists;
    public WindowsStartupService(IStartupStore? store = null, Func<string?>? processPath = null,
        Func<string, bool>? fileExists = null)
    {
        _store = store ?? new RegistryStartupStore();
        _processPath = processPath ?? (() => Environment.ProcessPath);
        _fileExists = fileExists ?? File.Exists;
    }
    public string? RegisteredCommand => _store.Read();
    public bool IsEnabled() => !string.IsNullOrWhiteSpace(RegisteredCommand);
    public void SetEnabled(bool enabled)
    {
        if (!enabled) { _store.Write(null); return; }
        string path = _processPath() ?? throw new InvalidOperationException("Process path is unavailable.");
        if (!Path.IsPathFullyQualified(path) || !_fileExists(path) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Start the published NetworkDownloadTray.exe before enabling startup.");
        _store.Write($"\"{path}\"");
    }
}
