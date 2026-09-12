using System.IO;

namespace NetworkDownloadTray.Services;

public static class AppLog
{
    private static readonly object Gate = new();
    private static DateTime _lastWrite;
    public static void Error(string operation, Exception exception)
    {
        lock (Gate)
        {
            // Rate-limit repeated failures and keep the diagnostic log bounded.
            if (DateTime.UtcNow - _lastWrite < TimeSpan.FromSeconds(5)) return;
            _lastWrite = DateTime.UtcNow;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkDownloadTray");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTime.UtcNow:O} {operation}: {exception}\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
