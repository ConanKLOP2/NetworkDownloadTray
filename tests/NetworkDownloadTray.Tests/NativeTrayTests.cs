using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Controls;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public sealed class InteractiveDesktopFactAttribute : FactAttribute
{
    public InteractiveDesktopFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NDT_DESKTOP_TESTS") != "1")
            Skip = "Set NDT_DESKTOP_TESTS=1 in an interactive Windows session with Explorer.";
    }
}

public class NativeTrayTests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    [InteractiveDesktopFact]
    public void NativeTrayRegistersCachesRecoversAndDispatchesMenu()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                bool opened = false, exited = false;
                using var tray = new TrayIconService(() => opened = true, () => exited = true);
                var snapshot = new NetworkDownloadTray.Models.DownloadSpeedSnapshot(46, "Test", false, true);
                tray.Update(snapshot);
                Assert.True(tray.IsCreated, tray.Error);
                var icon = tray.NativeIcon.Icon;
                tray.Update(snapshot);
                Assert.Same(icon, tray.NativeIcon.Icon);
                Assert.True(tray.NativeIcon.TrayIcon.TryRemove());
                Assert.False(tray.IsCreated);
                SendMessage(tray.NativeIcon.TrayIcon.MessageWindow.Handle,
                    RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
                Assert.True(tray.IsCreated);
                ((MenuItem)tray.NativeIcon.ContextMenu.Items[0]).RaiseEvent(new(System.Windows.Controls.MenuItem.ClickEvent));
                ((MenuItem)tray.NativeIcon.ContextMenu.Items[2]).RaiseEvent(new(System.Windows.Controls.MenuItem.ClickEvent));
                Assert.True(opened);
                Assert.True(exited);
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
