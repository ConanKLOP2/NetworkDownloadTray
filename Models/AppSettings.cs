namespace NetworkDownloadTray.Models;

public sealed class AppSettings
{
    public Dictionary<string, bool> AdapterOverrides { get; set; } = new();
    public bool AutoStartWithWindows { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool ShowDiagnostics { get; set; } = true;
}
