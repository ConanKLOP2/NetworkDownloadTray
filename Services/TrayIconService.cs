using System.Windows.Controls;
using H.NotifyIcon;
using NetworkDownloadTray.Models;

namespace NetworkDownloadTray.Services;

public sealed class TrayIconService : ITrayIconService
{
    private readonly TaskbarIcon _icon;
    private string? _lastText;
    private string? _lastTooltip;
    private int _lastSize;
    private bool _disposed;
    private DateTime _nextRetry;
    public bool IsCreated => !_disposed && _icon.IsCreated;
    public string? Error { get; private set; }
    public int PixelSize => _lastSize;
    internal TaskbarIcon NativeIcon => _icon;

    public TrayIconService(Action open, Action exit)
    {
        var menu = new ContextMenu();
        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) => open();
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(openItem); menu.Items.Add(new Separator()); menu.Items.Add(exitItem);
        _icon = new TaskbarIcon { ContextMenu = menu, ToolTipText = "Download: measuring..." };
    }

    public void Update(DownloadSpeedSnapshot snapshot)
    {
        if (_disposed) return;
        try
        {
            int size = TrayIconSize.Read();
            string text = TrayIconRenderer.DisplayText(snapshot.MegabitsPerSecond, snapshot.IsMeasuring, snapshot.IsAvailable);
            if (_lastText != text || _lastSize != size)
            {
                // H.NotifyIcon 2.4.1 owns Icon and disposes its previous value.
                _icon.Icon = TrayIconRenderer.Create(text, size);
                _lastText = text;
                _lastSize = size;
            }
            string tooltip = !snapshot.IsAvailable ? "Download: unavailable"
                : snapshot.IsMeasuring ? "Download: measuring..."
                : $"Download: {DownloadSpeedCalculator.FormatMegabits(snapshot.MegabitsPerSecond)} Mbps";
            string fullTooltip = tooltip + "\n" + snapshot.AdapterDescription;
            fullTooltip = fullTooltip[..Math.Min(120, fullTooltip.Length)];
            // Each assignment is a Shell_NotifyIcon round trip; skip it when nothing changed.
            if (_lastTooltip != fullTooltip)
            {
                _icon.ToolTipText = fullTooltip;
                _lastTooltip = fullTooltip;
            }
            // Library handles TaskbarCreated. Retry if Explorer wasn't ready then.
            if (!_icon.IsCreated && DateTime.UtcNow >= _nextRetry)
            {
                _nextRetry = DateTime.UtcNow.AddSeconds(5);
                _icon.ForceCreate(enablesEfficiencyMode: false);
            }
            Error = _icon.IsCreated ? null : "Waiting for Windows tray";
        }
        catch (Exception ex)
        {
            _lastText = null;
            _lastTooltip = null;
            Error = ex.Message;
            AppLog.Error("Update tray icon", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Dispose();
    }
}
