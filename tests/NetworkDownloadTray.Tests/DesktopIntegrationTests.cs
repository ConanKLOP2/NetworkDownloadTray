using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class DesktopIntegrationTests
{
    private sealed class TestTray : ITrayIconService
    {
        public bool IsCreated => true;
        public string? Error => null;
        public int PixelSize => 16;
        public void Update(DownloadSpeedSnapshot snapshot) { }
        public void Dispose() { }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    private sealed class Store : IStartupStore
    {
        public string? Read() => null;
        public void Write(string? command) => throw new InvalidOperationException("Smoke test must not change startup.");
    }
    private sealed class Provider : INetworkStatisticsProvider
    {
        public long Bytes = 1_000_000;
        public IReadOnlyList<AdapterStatistics> Read() =>
        [
            new("wifi-id", "Wi-Fi", "Intel Wireless (test)", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, Bytes),
            new("virtual-id", "vEthernet", "Hyper-V Virtual Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up, Bytes),
            new("ethernet-id", "Ethernet", "Intel Ethernet (test)", NetworkInterfaceType.Ethernet, OperationalStatus.Down, 0)
        ];
    }

    [Fact]
    public void WindowResumeHideReopenAndExitSmoke()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Application? app = null;
            DownloadMonitorService? monitor = null;
            TestTray? tray = null;
            try
            {
                app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var provider = new Provider();
                double clock = 1;
                monitor = new DownloadMonitorService(new NetworkSpeedReader(provider, () => clock));
                MainWindow? window = null;
                tray = new TestTray();
                string path = Path.Combine(Path.GetTempPath(), "NetworkDownloadTrayTest-" + Guid.NewGuid() + ".json");
                window = new MainWindow(monitor, new AppSettings(), new SettingsService(path), tray,
                    new WindowsStartupService(new Store()));
                monitor.SampleUpdated += (_, sample) => tray.Update(sample.Snapshot);
                tray.Update(new(0, "Test adapter", true, true));
                Assert.True(tray.IsCreated, tray.Error);
                window.Show();
                app.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await monitor.RefreshAsync();
                        clock = 2; provider.Bytes += 10_000_000;
                        await monitor.RefreshAsync();
                        Assert.Equal("80 Mbps", ((TextBlock)window.FindName("SpeedText")).Text);
                        var grid = (DataGrid)window.FindName("AdapterGrid");
                        Assert.Equal(3, grid.Items.Count);
                        monitor.ResetMeasurements();
                        clock++;
                        await monitor.RefreshAsync();
                        Assert.True(monitor.Latest!.Snapshot.IsMeasuring);
                        clock++; provider.Bytes += 10_000_000;
                        await monitor.RefreshAsync();
                        window.Close();
                        Assert.False(window.IsVisible);
                        window.ShowFromTray();
                        Assert.True(window.IsVisible);
                        Assert.Equal("80 Mbps", ((TextBlock)window.FindName("SpeedText")).Text);
                        window.UpdateLayout();
                        string? output = Environment.GetEnvironmentVariable("NDT_TEST_ARTIFACTS");
                        if (output != null)
                        {
                            Directory.CreateDirectory(output);
                            var target = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            target.Render(window);
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(target));
                            using var file = File.Create(Path.Combine(output, "window-smoke.png"));
                            encoder.Save(file);
                        }
                        app.Shutdown();
                    }
                    catch (Exception ex) { failure = ex; app.Shutdown(); }
                }));
                app.Run();
            }
            catch (Exception ex) { failure = ex; }
            finally { monitor?.Dispose(); tray?.Dispose(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Desktop smoke test did not exit.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
