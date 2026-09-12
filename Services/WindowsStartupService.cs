using Microsoft.Win32;

namespace NetworkDownloadTray.Services;

public sealed class WindowsStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NetworkDownloadTray";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null) return;
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable.");
            key.SetValue(AppName, $"\"{executable}\"");
        }
        else key.DeleteValue(AppName, false);
    }
}
