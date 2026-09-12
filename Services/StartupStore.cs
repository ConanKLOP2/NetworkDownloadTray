using Microsoft.Win32;

namespace NetworkDownloadTray.Services;

public interface IStartupStore
{
    string? Read();
    void Write(string? command);
}

public sealed class RegistryStartupStore : IStartupStore
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NetworkDownloadTray";

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) as string;
    }

    public void Write(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey)
            ?? throw new InvalidOperationException("Cannot access Windows startup settings.");
        if (command == null) key.DeleteValue(AppName, false);
        else key.SetValue(AppName, command);
    }
}
