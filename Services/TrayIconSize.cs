using System.Runtime.InteropServices;

namespace NetworkDownloadTray.Services;

public static class TrayIconSize
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    public static int Read()
    {
        // Use the primary taskbar's DPI, which may differ from the app window.
        uint dpi = GetDpiForWindow(FindWindow("Shell_TrayWnd", null));
        return Math.Clamp(GetSystemMetricsForDpi(49, dpi == 0 ? 96 : dpi), 16, 64);
    }
}
